# Deployment Improvement Plan

**Date:** 2026-04-02
**Branch:** docs/architecture-diagram
**Source:** architectural-review.md + CI/Kubernetes gap analysis

---

## Overview

Ten targeted improvements across Kubernetes manifests, GitHub Actions workflows, and container startup scripts. No infrastructure changes required. All items are self-contained and can be delivered independently.

Estimated total effort: ~2 sprints.

---

## P0 — Reliability Blockers

These must be resolved before the next production release. Each represents a scenario where a broken deployment goes undetected.

---

### P0-1: Add readiness and liveness probes to all deployments

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** 6 of 7 deployments (UI, API, Docs, MCP, FastAPI, RabbitMQ runner) have no probes. Kubernetes routes traffic to pods before they are ready and never restarts stuck/deadlocked pods. Only `insight-rabbitmq` is correctly configured.

**Implementation:**

Add to each deployment's container spec:

```yaml
readinessProbe:
  httpGet:
    path: /health
    port: <container-port>
  initialDelaySeconds: 15
  periodSeconds: 10
  failureThreshold: 3

livenessProbe:
  httpGet:
    path: /health
    port: <container-port>
  initialDelaySeconds: 30
  periodSeconds: 20
  failureThreshold: 5
```

Port map:
| Deployment | Port |
|---|---|
| deeplynxv3 (UI) | 3000 |
| deeplynxbackend (API) | 5000 |
| deeplynx-docs | 3001 |
| deeplynx-mcp | 43656 |
| insight-fastapi | 5009 |
| insight-rabbitmq-runner | — (use exec probe on process) |

**Note:** Upgrade `/health` to probe database and Redis dependencies (see architectural-review.md §2.3) before or in parallel with this item. A static `{ "status": "healthy" }` response reduces the value of readiness checks.

---

### P0-2: Add resource requests to all deployments

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** 5 of 7 deployments set only `limits`, no `requests`. The Kubernetes scheduler cannot bin-pack without requests — pods may be placed on nodes that cannot sustain their actual load, causing OOM kills and CPU throttling.

**Implementation:**

Add `requests` alongside existing `limits` for UI, API, Docs, FastAPI, and RabbitMQ runner:

```yaml
resources:
  requests:
    memory: 512Mi
    cpu: 250m
  limits:
    memory: 4Gi
    cpu: 1500m
```

Tune requests based on observed baseline usage after first deployment. MCP and RabbitMQ are already correctly configured — do not change them.

---

### P0-3: Post-deploy smoke test and automated rollback in CI

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** The workflow reports success as soon as `kubectl apply` completes. It has no knowledge of whether pods actually came up. A crash-looping deployment produces a green CI run.

**Implementation:**

Add two steps after the `Deploy K8s Workload` step:

```yaml
- name: Wait for rollout
  run: |
    kubectl rollout status deployment/deeplynxbackend -n ${{ vars.K8S_NAMESPACE }} --timeout=5m
    kubectl rollout status deployment/deeplynxv3 -n ${{ vars.K8S_NAMESPACE }} --timeout=5m

- name: Smoke test
  run: |
    BACKEND=$(kubectl get svc deeplynxbackend-service -n ${{ vars.K8S_NAMESPACE }} \
      -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
    curl -sf --retry 5 --retry-delay 5 http://$BACKEND:5000/health

- name: Rollback on failure
  if: failure()
  run: |
    kubectl rollout undo deployment/deeplynxbackend -n ${{ vars.K8S_NAMESPACE }}
    kubectl rollout undo deployment/deeplynxv3 -n ${{ vars.K8S_NAMESPACE }}
```

---

## P1 — Operational Gaps

High-value improvements with low implementation risk. Deliver within the next sprint after P0 items.

---

### P1-1: Parallel image builds

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** 7 Docker images are built sequentially in a single job. They are fully independent and can run concurrently.

**Implementation:**

Split the single `build` job into parallel jobs — one per image (or grouped: `build-core` for UI/API/Docs/MCP, `build-insight` for the 3 Insight images). Each job runs independently; the `kubernetes` deploy job depends on all of them via `needs`.

```yaml
jobs:
  build-core:
    steps:
      - # build UI, server, docs, mcp

  build-insight:
    steps:
      - # build insight-fastapi, insight-rabbitmq, insight-rabbitmq-runner

  kubernetes:
    needs: [build-core, build-insight]
    steps:
      - # deploy
```

Expected impact: 5–10 minute reduction per deploy cycle.

---

### P1-2: Explicit replica counts and rolling update parameters

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** All deployments omit `spec.replicas` (defaulting silently to 1) and do not specify `maxSurge`/`maxUnavailable`. With `maxUnavailable: 1` (the default), a rolling update on a single-replica deployment causes momentary downtime — the old pod is terminated before the new one passes readiness.

**Implementation:**

Add to each deployment spec:

```yaml
spec:
  replicas: 1
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
```

`maxUnavailable: 0` ensures the old pod stays up until the new pod is ready. `maxSurge: 1` allows a temporary second pod during the transition. This is zero-downtime for single-replica deployments.

Also add `progressDeadlineSeconds: 300` to surface hung rollouts quickly:

```yaml
spec:
  progressDeadlineSeconds: 300
```

---

### P1-3: Automated rollback on deploy failure

Covered by P0-3. Ensure rollback targets all affected deployments, not just UI and API. Expand the rollback step to include Docs, MCP, and Insight services as needed per environment.

---

### P1-4: Fix silent failure in `entrypoint.sh`

**File:** `Dockerfiles/server/entrypoint.sh`

**Problem:** Line 23 uses `|| true` when creating the pgvector extension, suppressing all errors. If pgvector fails to install, the application starts with a broken vector search dependency and appears healthy.

**Implementation:**

Remove `|| true`:

```bash
# Before
psql -h "$POSTGRES_HOST" ... -c "CREATE EXTENSION IF NOT EXISTS vector;" || true

# After
psql -h "$POSTGRES_HOST" ... -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

With `set -e` already present at line 2, a failure will exit the script, the container will exit non-zero, and the pod will enter `CrashLoopBackOff` — visible and actionable rather than silent.

---

## P2 — Hardening

Deliver in the sprint following P1. Each item reduces blast radius of future incidents.

---

### P2-1: Automate database migrations via init container

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`, potentially `Dockerfiles/server/Dockerfile.public`

**Problem:** `entrypoint.sh` does not run EF migrations. Migrations are a manual step. A deployment can push application code that depends on a schema that does not yet exist.

**Implementation:**

Add an init container to the `deeplynxbackend` deployment that runs migrations before the application container starts:

```yaml
initContainers:
- name: migrate
  image: ${CI_REGISTRY}/${CI_REGISTRY_PATH}:deeplynxv3-server-${RUN_NUMBER}
  command: ["dotnet", "deeplynx.api.dll", "--migrate"]
  envFrom:
  - secretRef:
      name: app-secrets
  - configMapRef:
      name: app-config
```

This requires the API to support a `--migrate` flag (or equivalent) that runs `dbContext.Database.MigrateAsync()` and exits. The init container completes before any API pod starts, making schema and code deploy atomic.

---

### P2-2: Add PodDisruptionBudgets

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** No PDBs are configured. During node maintenance or cluster upgrades, Kubernetes can evict all pods of a deployment simultaneously, causing full outages.

**Implementation:**

Add a PDB for each user-facing deployment:

```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: deeplynxbackend-pdb
  namespace: deeplynxv3-dev
spec:
  minAvailable: 1
  selector:
    matchLabels:
      app: deeplynxbackend
```

Repeat for UI, Docs, MCP, and FastAPI.

---

### P2-3: Image digest pinning for auditability

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** Images are referenced by mutable run-number tags. `imagePullPolicy: Always` means any tag overwrite silently changes what runs on pod restart. There is no audit trail linking a running pod to a specific image build.

**Implementation:**

Capture the digest from `az acr build` output and reference it in the manifest:

```yaml
- name: Build server image
  id: build-server
  run: |
    DIGEST=$(az acr build ... --query "outputImages[0].digest" -o tsv)
    echo "server-digest=$DIGEST" >> $GITHUB_OUTPUT

# In the deploy step, substitute digest into manifest:
# image: registry.azurecr.us/path@sha256:<digest>
```

This gives a tamper-evident record in the deployment manifest and in `kubectl describe pod` output.

---

## Relationship to `architectural-review.md`

Several items in this plan are blockers or dependencies for findings in the architectural review:

| This Plan | Arch Review Dependency |
|---|---|
| P0-1 (probes) | §2.3 — shallow health check must be upgraded first for probes to be meaningful |
| P0-3 (smoke test) | §2.3 — health check upgrade makes smoke test reliable |
| P1-2 (replica params) | §2.4 — multi-replica scaling requires Redis cache + SignalR backplane before replicas > 1 |
| P2-1 (migrations) | §2.1 — DbContext lifetime fix should land in same release |

---

## Delivery Sequence

```
Sprint 1
├── P0-1  Readiness/liveness probes
├── P0-2  Resource requests
├── P0-3  Smoke test + rollback in CI
└── P1-4  Fix entrypoint.sh || true

Sprint 2
├── P1-1  Parallel image builds
├── P1-2  Explicit replicas + rolling update params
└── P2-2  PodDisruptionBudgets

Sprint 3
├── P2-1  Migration init container
└── P2-3  Image digest pinning
```
