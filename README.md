# .NET CI/CD Pipeline on Kubernetes

A production-minded delivery pipeline for a containerized .NET service, demonstrating build-once/promote-the-artifact CI/CD, hardened Kubernetes deployment with Kustomize overlays, and operational documentation.

The application itself is intentionally simple — a minimal .NET API. The focus of this project is the **delivery and operations** around it: how code becomes a validated, immutable artifact and is promoted safely through environments.

## What this demonstrates

- **CI as a merge gate** — every pull request is restored, built, tested, and containerized before it can merge.
- **Build once, promote the artifact** — the image is built and tagged by commit SHA a single time, then the *identical* image is promoted through `dev` to `test`. No rebuilds between environments.
- **Immutable, traceable images** — SHA-based tags mean every deployment maps to an exact commit, and rollback is deterministic.
- **Hardened Kubernetes workloads** — non-root, read-only root filesystem, all Linux capabilities dropped, seccomp, resource requests/limits, and liveness/readiness probes.
- **Environment parity via Kustomize** — one shared base with thin per-environment overlays, so environments can't structurally drift.
- **Config-driven health** — readiness reflects configured dependency health, evaluated per environment.
- **Operational docs** — a runbook (detect/diagnose/remediate) and a deployment-strategy comparison (rolling vs blue-green vs canary).

## Architecture
## Application endpoints

| Endpoint | Purpose |
|---|---|
| `GET /` | Service metadata (name, environment, region, version) |
| `GET /health/live` | Liveness — is the process alive |
| `GET /health/ready` | Readiness — can it serve traffic (checks configured dependencies) |
| `GET /api/work-items` | Sample application data from configuration |

## Project layout
## Running locally

Build and run the container:

```bash
docker build -t dotnet-api:local .
docker run -d --name dotnet-api -p 8080:8080 dotnet-api:local
```

Verify it:

```bash
curl http://localhost:8080/
curl http://localhost:8080/health/ready
curl http://localhost:8080/api/work-items
```

## Kubernetes

Render the manifests for an environment with Kustomize:

```bash
kubectl kustomize k8s/overlays/dev
```

The base defines the hardened Deployment and Service. Each overlay sets only what differs per environment — namespace, image tag, and configuration — so shared concerns (security context, probes, resource limits, replica count) are defined once and inherited identically.

## Production path

Deliberate scope tradeoffs were made to keep this focused. The natural next steps for a real production deployment:

- Image vulnerability scanning as a CI gate (e.g. Trivy), failing on critical/high.
- Managed Kubernetes cluster via a kubeconfig secret, with infrastructure as code (Terraform).
- Manual approval gate before `test`/`prod` using GitHub Environments with required reviewers.
- Canary deploys with automated rollback (Argo Rollouts / Flagger), gated on the same SLIs used for alerting.
- Pod Disruption Budgets and Horizontal Pod Autoscaling.
- Real secrets from a manager (e.g. HashiCorp Vault) rather than pipeline tokens.
- Distinguishing hard vs soft dependencies in readiness so a degraded non-critical dependency doesn't remove the whole service from rotation.

## Tech
.NET · Docker (multi-stage) · GitHub Actions · Kubernetes · Kustomize · GHCR · xUnit