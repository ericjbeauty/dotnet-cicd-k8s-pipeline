# Runbook: CandidateApi

Operational guide for the CandidateApi service. Covers the most likely failure
scenarios, how to detect them, how to diagnose, and how to remediate.

## Service overview

- **Service:** candidate-api (.NET 10 Web API)
- **Endpoints:**
  - `GET /` – service metadata
  - `GET /health/live` – liveness (process is up)
  - `GET /health/ready` – readiness (all configured dependencies healthy)
  - `GET /api/work-items` – sample application data
- **Dependencies** (from configuration): `postgres` (database), `redis` (cache),
  `third-party-billing` (http). Readiness fails if any dependency reports unhealthy.
- **Deployment:** Kubernetes via Kustomize overlays (`candidate-api-dev`,
  `candidate-api-test`). 2 replicas, rolling updates by default.

---

## Scenario 1: Readiness check failing (pods not receiving traffic)

### Detection
- Kubernetes Service stops routing to pods; `kubectl get pods` shows pods
  `Running` but `0/1 READY`.
- Alert: readiness probe failure rate > 0 for > 2 minutes (see Alerting).
- User impact: requests fail or are load-balanced only to healthy replicas.

### Diagnosis
1. Confirm which pods are not ready:
   `kubectl -n <ns> get pods -l app=candidate-api`
2. Inspect the readiness endpoint from inside the cluster:
   `kubectl -n <ns> exec <pod> -- wget -qO- localhost:8080/health/ready`
3. Identify the unhealthy dependency in the response payload.
4. Check the dependency directly:
   - postgres: connectivity / credentials / connection pool exhaustion
   - redis: connectivity / memory pressure / eviction
   - third-party-billing: upstream HTTP status, latency, rate limiting

### Remediation
- If a real dependency is down: restore or fail over the dependency. Readiness
  recovers automatically once the dependency reports healthy.
- If the dependency is healthy but config is wrong: correct the `CandidateApi`
  dependency configuration in the overlay's `config.env`, re-apply, and let the
  ConfigMap hash trigger a rolling restart.
- If degradation is in a non-critical dependency, consider whether it should
  gate readiness at all (see Follow-ups).

---

## Scenario 2: Deployment rollout failing / bad release

### Detection
- `kubectl rollout status` times out in the CD pipeline; the deploy job fails.
- New pods crash-loop (`CrashLoopBackOff`) or never become ready.

### Diagnosis
1. `kubectl -n <ns> rollout status deployment/candidate-api`
2. `kubectl -n <ns> get pods` – look for restarts / crash loops.
3. `kubectl -n <ns> logs deployment/candidate-api --previous` for crash output.
4. `kubectl -n <ns> describe pod <pod>` for scheduling / image-pull / probe events.

### Remediation
- Roll back to the previous known-good image (immutable SHA tag):
  `kubectl -n <ns> rollout undo deployment/candidate-api`
- Because images are SHA-pinned and built once, the previous revision is an
  exact, reproducible artifact.
- Promotion to `test` is gated on `dev` succeeding, so a bad build is contained
  to dev before it can reach test.

---

## Scenario 3: Dependent service degraded (e.g. third-party-billing slow)

### Detection
- Elevated request latency / error rate on endpoints that touch the dependency.
- Readiness may flap if the dependency intermittently reports unhealthy.

### Diagnosis
1. Correlate latency/error spikes with the dependency via dashboards.
2. Check the dependency's own health and status page.
3. Determine blast radius: is it all requests or only dependency-bound paths?

### Remediation
- Short term: if the dependency is non-critical, degrade gracefully rather than
  failing readiness for the whole service.
- Apply timeouts / retries with backoff / circuit breaking at the client.
- Communicate status; engage the dependency owner / vendor.

---

## Alerting strategy

| Condition | Severity | Action |
|---|---|---|
| Readiness failing on > 50% of replicas for > 2m | P1 (page) | On-call paged; begin Scenario 1 |
| Liveness restarts > 3 in 10m on a pod | P2 | Investigate crash loop (Scenario 2) |
| p99 latency > SLO threshold for > 5m | P2 | Investigate dependency / saturation |
| Error rate (5xx) > 2% for > 5m | P1 (page) | Begin triage; consider rollback |
| Deploy rollout failed | P2 | Auto-rollback + notify release owner |

**Reducing alert fatigue:**
- Alert on **symptoms users feel** (latency, error rate, readiness), not on
  every transient blip.
- Use `for:` durations so brief spikes self-resolve without paging.
- Page only on P1; route P2 to a queue/channel for business-hours follow-up.
- Tie alerts to SLO burn rate (fast-burn pages, slow-burn tickets) rather than
  static thresholds where possible.

---

## Escalation path

1. On-call SRE (primary) – acknowledge within alert SLA.
2. Service owner / backend team – if the issue is application logic.
3. Dependency owner / vendor – if a downstream dependency is at fault.
4. Engineering manager – for prolonged P1 or customer-facing incidents.

---

## Follow-ups / hardening

- Distinguish **hard** vs **soft** dependencies in readiness so a degraded
  non-critical dependency doesn't remove the whole service from rotation.
- Add Pod Disruption Budgets and Horizontal Pod Autoscaling for resilience.
- Add a canary or blue-green strategy for higher-risk releases (see
  docs/deployment-strategy.md).
