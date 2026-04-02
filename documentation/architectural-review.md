# DeepLynx Nexus — Architectural Review

**Date:** 2026-04-01
**Reviewer:** Claude Code (claude-sonnet-4-6)
**Branch:** develop

---

## Executive Summary

DeepLynx Nexus is a well-structured, multi-tenant knowledge graph platform built on modern, enterprise-grade technology. The layered .NET backend, pluggable storage, and optional AI/RAG pipeline (Insight) reflect thoughtful architecture decisions. Security fundamentals are sound — hashed API keys, token revocation, multi-strategy auth, proper Kubernetes secrets management. Integration test coverage is broad (44 test files, Testcontainers-backed).

The areas that need the most attention before significant scale are: **observability gaps** (no distributed tracing or APM), **missing rate limiting**, **EF Core read performance** (no `.AsNoTracking()`), and **frontend resilience** (no global error boundaries). None of these are architectural rethinks — they are targeted improvements that can be delivered incrementally.

---

## 1. Current Architecture Overview

```
Browser / LLM
     │
     ▼
Next.js 16 (port 3000)          ← MCP Service (port 43656)
     │ REST/axios                        │
     ▼                                   │
ASP.NET Core .NET 10 (port 5000) ◄───────┘
     │ Middleware stack (Auth, Sensitivity, UserContext)
     │
     ▼
Business Layer (38 domain classes)
     │
     ├── EF Core 10 → PostgreSQL 18
     ├── File Storage (Filesystem / Azure Blob / AWS S3)
     ├── Cache (Memory / Redis)
     └── SignalR (real-time notifications)
          │
          ▼ (optional, profile: insight)
     Insight FastAPI (port 5009)
          ├── RabbitMQ (async OCR → chunk → embed pipeline)
          ├── MinIO (object storage)
          └── pgvector (PostgreSQL vector similarity)
```

### Strengths

| Area | Assessment |
|---|---|
| Layer separation | Clean — controllers are thin, all logic in `*Business.cs` |
| Authentication | Multi-strategy (JWT, OKTA, Entra, API keys), token revocation, hashed keys |
| File storage | Strategy pattern + factory — trivially swappable backends |
| Testing | 44 integration test files, Testcontainers, real DB per suite |
| Kubernetes | Resource limits, PersistentVolumes, secrets from K8s Secrets |
| Docker builds | Multi-stage, non-root user, patched base images |
| Database indexes | Filtered unique constraints, composite indexes on FK columns |
| Audit trail | Serilog → PostgreSQL, EventBusiness on every mutation |

---

## 2. Findings & Recommendations

### 2.1 Performance

#### No `.AsNoTracking()` on Read Queries
**Severity: High**

Zero instances of `.AsNoTracking()` exist in the business layer. EF Core tracks every entity returned — maintaining a snapshot for change detection. For read-only queries (GET endpoints, searches, graph traversal), this wastes CPU and memory and slows garbage collection at scale.

**Recommendation:** Add `.AsNoTracking()` (or use `.AsNoTrackingWithIdentityResolution()` for queries with `.Include()`) on all queries that do not need to write back.

```csharp
// Before
var records = await _context.Records
    .Include(r => r.Tags)
    .Where(r => r.ProjectId == projectId)
    .ToListAsync();

// After
var records = await _context.Records
    .AsNoTracking()
    .Include(r => r.Tags)
    .Where(r => r.ProjectId == projectId)
    .ToListAsync();
```

Priority files: `RecordBusiness.cs`, `GraphBusiness.cs`, `QueryBusiness.cs`, `ClassBusiness.cs`.

---

#### In-Memory Filtering After Materialisation
**Severity: Medium**

Several business methods load a collection with `.ToListAsync()` and then apply LINQ filtering in memory. With large datasets this loads far more rows than needed and forces all that data through the network and deserialisation.

**Recommendation:** Push all predicates into the EF query before calling `.ToListAsync()` so the WHERE clause is evaluated in PostgreSQL.

---

#### Transient `DbContext` Lifetime
**Severity: Medium**

`DeeplynxContext` is registered as `Transient` (`deeplynx.api/Program.cs`). A new context instance is created for each injection, bypassing EF's first-level cache and creating more connection pool churn than `Scoped` (one context per HTTP request).

**Recommendation:** Switch to `AddDbContext<DeeplynxContext>(options => ..., ServiceLifetime.Scoped)`. Scoped is the recommended and default lifetime for EF Core in ASP.NET Core.

---

#### Cache Under-Utilisation
**Severity: Medium**

A swappable cache service (`CacheService.cs`) exists and Redis is supported, but permissions, roles, sensitivity labels, and class metadata — all read on almost every request — are not cached. This generates repeated identical DB round-trips.

**Recommendation:** Cache permission lookups and role memberships with short TTLs (30–60 s). Invalidate on write. This alone will dramatically reduce DB load under concurrent users.

---

#### `GraphBusiness` Cartesian Products
**Severity: Medium**

Nested `.ThenInclude()` chains on relationships with large result sets can generate cartesian product SQL. PostgreSQL splits these via multiple queries only when `SplitQuery` is enabled.

**Recommendation:** Evaluate `QuerySplittingBehavior.SplitQuery` globally or per-query on graph traversal paths. Benchmark with representative data volumes.

---

### 2.2 Security

#### No Rate Limiting
**Severity: High**

`Microsoft.AspNetCore.RateLimiting` is referenced in the project but never configured. Auth endpoints (`/api/v1/token`, OAuth handshake), bulk upload, and AI query endpoints are all unprotected against brute force or denial-of-service.

**Recommendation:** Add rate limiting middleware in `Program.cs`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});
```

Apply tighter limits to auth routes; apply a broader policy globally. Use `RedisRateLimitStore` in production for multi-replica correctness.

---

#### Overly Permissive CORS
**Severity: Medium**

`Program.cs` configures `.AllowAnyMethod()`, `.AllowAnyHeader()`, and `.AllowCredentials()` with domain-wildcard origins (`*.inl.gov`, `*.cluster.local`). `AllowAnyMethod` opens `DELETE`, `PUT`, and `PATCH` to any origin matching the wildcard.

**Recommendation:** Restrict to the specific methods and headers required. Enumerate allowed origins explicitly in non-dev environments via config rather than wildcards with `AllowCredentials`.

---

#### `DISABLE_BACKEND_AUTHENTICATION` Flag
**Severity: Medium**

When set to `true`, the authentication middleware auto-creates a SysAdmin session for any request — including malformed ones. If this flag is accidentally deployed to a non-local environment, the application is fully open.

**Recommendation:** Add a compile-time or startup assertion that rejects this flag unless `ASPNETCORE_ENVIRONMENT` is `Development`. Log a loud warning at startup if it is `true`.

```csharp
if (disableAuth && !env.IsDevelopment())
    throw new InvalidOperationException(
        "DISABLE_BACKEND_AUTHENTICATION cannot be true outside Development.");
```

---

#### JWT Secret in `docker-compose.yaml`
**Severity: Low (dev only)**

`docker-compose.yaml` ships with `JWT_SECRET_KEY: demo_ZGDcnDdkRjLcx1kty9lXzlZ9L2ywFv3rC/91I3Z6fLQ=`. It is clearly a placeholder, but a developer who copies this file to a staging server without changing the value would run with a publicly-known secret.

**Recommendation:** Replace the value with a conspicuous placeholder (`JWT_SECRET_KEY: CHANGE_ME_BEFORE_USE`) and add a startup guard that rejects the demo value.

---

### 2.3 Observability

#### No Distributed Tracing or APM
**Severity: High**

There is no OpenTelemetry, Application Insights, Datadog, or equivalent instrumentation. When a slow request is reported in production, there is no way to pinpoint which layer (middleware, business, EF query, Insight pipeline) is responsible. The only signal is Serilog log lines.

**Recommendation:** Integrate OpenTelemetry with a minimal setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

Export to any OTLP-compatible backend (Jaeger, Tempo, Azure Monitor). Even basic span data will dramatically reduce mean time to diagnosis.

---

#### Shallow Health Check
**Severity: Medium**

The `/health` endpoint returns `{ "status": "healthy" }` unconditionally. It does not probe the database, cache, or Redis connection. Kubernetes liveness/readiness probes hitting this endpoint will report a pod healthy even if its DB connection is broken.

**Recommendation:** Upgrade to ASP.NET Core health checks with dependency probes:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddRedis(redisConnectionString, name: "redis");  // conditional

app.MapHealthChecks("/health/ready", new HealthCheckOptions { /* include all */ });
app.MapHealthChecks("/health/live", new HealthCheckOptions { /* exclude slow checks */ });
```

Use `/health/ready` as the Kubernetes readiness probe and `/health/live` as the liveness probe.

---

#### Log Level Too Coarse
**Severity: Low**

`MinimumLevel.Information` filters out `Warning` and `Debug` level entries. In a multi-service system, warning-level events (connection retries, slow query hints) are valuable early signals.

**Recommendation:** Set `MinimumLevel.Warning` for framework namespaces (EF, ASP.NET) and keep `Information` for application namespaces. Use `MinimumLevel.Override` to tune per-namespace.

---

### 2.4 Scalability

#### Single PostgreSQL Instance (Shared by Nexus + Insight)
**Severity: Medium**

Both the core API and the Insight AI pipeline read/write the same PostgreSQL instance. The Insight embedding pipeline (document ingestion bursts) will compete for I/O with API query traffic.

**Recommendation:**
- **Short term:** Ensure Insight workers use a separate connection pool and are governed by a `max_pool_size`.
- **Medium term:** Consider a read replica for Insight's similarity search queries. pgvector scans are expensive and can saturate I/O.
- **Long term:** Evaluate a dedicated PostgreSQL instance for Insight with logical replication of shared tables.

---

#### No Horizontal API Scaling Strategy
**Severity: Medium**

The current in-memory cache (`CacheService`) is per-process. Running multiple API replicas (as Kubernetes allows) means each replica has its own independent cache — invalidation on one replica does not propagate to others.

**Recommendation:**
- Require Redis in multi-replica deployments (`REDIS_CONNECTION_STRING` must be set).
- Document this as a prerequisite in deployment guides.
- SignalR backplane (Redis or Azure SignalR Service) is also required for multi-replica real-time notifications.

---

#### RabbitMQ — No Dead Letter Queue Visible
**Severity: Medium**

The Insight RabbitMQ pipeline processes documents through OCR → chunking → embedding. If any step fails permanently (corrupted file, embedding API timeout), the message behavior is unclear from the configuration visible.

**Recommendation:** Confirm a Dead Letter Exchange (DLX) is configured for each queue. Failed messages should be routed to a DLQ for inspection and retry rather than lost or causing infinite requeue loops.

---

#### No Request Timeout Enforcement in Business Layer
**Severity: Low**

Long-running queries (complex graph traversals, large exports) can occupy a thread indefinitely. There is no `CancellationToken` propagation from the controller's `HttpContext.RequestAborted` into the business layer or EF queries.

**Recommendation:** Pass `cancellationToken` through business method signatures and into `ToListAsync(cancellationToken)`, `SaveChangesAsync(cancellationToken)`, etc. This is a low-cost change that prevents thread exhaustion under slow clients.

---

### 2.5 Frontend

#### No Global Error Boundary
**Severity: Medium**

The Next.js app has no root-level `error.tsx` and no React error boundary component. An unhandled render exception in any page will crash the entire client-side tree and show a blank screen.

**Recommendation:** Add `error.tsx` at the `(home)` layout level and wrap critical sub-trees (graph visualization, AI search) in dedicated error boundaries with fallback UI.

```tsx
// app/(home)/error.tsx
'use client';
export default function Error({ error, reset }) {
  return (
    <div>
      <h2>Something went wrong</h2>
      <button onClick={reset}>Try again</button>
    </div>
  );
}
```

---

#### NextAuth 5 Beta in Production
**Severity: Medium**

`next-auth@5.0.0-beta.30` is a pre-release package. Beta packages may introduce breaking changes between minor versions and are not covered by semantic versioning guarantees.

**Recommendation:** Track the NextAuth 5 stable release and migrate as soon as it ships. Pin the beta version in `package.json` to avoid unintended upgrades (`"next-auth": "5.0.0-beta.30"` not `"^5"`).

---

#### Mixed UI Library Strategy (MUI + Tailwind + DaisyUI)
**Severity: Low**

The UI uses Material-UI 7, TailwindCSS 4, and DaisyUI 5 simultaneously. While workable, this increases bundle size and can lead to style conflicts or inconsistent design language as the team grows.

**Recommendation:** Establish a documented component hierarchy: MUI for complex interactive components (tables, modals, forms), Tailwind/DaisyUI for layout and utility styling. Avoid using both for the same component type.

---

#### No End-to-End Tests
**Severity: Low**

Backend integration tests are comprehensive, but there are no visible E2E tests for the frontend (Playwright, Cypress). Critical user flows — login, graph navigation, file upload, AI search — are untested at the browser level.

**Recommendation:** Add a small Playwright suite covering the 5–10 most critical user paths. This does not need to be comprehensive; it serves as a regression net for deployments.

---

### 2.6 MCP Service

#### Limited Tool Surface
**Severity: Low (informational)**

The MCP service currently exposes only `ProjectTools` and `RecordTools`. As LLM-driven workflows grow, the tool surface will need to expand. The current architecture (thin HTTP wrapper → main API) is the right pattern — keep it that way.

**Recommendation:** Add `EdgeTools`, `QueryTools`, and `FileTools` as the next logical additions. Consider a `SchemaTools` endpoint that lets an LLM introspect available classes and relationships before constructing queries.

---

#### No MCP-Level Rate Limiting
**Severity: Low**

The MCP service inherits whatever rate limiting the main API provides — which is none (see §2.2). LLM tool calls can execute in rapid succession.

**Recommendation:** Apply a per-service-token rate limit at the MCP layer once global rate limiting is implemented.

---

## 3. Prioritised Recommendation Backlog

| Priority | Area | Recommendation |
|---|---|---|
| P0 | Security | Implement rate limiting on auth + API endpoints |
| P0 | Observability | Add OpenTelemetry tracing (OTLP export) |
| P1 | Performance | Add `.AsNoTracking()` to all read-only EF queries |
| P1 | Security | Guard `DISABLE_BACKEND_AUTHENTICATION` against non-dev environments |
| P1 | Observability | Upgrade `/health` to probe DB and Redis dependencies |
| P1 | Scalability | Require Redis for multi-replica deployments; add SignalR backplane |
| P2 | Performance | Switch `DbContext` lifetime from Transient to Scoped |
| P2 | Performance | Cache permission/role lookups with short TTL |
| P2 | Performance | Enable `SplitQuery` on graph traversal queries |
| P2 | Scalability | Confirm RabbitMQ Dead Letter Queue configuration in Insight |
| P2 | Frontend | Add root-level `error.tsx` and React error boundaries |
| P2 | Security | Restrict CORS to specific methods and enumerate allowed origins |
| P3 | Frontend | Migrate off NextAuth 5 beta when stable ships |
| P3 | Performance | Propagate `CancellationToken` through business layer |
| P3 | Testing | Add Playwright E2E suite for critical user flows |
| P3 | MCP | Expand tool surface (EdgeTools, QueryTools, SchemaTools) |
| P3 | Scalability | Evaluate read replica for Insight pgvector queries |

---

## 4. Scaling Roadmap

### Current State → 10× User Load

The primary bottleneck at 10× will be **database connection saturation** and **in-process cache divergence** across replicas.

1. Enable Redis (`REDIS_CONNECTION_STRING` required for K8s deployments).
2. Add Redis SignalR backplane.
3. Switch `DbContext` to Scoped.
4. Apply `.AsNoTracking()` to read paths.
5. Cache permission/role lookups.

### 10× → 100× User Load

At 100×, the knowledge graph query path and Insight vector search become bottlenecks.

1. Add a PostgreSQL read replica; route `QueryBusiness` and `GraphBusiness` reads to it.
2. Move Insight to its own PostgreSQL instance with logical replication of shared tables.
3. Add horizontal API pod scaling (2–4 replicas) behind the existing K8s service.
4. Introduce a CDN/edge cache for static assets and public API responses.
5. Evaluate async job queue (RabbitMQ or Azure Service Bus) for expensive graph analytics operations that currently block HTTP threads.

### 100× → Beyond

At this scale the monolithic `deeplynx.business` layer will need selective decomposition:

1. Extract `FileBusiness` → dedicated file service (separate scaling profile, large memory nodes).
2. Extract Insight into a fully independent service with its own database (it is already architecturally separate; formalise the boundary).
3. Introduce API gateway (NGINX/YARP/Azure API Management) for routing, rate limiting, and auth offload.
4. Evaluate CQRS for the Records/Edges domain — write path through the existing API, read path through a materialised projection optimised for graph queries.

---

## 5. Quick Wins (< 1 day each)

These can be done immediately with low risk:

- **`.AsNoTracking()`** on all GET-path EF queries — pure performance gain, zero behaviour change for reads.
- **`/health` dependency probes** — swap the static response for `AddHealthChecks()` + `AddNpgSql()`.
- **`DISABLE_BACKEND_AUTHENTICATION` guard** — one-line startup assertion.
- **`error.tsx`** in the Next.js app — copy the Next.js docs example, add a "Try again" button.
- **`CancellationToken` propagation** — thread the `HttpContext.RequestAborted` token into business method signatures.

---

## 6. Conclusion

DeepLynx Nexus has a solid architectural foundation. The layering is clean, the security model is thoughtful, and the test suite gives genuine confidence in the data layer. The gaps are primarily operational: observability tooling, rate limiting, and a few EF performance patterns that won't bite until load increases.

The recommended path is: close the P0 items (rate limiting, tracing) before the next production release, then work through P1–P2 in the next two sprints. The scaling roadmap is incremental — no major architectural rewrites are required to reach 10× or even 100× current load.
