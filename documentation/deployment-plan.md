# Deployment Improvement Plan

**Date:** 2026-04-02
**Branch:** docs/architecture-diagram
**Source:** architectural-review.md + CI/Kubernetes gap analysis

---

## Overview

Ten targeted improvements across Kubernetes manifests, GitHub Actions workflows, and container startup scripts. No infrastructure changes required. Most items can be delivered independently — see the dependency table at the bottom for items that have ordering constraints.

Estimated total effort: ~2 sprints.

---

## P0 — Reliability Blockers

These must be resolved before the next production release. Each represents a scenario where a broken deployment goes undetected.

---

### P0-1: Add readiness and liveness probes to all deployments

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** 6 of 7 deployments (UI, API, Docs, MCP, FastAPI, RabbitMQ runner) have no probes. Kubernetes routes traffic to pods before they are ready and never restarts stuck/deadlocked pods. Only `insight-rabbitmq` is correctly configured.

**Prerequisites:** Verify each service exposes a `/health` endpoint before implementing. The API (`/health`) exists today but returns a static response — upgrade it to probe dependencies first (see architectural-review.md §2.3) so readiness checks are meaningful.

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
| --- | --- |
| deeplynxv3 (UI) | 3000 |
| deeplynxbackend (API) | 5000 |
| deeplynx-docs | 3001 |
| deeplynx-mcp | 43656 |
| insight-fastapi | 5009 |
| insight-rabbitmq-runner | — (use exec probe on process) |

---

### P0-2: Add resource requests to all deployments

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** 5 of 7 deployments set only `limits`, no `requests`. The Kubernetes scheduler cannot bin-pack without requests — pods may be placed on nodes that cannot sustain their actual load, causing OOM kills and CPU throttling. MCP and RabbitMQ are already correctly configured — do not change them.

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

Tune requests based on observed baseline usage after first deployment. FastAPI and RabbitMQ runner are more resource-intensive than UI/Docs and may need higher initial request values.

---

### P0-3: Post-deploy smoke test and automated rollback in CI

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** The workflow reports success as soon as `kubectl apply` completes. It has no knowledge of whether pods actually came up. A crash-looping deployment produces a green CI run. If the smoke test fails, only the two most recently changed deployments are rolled back — rollback must cover all 7.

**Implementation:**

The services in the K8s manifests are `ClusterIP` — there is no external LoadBalancer IP to query. Use `kubectl exec` to probe the health endpoint directly from within the cluster:

```yaml
- name: Wait for rollout
  run: |
    kubectl rollout status deployment/deeplynxbackend -n deeplynxv3-dev --timeout=5m
    kubectl rollout status deployment/deeplynxv3 -n deeplynxv3-dev --timeout=5m

- name: Smoke test
  run: |
    POD=$(kubectl get pods -n deeplynxv3-dev -l app=deeplynxbackend \
      -o jsonpath='{.items[0].metadata.name}')
    kubectl exec -n deeplynxv3-dev $POD -- \
      curl -sf --retry 5 --retry-delay 3 http://localhost:5000/health

- name: Rollback on failure
  if: failure()
  run: |
    kubectl rollout undo deployment/deeplynxv3 -n deeplynxv3-dev
    kubectl rollout undo deployment/deeplynxbackend -n deeplynxv3-dev
    kubectl rollout undo deployment/deeplynx-docs -n deeplynxv3-dev
    kubectl rollout undo deployment/deeplynx-mcp -n deeplynxv3-dev
    kubectl rollout undo deployment/insight-fastapi -n deeplynxv3-dev
    kubectl rollout undo deployment/insight-rabbitmq -n deeplynxv3-dev
    kubectl rollout undo deployment/insight-rabbitmq-runner -n deeplynxv3-dev
```

---

## P1 — Operational Gaps

High-value improvements with low implementation risk. Deliver within the next sprint after P0 items.

---

### P1-1: Parallel image builds

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** 7 Docker images are built sequentially in a single job. They are fully independent and can run concurrently.

**Implementation:**

Split the single `build` job into two parallel jobs — `build-core` (UI, server, docs, MCP) and `build-insight` (FastAPI, RabbitMQ, RabbitMQ runner). The `kubernetes` deploy job depends on both via `needs`. Grouping rather than 7 individual jobs reduces risk of resource contention on the self-hosted runner and ACR concurrent upload limits.

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

Expected impact: 5–10 minute reduction per deploy cycle. If the self-hosted runner has sufficient capacity, individual jobs per image can be evaluated after validating this grouping.

---

### P1-2: Explicit replica counts and rolling update parameters

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** All deployments omit `spec.replicas` (defaulting silently to 1) and do not specify `maxSurge`/`maxUnavailable`. With `maxUnavailable: 1` (the default), a rolling update on a single-replica deployment causes momentary downtime — the old pod is terminated before the new one passes readiness.

**Implementation:**

Add to each deployment spec:

```yaml
spec:
  replicas: 1
  progressDeadlineSeconds: 300
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
```

`maxUnavailable: 0` ensures the old pod stays up until the new pod is ready. `maxSurge: 1` allows a temporary second pod during the transition. `progressDeadlineSeconds: 300` surfaces hung rollouts as a failure within 5 minutes rather than waiting indefinitely.

**Note:** Running replicas > 1 requires Redis for the in-process cache and a SignalR backplane for real-time notifications. Do not increase replica counts above 1 until those are in place (see architectural-review.md §2.4).

---

### P1-3: Fix silent failure in `entrypoint.sh`

**File:** `Dockerfiles/server/entrypoint.sh`

**Problem:** Line 23 uses `|| true` when creating the pgvector extension, suppressing all errors. If pgvector fails to install, the application starts with a broken vector search dependency and appears healthy.

**Implementation:**

Remove `|| true` from line 23:

```bash
# Before
psql -h "$POSTGRES_DB_HOST" -U "$POSTGRES_USER" -d deeplynx -c "CREATE EXTENSION IF NOT EXISTS vector;" || true

# After
psql -h "$POSTGRES_DB_HOST" -U "$POSTGRES_USER" -d deeplynx -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

With `set -e` already present at line 2, a failure exits the script, the container exits non-zero, and the pod enters `CrashLoopBackOff` — visible and actionable rather than silent.

---

## P2 — Hardening

Deliver in the sprint following P1. Each item reduces blast radius of future incidents.

---

### P2-1: Automate database migrations

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`, `deeplynx.api/Program.cs`

**Problem:** `entrypoint.sh` does not run EF migrations. Migrations are a manual step. A deployment can push application code that depends on a schema that does not yet exist.

**Implementation:**

Use a Kubernetes Job (not an init container) so migrations run once per deploy rather than on every pod restart or replica scale-up:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: db-migrate-${RUN_NUMBER}
  namespace: deeplynxv3-dev
spec:
  template:
    spec:
      containers:
      - name: migrate
        image: ${CI_REGISTRY}/${CI_REGISTRY_PATH}:deeplynxv3-server-${RUN_NUMBER}
        command: ["dotnet", "deeplynx.api.dll", "--migrate"]
        envFrom:
        - secretRef:
            name: app-secrets
        - configMapRef:
            name: app-config
      restartPolicy: Never
  backoffLimit: 1
```

In CI, apply the Job and wait for completion before deploying the main workload:

```yaml
- name: Run migrations
  run: |
    kubectl apply -f kubernetes/migrate-job.yaml -n deeplynxv3-dev
    kubectl wait --for=condition=complete job/db-migrate-${GITHUB_RUN_NUMBER} \
      -n deeplynxv3-dev --timeout=5m
```

**Prerequisites:**

- The API must support a `--migrate` flag that calls `dbContext.Database.MigrateAsync()` and exits. This requires a code change in `deeplynx.api/Program.cs` before this item can be implemented.
- If the migration Job fails, the main deployment will not proceed — investigate and fix the migration before retrying. Do not use `backoffLimit > 1` with non-idempotent migrations.

---

### P2-2: Add PodDisruptionBudgets

**Files:** `kubernetes/development.yaml`, `kubernetes/sandbox.yaml`

**Problem:** No PDBs are configured. During node maintenance or cluster upgrades, Kubernetes can evict all pods of a deployment simultaneously, causing full outages.

**Implementation:**

Add a PDB for each user-facing deployment. Example for the API:

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

### P2-3: Confirm image tag immutability

**Files:** `.github/workflows/development.yaml`, `.github/workflows/sandbox-test.yaml`

**Problem:** Images are referenced by run-number tags (e.g., `deeplynxv3-ui-$GITHUB_RUN_NUMBER`). If ACR tag overwrites are not disabled, `imagePullPolicy: Always` means a tag overwrite silently changes what runs on pod restart.

**Implementation:**

ACR disables tag overwrites by default. Verify this is enforced on the registry:

```bash
az acr config content-trust show --name <ACR_NAME>
az acr update --name <ACR_NAME> --allow-trusted-services false  # if not already locked down
```

If compliance requires cryptographic proof of what ran, capture the digest after build:

```bash
DIGEST=$(az acr repository show \
  --name $ACR_NAME \
  --image ${CI_REGISTRY_PATH}:deeplynxv3-server-${GITHUB_RUN_NUMBER} \
  --query "digest" -o tsv)
echo "server-digest=$DIGEST" >> $GITHUB_OUTPUT
```

Then reference the digest in the manifest alongside the tag for auditability.

---

## Relationship to `architectural-review.md`

Several items in this plan have ordering dependencies with findings in the architectural review:

| This Plan | Dependency |
| --- | --- |
| P0-1 (probes) | Upgrade `/health` to probe DB + Redis first (arch review §2.3) — static health response makes readiness probes unreliable |
| P0-3 (smoke test) | Same — health check upgrade makes smoke test signal trustworthy |
| P1-2 (replica params) | Do not increase replicas > 1 until Redis cache + SignalR backplane are in place (arch review §2.4) |
| P2-1 (migrations) | `--migrate` flag requires a code change in `deeplynx.api`; DbContext lifetime fix (arch review §2.1) should land in the same release |

---

## Section 2: Local Deployment

Findings from reviewing `docker-compose.yaml`, `Dockerfiles/server/entrypoint.sh`, `Dockerfiles/server/Dockerfile.local`, `Dockerfiles/ui/Dockerfile.local`, and `Dockerfiles/database/check_db_version.sh`.

---

## L-P0 — Critical

### L-P0-1: Server final image is the nightly SDK, not a runtime image

**File:** `Dockerfiles/server/Dockerfile.local` line 49

**Problem:** The final stage uses `mcr.microsoft.com/dotnet/nightly/sdk:10.0`. The nightly SDK is ~3× larger than the runtime image, includes build tooling that has no place in a runtime container, and is not a stable release channel.

**Implementation:**

```dockerfile
# Before
FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0 AS final

# After
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
```

The `postgresql-client` install in the final stage remains valid — `apt-get` is available on the aspnet image (Debian-based).

---

### L-P0-2: No health check on `server` — `ui`, `docs`, and `mcp` start before the API is ready

**File:** `docker-compose.yaml`

**Problem:** `nx-postgres` and `insight-rabbitmq` have health checks. `server` does not. `ui`, `docs`, and `mcp` use `depends_on: server` without a condition, so Compose starts them the moment the server container launches — not when the API is ready to accept requests. Requests during the startup window fail silently.

**Implementation:**

Add a health check to the `server` service:

```yaml
server:
  healthcheck:
    test: ["CMD-SHELL", "curl -sf http://localhost:5000/health || exit 1"]
    interval: 10s
    timeout: 5s
    retries: 10
    start_period: 30s
```

Update `ui`, `docs`, and `mcp` to wait for the server to be healthy:

```yaml
ui:
  depends_on:
    server:
      condition: service_healthy

docs:
  depends_on:
    server:
      condition: service_healthy

mcp:
  depends_on:
    server:
      condition: service_healthy
```

**Note:** The `curl` binary must be present in the server image. It is not installed by default in `mcr.microsoft.com/dotnet/aspnet`. Add it alongside `postgresql-client`:

```dockerfile
RUN apt-get update && apt-get install -y \
    postgresql-client \
    curl \
    && apt-get clean
```

---

## L-P1 — Operational Gaps

### L-P1-1: Core service config is hardcoded in `docker-compose.yaml`

**File:** `docker-compose.yaml`

**Problem:** `server`, `ui`, `docs`, and `mcp` have all configuration inline as `environment:` blocks. Insight services correctly use `env_file:`. Developers cannot customize values (database password, email addresses, JWT secret, auth flags) without editing the compose file, which risks accidental commits of personal config.

**Implementation:**

Add an `env_file` reference to each core service pointing at the project `.env` (which is `.gitignore`d):

```yaml
server:
  env_file:
    - .env
  environment:           # keep only non-secret, non-variable defaults here
    FILE_STORAGE_METHOD: filesystem
    STORAGE_DIRECTORY: /data
    ...
```

Move all variable or sensitive values (`POSTGRES_PASSWORD`, `JWT_SECRET_KEY`, `SUPERUSER_EMAIL`, `DISABLE_BACKEND_AUTHENTICATION`) into `.env_sample` with placeholder values, and document that the developer must copy to `.env` and fill in. Non-sensitive defaults that should work out of the box can remain as inline `environment:` entries.

---

### L-P1-2: `SUPERUSER_EMAIL` hardcoded to a specific developer's address

**File:** `docker-compose.yaml` line 80

**Problem:** `SUPERUSER_EMAIL: jaren.brownlee@inl.gov` is hardcoded. Every developer who runs `docker compose up` creates a SysAdmin account for that address. Any system emails triggered during local development are directed to a real person.

**Implementation:** Move `SUPERUSER_EMAIL` to `.env_sample` with a placeholder value (`SUPERUSER_EMAIL=admin@example.com`). Remove it from the hardcoded `environment:` block in the compose file (covered by L-P1-1).

---

### L-P1-3: Duplicate and redundant database/pgvector setup across startup scripts

**Files:** `docker-compose.yaml`, `Dockerfiles/server/entrypoint.sh`, `Dockerfiles/database/check_db_version.sh`

**Problem:** Three overlapping mechanisms attempt to set up the same resources:

1. The official `postgres` image with `POSTGRES_DB=deeplynx` already creates the `deeplynx` database during container init.
2. `entrypoint.sh` lines 12–19 try to create `deeplynx` again — this is dead code for all fresh installs.
3. `check_db_version.sh` lines 46–54 attempt to install pgvector (suppressing failure with `> /dev/null 2>&1`, then setting `NEEDS_UPGRADE=true`).
4. `entrypoint.sh` line 23 attempts pgvector install again with `|| true`, hiding any remaining failure.

**Implementation:**

- Remove the database existence check and creation block from `entrypoint.sh` (lines 11–19). The postgres image handles this.
- Consolidate pgvector install to `check_db_version.sh` only, and remove `|| true`/output suppression so failures are visible.
- Remove the duplicate pgvector install from `entrypoint.sh` line 23 entirely (the version check job runs first and either succeeds or exits non-zero, blocking `server` from starting via the `depends_on` chain).

This is related to P1-3 in Section 1 (the `|| true` fix) — both should land in the same change.

---

## L-P2 — Hardening

### L-P2-1: INL cert fetch duplicated across Dockerfile build and final stages

**Files:** `Dockerfiles/server/Dockerfile.local`, `Dockerfiles/ui/Dockerfile.local`

**Problem:** Both Dockerfiles fetch the INL CA cert (`wget certstore.inl.gov/...`) in the build stage and then again in the final stage. The final stage does not inherit the build stage's cert store, so the second fetch is necessary — but the first one is not, since the build stage uses the cert only for subsequent `RUN` commands (dotnet restore, npm install).

**Implementation:** For `Dockerfile.local` files that run inside an INL network environment, the cert fetch in the build stage is genuinely needed (to reach package registries). Leave it. For `Dockerfile.public` variants intended for external use, evaluate whether the cert fetch should be removed entirely or replaced with a documented substitution step.

No immediate code change needed; this is an awareness item.

---

### L-P2-2: `minimatch` patch in UI Dockerfile is fragile

**File:** `Dockerfiles/ui/Dockerfile.local` lines 78–86

**Problem:** The final stage performs an inline npm package surgery to patch `minimatch`, ending with `|| true`. If the patched path doesn't exist or the pack fails, the patch silently does nothing — which defeats the purpose of patching a vulnerable dependency.

**Implementation:**

Determine whether the `minimatch` vulnerability is still present in the current `node:lts-alpine` base image. If it is, pin the base image to a version where the vulnerability is resolved and remove the patch script. If the vulnerability is already fixed upstream, remove the patch entirely.

```bash
# Check current minimatch version in a fresh node:lts-alpine container
docker run --rm node:lts-alpine node -e "console.log(require('/usr/local/lib/node_modules/npm/node_modules/minimatch/package.json').version)"
```

---

### L-P2-3: Investigate `moon.css` copied into server image

**File:** `Dockerfiles/server/Dockerfile.local` line 62

**Problem:** `COPY deeplynx.api/moon.css .` places a CSS file in the .NET publish output directory alongside the application DLLs. The ASP.NET Core runtime does not serve this file. If it is a static asset, it belongs in the UI build, not the server image.

**Implementation:** Determine what `moon.css` is used for. If it is loaded at runtime by the .NET application (e.g., for Scalar API docs theming), the copy is intentional and should be documented. If it is unused, remove the `COPY` line.

---

## Cross-Section Dependencies

| This Plan | Dependency |
| --- | --- |
| P0-1 (probes) | Upgrade `/health` to probe DB + Redis first (arch review §2.3) — static health response makes readiness probes unreliable |
| P0-3 (smoke test) | Same — health check upgrade makes smoke test signal trustworthy |
| P1-2 (replica params) | Do not increase replicas > 1 until Redis cache + SignalR backplane are in place (arch review §2.4) |
| P2-1 (migrations) | `--migrate` flag requires a code change in `deeplynx.api`; DbContext lifetime fix (arch review §2.1) should land in the same release |
| L-P0-2 (server health check) | Requires `curl` added to server image (covered in L-P0-1 implementation) |
| L-P1-3 (pgvector consolidation) | Aligns with P1-3 (`|| true` removal) — deliver together |

---

## Delivery Sequence

```text
Sprint 1
├── L-P0-1  Replace nightly SDK final image with aspnet runtime
├── L-P0-2  Add server health check; update ui/docs/mcp depends_on
├── P0-1    Readiness/liveness probes (after /health upgrade)
├── P0-2    Resource requests
├── P0-3    Smoke test + rollback in CI
└── P1-3 + L-P1-3  Fix entrypoint.sh || true; consolidate pgvector setup

Sprint 2
├── L-P1-1  Move core service config to env_file
├── L-P1-2  Remove hardcoded SUPERUSER_EMAIL
├── P1-1    Parallel image builds
├── P1-2    Explicit replicas + rolling update params
└── P2-2    PodDisruptionBudgets

Sprint 3
├── L-P2-2  Resolve or remove minimatch patch
├── L-P2-3  Investigate moon.css
├── P2-1    Migration Job (requires --migrate flag in API)
└── P2-3    Confirm ACR tag immutability
```
