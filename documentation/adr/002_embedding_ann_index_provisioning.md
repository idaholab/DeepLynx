# Embedding ANN Index Strategy and Provisioning

Date: 2026-06-17

## Status

Pending

## Context

Insight (the RAG engine) stores document-chunk vectors in `dl_vector.embeddings`. The MVP
supported a single embedding model, `bge-m3` (1024 dims), and the table was hardcoded to
`vector(1024)` with **no ANN index** — queries were exact KNN over a sequential scan. We now
need to (a) support multiple embedding models of differing dimensionality and (b) replace exact
KNN with approximate nearest-neighbor (ANN) search so retrieval scales to many millions of rows.

Several pgvector (0.8.1) constraints drive the design:

- An HNSW/IVFFlat index requires a **single fixed dimension and a single opclass**. You cannot
  build one ANN index over a column that holds vectors of mixed dimensionality.
- pgvector has **no multicolumn ANN index** — you cannot create `(embedding_model, vector)` so a
  single index "branches" by model. An ANN index covers the vector expression only.
- An **untyped `vector` column cannot be indexed** by HNSW (`column does not have dimensions`).
  The dimension must be re-imposed at index-build time via a cast, e.g. `(vector::vector(1024))`.
- `CREATE INDEX CONCURRENTLY` **cannot run inside a transaction**, and EF Core wraps every
  migration in one. Large HNSW builds are also slow and memory-hungry and should run in a
  maintenance window with an elevated `maintenance_work_mem`.

Two facts about how models reach the system:

- An `AiModelConfig` row (which points a project/org at a model) is created at **runtime** via the
  API. But a genuinely _new embedding model_ also requires standing up a serving backend and
  release configuration — i.e. real model **support is a release-gated decision**, not arbitrary
  user input.
- The embedding **dimension is not stored on `AiModelConfig`**; it is discovered empirically from
  the produced vector's length at insert time.

## Decision

1. **Distance metric: cosine, system-wide** (`<=>` / `vector_cosine_ops`). Chosen for multi-model
   robustness — cosine is scale-invariant, so we do not depend on or enforce per-model
   normalization. The read-path query operator must match this opclass (companion change).
2. **Storage: a single `dl_vector.embeddings` table with a per-model partial HNSW index**, each
   index casting to that model's dimension and predicated on its model, e.g.
   `USING hnsw ((vector::vector(1024)) vector_cosine_ops) WHERE embedding_model = 'bge-m3'`.
   Designed **partition-ready** (read/write paths always filter by `project_id` and
   `embedding_model`); physical partitioning is deferred until build/maintenance cost justifies it.
3. **Model identity = `embedding_model` (name) + `dimensions` (int)** columns. No synthetic hash.
4. **Cheap, transactional DDL stays in EF migrations**: the four metadata columns
   (`organization_id`, `project_id`, `embedding_model`, `dimensions`), the vector-column relax to
   untyped, and the composite btree `(project_id, embedding_model)`. (All delivered in migration
   `20260617200804_AddEmbeddingMetadataColumns`.)
5. **Partial HNSW index DDL is delivered out-of-band**, not as EF migrations — as checked-in,
   idempotent SQL scripts (`deeplynx.datalayer/Scripts/Embeddings/`) executed via the
   [embedding index maintenance runbook](../runbooks/embedding-index-maintenance.md). This is
   because `CREATE INDEX CONCURRENTLY` cannot live in EF's per-migration transaction, and because
   we want operators to control the timing and memory of a heavy build.
6. **New-model index provisioning is a release-gated ops runbook step.** When a release introduces
   support for a new embedding model, building its partial HNSW index is a line item in that
   release's rollout, using the templated script. This is preferred over runtime DDL (from the API
   or the ingestion worker) because it keeps `CREATE` privilege out of the data plane and the heavy
   build under deliberate operator control.
7. **A detection query is the safety net** for the runbook's one weakness (a forgotten step). It
   lists any `embedding_model` present in the data with no valid partial HNSW index. It is intended
   to be wired into a startup/health check in the companion source-code PR.
8. **Legacy-row backfill is J2-owned.** Pre-existing rows carry no model record; they are backfilled
   to `bge-m3` / `1024` (the only model the MVP ran) — an assumption requiring J2 sign-off — and
   org/project are backfilled by join to `deeplynx.records`. Executed post-deploy in a maintenance
   window, not automatically.

## Consequences

- **Correct-but-slow degradation.** A model without its partial index is not broken — its queries
  fall back to exact scan over that model's row subset. Provisioning may therefore lag safely, and
  ANN is a performance optimization rather than a correctness dependency.
- **Schema is not fully reproducible from EF migrations.** The set of partial HNSW indexes is a
  function of which models exist, so it lives in scripts + runbook, not migration history. This is
  inherent to supporting runtime-configurable models, not a defect of this approach. The detection
  query plus the checked-in scripts keep the gap observable and version-controlled.
- **`NOT NULL` enforcement on the four columns is deferred** to a follow-up EF migration _after_
  backfill completes, so the EF model and the database agree (avoiding intentional drift). Until
  then the columns remain nullable.
- **New operational concept.** Adding an embedding model now has a mandatory provisioning step. If
  skipped, retrieval for that model silently degrades to exact scan (caught by the detection query).
- **Alternatives rejected:** untyped column + no ANN (loses ANN); declarative partition-by-model
  (partitions inherit one column type, so it does not escape the mixed-dimension constraint);
  runtime DDL from the API or ingestion worker (widens `CREATE` privilege into the data plane,
  buries a slow build in the hot path, and needs advisory-lock/INVALID-index handling).
