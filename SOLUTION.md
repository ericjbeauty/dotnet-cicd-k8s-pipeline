# SRE Take-Home — Solution

This document explains the delivery pipeline and operational setup built around
the CandidateApi .NET service: how it builds, tests, containerizes, packages,
deploys across environments, and how it would be operated in production.

The original assessment instructions are preserved in README.md.

## What was built

- Containerization: multi-stage Dockerfile (non-root, port 8080)
- CI on pull requests: .github/workflows/ci.yml
- CD on push to main: .github/workflows/cd.yml
- Kubernetes: k8s/ — Kustomize base + dev/test overlays
- NuGet: CandidateApi.Contracts packaged and published in CD
- Incident response: docs/runbook.md
- Deployment strategy: docs/deployment-strategy.md

## Pipeline overview

CI (on pull request): triggered when a PR targets main. Restores, builds
(Release), and runs unit tests against the solution, then builds the Docker
image to catch container-level breakage before merge. The .NET SDK version is
pinned via global.json and consumed by setup-dotnet so CI uses the exact SDK
developers use locally.

CD (on push to main): three jobs chained with needs, so each stage gates the
next.

1. build-test-publish: restore, build, test; dotnet pack the
   CandidateApi.Contracts library into a NuGet package; build the image ONCE and
   push it to GHCR tagged by commit SHA (sha-<12>); publish the NuGet package to
   GHCR.
2. deploy-dev: spin up an ephemeral kind cluster, deploy the exact SHA-pinned
   image to the dev overlay, and block on rollout status (the job fails if pods
   do not become ready).
3. deploy-test: runs only if dev succeeded; promotes the SAME image (no rebuild)
   to the test overlay.

Core principle — build once, promote the artifact: the image is built a single
time per commit and identified by an immutable SHA tag. Dev and test deploy that
identical artifact. Environments differ only in configuration, never in the
binary, so what passes in dev is exactly what runs in test, and rollbacks are
deterministic.

## Kubernetes (Kustomize)

The k8s/ directory contains a base (Deployment, Service, kustomization) and two
overlays (dev, test) that supply only environment-specific configuration
(namespace, region, dependency health, image tag). Both environments are
generated from the same base, which keeps them in lockstep and prevents drift.

Production-hardening in the manifests:
- Health probes wired to the app: liveness to /health/live, readiness to
  /health/ready.
- Resource requests and limits on CPU and memory.
- Security context: non-root user, read-only root filesystem, all Linux
  capabilities dropped, allowPrivilegeEscalation false, seccomp RuntimeDefault.
  A writable emptyDir is mounted at /tmp since the root filesystem is read-only.
- ConfigMap hashing (Kustomize configMapGenerator) so config changes roll pods
  automatically.

Readiness and dependencies: /health/ready reports healthy only when all
configured dependencies (postgres, redis, third-party-billing) are healthy.
Dependency health is configuration-driven, so a failing readiness check can be
simulated per environment by flipping a dependency Healthy flag in the overlay
config.env.

## Observability and SLOs (approach)

- SLI candidates: request error rate (5xx), p99 latency, readiness success.
- Example SLO: 99.9% of requests succeed (non-5xx) over a 30-day window; error
  budget 0.1%. Alert on burn rate (fast burn pages, slow burn files a ticket)
  rather than static thresholds.
- Logs should be structured (JSON) and shipped to an aggregator (Loki /
  Application Insights / equivalent) keyed by service, environment, and trace id.

Alerting detail and severities are documented in docs/runbook.md.

## How to run locally

- dotnet restore
- dotnet build SreTakeHome.sln
- dotnet test SreTakeHome.sln
- dotnet run --project src/CandidateApi/CandidateApi.csproj --urls http://localhost:5000
- docker build -t candidateapi:local .
- docker run -d -p 8080:8080 candidateapi:local
- curl http://localhost:8080/health/ready
- kubectl kustomize k8s/overlays/dev   (render manifests without a cluster)

## Secrets and configuration

- No secrets are hardcoded in workflows or manifests. CD authenticates to GHCR
  with the built-in GITHUB_TOKEN; the workflow grants only packages: write.
- Environment-specific values live in Kustomize overlays. Real secrets would be
  delivered via Kubernetes Secrets sourced from a secret manager (e.g. External
  Secrets Operator backed by Vault or a cloud secret store), not committed.
- GitHub Environments (development, test) are used so approval gates and
  environment secrets/protection rules can be added without code changes.

## Honest tradeoffs and what I would do next

- Ephemeral kind clusters prove the deploy/rollout mechanics end-to-end without
  cloud credentials. A real setup would point kubectl at a managed cluster via a
  kubeconfig stored as an environment secret, and the cluster, registry, and
  networking would be provisioned with IaC (Terraform).
- Progressive delivery: rolling updates are the current default; canary with
  automated rollback (Argo Rollouts / Flagger) is the recommended next step for
  production. See docs/deployment-strategy.md.
- Security scanning (image and dependency scanning, e.g. Trivy) would be added
  as a CI gate.
- Pod Disruption Budgets and Horizontal Pod Autoscaling would be added for
  resilience and autoscaling.
