# Deployment Strategy

## Current approach: rolling updates

The base Deployment uses Kubernetes' default **rolling update** strategy with 2
replicas. New pods must pass the readiness probe before old pods are removed, so
traffic only shifts to instances that report all dependencies healthy. This
gives zero-downtime deploys for low-risk changes with no extra tooling.

### Why this is the sensible default here
- The service is stateless, so replicas are interchangeable.
- Readiness-gated rollout already prevents shifting traffic to unhealthy pods.
- Rollback is a single command against an immutable, SHA-pinned previous image.

## Environment promotion

Images are built and pushed **once** per commit, tagged by Git SHA. The same
image is deployed to `dev`, and only after a successful dev rollout is that
**exact image** promoted to `test`. Environments never rebuild, so what passed
in dev is bit-for-bit what runs in test. Differences between environments live
only in configuration (Kustomize overlays), never in the artifact.

## Beyond rolling updates (tradeoffs)

For higher-risk changes, two progressive strategies are worth adopting:

### Blue-green
Run the new version (green) alongside the current version (blue), then switch
the Service selector once green is verified.
- **Pros:** instant cutover, instant rollback (flip selector back), full
  validation before any user traffic.
- **Cons:** ~2x resources during the switch; stateful/migration concerns need
  care; the cutover is all-or-nothing.

### Canary (recommended next step)
Shift a small percentage of traffic to the new version, watch SLIs (error rate,
latency), then ramp up or automatically roll back on regression.
- **Pros:** limits blast radius; enables automated rollback on metric breach;
  real production signal before full rollout.
- **Cons:** needs traffic-splitting (a service mesh like Istio/Linkerd or an
  ingress that supports weighting) and solid metrics + automated analysis
  (e.g. Argo Rollouts or Flagger).

## Recommendation

Keep rolling updates as the default. Introduce **canary with automated
rollback** (via Argo Rollouts or Flagger) for production, gated on the same
SLIs defined for alerting. Reserve blue-green for changes where an instant,
clean cutover matters more than resource efficiency.
