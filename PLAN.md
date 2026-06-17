# Insight Embedding Table Work Plan

Insight is a retrieval-augmented generator that embeds vector chunks based on documents uploaded into the Nexus warehouse.

## Human Instruction and Analysis

This section is human instruction that should guide Claude in approaching this problem and development. If you are an agent, do not touch this section please.

### Source Code Overview

1. `deeplynx.insight`: The retrieval augmented generator source code
2. `deeplynx.datalayer`: The models used for PostgreSQL tables, including `Embedding.cs`, which defines the vector table that holds embeddings
3. `deeplynx.api`: The core warehouse API

### Background

The Insight minimum viable product only supported a single embedding model, `bge-m3`, which produces vectors with 1024 dimensions. However, we'd like to support other embedding models, which may or may not create vectors with different dimensions. In theory we'll need a column that uniquely identifies an embedding model used to create records in the `deeplynx.dl_vector` schema's `embeddings` table, because different models don't understand each other's embeddings.

### Plan

The following deliverables are sourced from a Jira ticket assigning this work.

1. Partition embeddings by project/org — the flat dl_vector.embeddings table needs project/org-level separation for storage, indexing, maintenance, and cleanup.
2. Replace exact KNN with approximate nearest-neighbor (ANN) search — exact KNN doesn't scale; needs an indexed ANN approach (e.g. HNSW or IVFFlat via pgvector) to keep query latency manageable as data grows.
3. Support multiple embedding models and vector sizes at the DB level — the schema is hardcoded to vector(1024); it needs to accommodate different models with different dimensionalities per project.
4. Define and implement a cross-project search strategy — either restrict cross-project search to projects sharing the same model, or implement a per-space search + result merging (late fusion) approach.
5. Write migration files to backfill existing data — existing rows need project metadata backfilled (via join on record_id), and J2 needs to be looped in as the owner of that process.

### Notes

First categorize work into distinct buckets. For example, work involving the database schema, versus work involving the Nexus API, etc. Initially we will only focus on the Postgres schema, data, etc.

## Claude Analysis and Plan

### How the system works today (ground truth from the code)

**The table.** `dl_vector.embeddings` is created by raw SQL in two migrations — `20260209221659_PgVector` (original) and `20260408185624_TrackEmbeddings` (idempotent re-declare with `IF NOT EXISTS`). Schema:

```
id              BIGINT identity PK
record_id       BIGINT NOT NULL  FK -> deeplynx.records(id) ON DELETE CASCADE
page_number     INTEGER NOT NULL
text_chunk      TEXT NOT NULL
vector          vector(1024) NOT NULL     -- hardcoded dimension
last_updated_at TIMESTAMP
```

Indexes: `idx_embeddings_id` (btree on id) and `idx_embeddings_record_id` (btree on record_id). **There is no vector index** — so search is exact KNN over a sequential scan. There is **no column identifying the model** that produced a row, and **no project/org column**. The EF model `deeplynx.datalayer/Models/Embedding.cs` mirrors this and maps `vector` as a plain `string` (not the `Pgvector` type, which the `OntologyVector` model does use — minor inconsistency).

**The infra is ahead of the schema.** `Dockerfiles/database/Dockerfile.local` builds **pgvector v0.8.1** on **PostgreSQL 18** (enforced by `DatabaseVersionChecker`). That means HNSW, IVFFlat, `halfvec`, partial indexes, and 0.8's iterative index scans are all available to us today. None of it is used yet.

**The read path** (`deeplynx.insight/app/fastapi/api/routes/query.py`):

```sql
SELECT record_id, page_number, text_chunk
FROM dl_vector.embeddings
WHERE record_id = ANY(%s)          -- always pre-filtered to caller's file_ids
ORDER BY vector <-> %s::vector      -- exact KNN, L2 distance operator
LIMIT %s
```

**The write path** (`deeplynx.insight/app/rabbitmq/workers/text_to_chunks.py`):

```sql
INSERT INTO dl_vector.embeddings (record_id, page_number, text_chunk, vector)
VALUES (%s, %s, %s, %s)
```

**Model selection already exists — just not persisted.** The API routes are project-scoped (`/organizations/{orgId}/projects/{projectId}/insight/...`). `InsightBusiness.ResolveModelConfig` resolves an `AiModelConfig` (project default → org default) and threads `EmbeddingServerUrl` / `EmbeddingModelName` / token into both the upload pipeline and the query request. So Nexus _can already_ route different projects to different embedding models per request. What's missing is (a) the DB recording which model wrote each row, and (b) a vector column/index that tolerates more than one dimension.

**Project/org context already flows in — then gets dropped.** `upload.py::get_record_and_datasource` joins `records` and already selects `organization_id` and `project_id`, and `PendingFileMessage` carries them. But `FileMessage` / `ImageMessage` / `TextMessage` (the chain that reaches the worker doing the INSERT) do **not** carry org/project, so by the time we insert we only have `record_id`. They are recoverable from `record_id` via the records table.

**A second vector table exists:** `dl_vector.ontology_vector` (class*id / relationship_id / `vector`). Note it uses an \_untyped* `vector` column (no fixed dimension, no index) — so it already faces, and "solves," the multi-dimension problem the wrong way (by being unindexable). Worth deciding whether it's in scope.

### Feasibility of each deliverable

**1. Partition by project/org — feasible, low risk.** Recommended first step is _denormalized columns_ (`organization_id`, `project_id`) plus composite indexes, not Postgres declarative partitioning. Reasons: (a) the values are derivable by `JOIN deeplynx.records`; (b) declarative partitioning forces the partition key into the primary key, complicates the `ON DELETE CASCADE` FK to `records`, and requires a separate vector index _per partition_; (c) the query already filters by record set and would simply add `AND project_id = …`. True physical partitioning should be deferred until row counts justify it. **Open question below.**

**2. Exact KNN → ANN — feasible, but it interacts with #1 and #3.** Two caveats the ticket doesn't mention:

- **Filter interaction.** ANN indexes order the _whole_ column; our query pre-filters with `WHERE record_id = ANY(...)`. pgvector does _post_-filtering, so a tight record filter can blow past the index's `ef_search`/`nprobe` candidate set and silently drop results. pgvector 0.8's `hnsw.iterative_scan = relaxed_order` mitigates this. The payoff from ANN is largest when we search a _whole project/org partition_; it's marginal (or harmful) when the query is already scoped to a few files. So the value of #2 depends on the search granularity #1/#4 settle on.
- **Operator/opclass must match.** The index opclass has to match the query operator. Today the query uses `<->` (L2, `vector_l2_ops`). bge-m3 vectors are normalized, where cosine (`<=>`, `vector_cosine_ops`) is conventional. Changing the metric changes results, so this needs a deliberate decision, not a silent default.

**3. Multiple models / dimensions — this is the crux, and it conflicts head-on with #2.** A pgvector ANN index requires a _single fixed dimension and opclass_; you cannot build one HNSW/IVFFlat index over a column holding mixed dimensions. The three viable shapes:

- **(a) Untyped `vector` column + model id column, no ANN.** Simplest schema, but you lose ANN entirely (this is what `ontology_vector` does). Contradicts #2. Reject.
- **(b) Partial indexes per model, single table.** Add `embedding_model` (and `dimensions`) columns, keep one `vector` column, and create one partial HNSW index per model, e.g. `CREATE INDEX … USING hnsw ((vector::vector(1024)) vector_cosine_ops) WHERE embedding_model = 'bge-m3'`. Every query already knows the project's model (`embeddingConfig.ModelName`), so it adds `WHERE embedding_model = …` and hits exactly one partial index. **This is my recommendation** — it satisfies #1, #2, and #3 together with the least structural disruption.
- **(c) Declarative partition by model (LIST).** Cleaner isolation, but heavier: partition key in PK, per-partition indexes, FK caveats. Defer unless (b) proves insufficient.
- **Identity question:** what uniquely identifies "embeddings that are mutually comparable"? Not the `AiModelConfig` row (two configs can point at the same model). The natural key is **model name + dimension** (provider optional). The model name string is already the value threaded through the pipeline, so it's the pragmatic choice.

**4. Cross-project search — partly schema, mostly API; currently not possible at all.** The API route is single-project and `FilterAuthorizedRecordIds` scopes file*ids to that project, so cross-project search is a \_new* capability, not a modification. Two regimes:

- **Same model across projects:** trivial once #1/#3 land — one ANN query with `WHERE project_id = ANY(...) AND embedding_model = X`. Scores are comparable.
- **Different models:** fundamentally requires re-embedding the query once per model and merging — _late fusion_. Raw distances from different models/metrics are **not** comparable, so merge by rank (Reciprocal Rank Fusion), not by score. Recommend shipping the same-model union first and treating late fusion as a later phase. Schema-wise both regimes just need the `project_id` + `embedding_model` columns and indexes from #1/#3.

**5. Backfill migration — feasible, with one unavoidable assumption.**

- `organization_id` / `project_id`: clean `UPDATE … FROM deeplynx.records WHERE embeddings.record_id = records.id`.
- `embedding_model`: **existing rows carry no record of their model.** Since the MVP only ever ran bge-m3 @ 1024, it is safe to backfill all existing rows to `'bge-m3'` / `1024` — but this is an assumption that must be stated and signed off (this is the J2-owned piece).
- **Operational ordering:** add nullable columns → backfill → `SET NOT NULL` → build indexes. HNSW builds on a large table are slow and memory-hungry and lock the table; production should use `CREATE INDEX CONCURRENTLY`, which **cannot run inside a transaction** — and EF Core wraps each migration in one. So the index build likely needs a `migrationBuilder.Sql` with the suppress-transaction flag, or an out-of-band script. Flag for review.

### Cross-cutting issues to resolve before coding

1. **The write path must persist the new columns.** Either (preferred) extend `FileMessage`/`ImageMessage`/`TextMessage` to carry `project_id`/`organization_id`/`embedding_model` (org/project already sit on `PendingFileMessage` upstream — just propagate them), or have the worker re-query `records` by `record_id` at insert time. The model name is already on the message. `ontology_to_vectors` has no record_id but its `class_id`/`relationship_id` join to `classes`/`relationships`, which carry `project_id`.
2. **Dual-managed schema.** The table lives in hand-written SQL (`PgVector` + `TrackEmbeddings`) _and_ the EF model/snapshot. New columns must be added in both, or EF will try to "fix" drift. Keep using raw SQL migrations for the vector bits.
3. **Distance metric** (L2 vs cosine) must be settled because index opclass and query operator have to agree.
4. **`ontology_vector` scope** — same multi-model problem, different table; in or out?

### Suggested work breakdown (Postgres-first, per the Notes)

- **Phase 0 — schema:** migration adding `organization_id`, `project_id`, `embedding_model`, `dimensions` (nullable); update `Embedding.cs` + snapshot.
- **Phase 1 — backfill:** `UPDATE…FROM records` for org/project; constant backfill of model/dim for legacy rows (J2 sign-off); then `SET NOT NULL`.
- **Phase 2 — indexes:** per-model partial HNSW (opclass per the metric decision), built `CONCURRENTLY`; composite btree on `(project_id, embedding_model)`.
- **Phase 3 — write path:** propagate project/org/model to the INSERT.
- **Phase 4 — read path:** query adds `project_id` + `embedding_model` predicates; enable iterative scan; tune `ef_search`.
- **Phase 5 — cross-project (API/Insight):** same-model union first; late fusion (RRF) later.

### Clarifying questions

1. **Partitioning depth:** are denormalized columns + indexes acceptable, or is true Postgres declarative partitioning a hard requirement for this ticket?
2. **Cross-project scope now:** ship same-model union only, or is heterogeneous-model late fusion required in this round?
3. **Distance metric:** keep L2 (`<->`) or move to cosine (`<=>`) for the (normalized) bge-m3 vectors? This locks the index opclass.
4. **Model identity key:** is the model _name_ (+ dimension) sufficient as the uniqueness column, or do you want it tied to `AiModelConfig`?
5. **`ontology_vector`:** in scope for the same treatment, or embeddings-only for now?
6. **Expected scale:** rough current/projected row counts per project — this decides whether ANN and physical partitioning are warranted yet.

#### Human Answers:

1. I think a system with proper indexes should be okay, but the production table is going to have millions of records in the near future, so we could go either way. It depends on what scale Postgres can handle quick performance in the RAG pipeline. The partitioning or sharding question in my opinion is not well informed, so it could be wrong that we need physical partitioning.
2. We should drop cross-project scope, I don't think that's a real requirement. We do need the project and org fields on the embeddings table either way, but ignore that comment.
3. I'm not sure what the differences are. Please create a brief write-up to pose this question and its pros/cons to the AI engineers.
4. I'm not sure how models should be uniquely identified. Do models typically have a hash, indicating their uniqueness, that we could leverage?
5. Out of scope, ignore
6. We want to handle many millions of records

#### Claude Responses to Human Answers

##### Re #1 + #6 — Scale, indexing, and whether we need physical partitioning

Short version: **at "many millions of rows" you do not need declarative partitioning for query performance** — pgvector HNSW comfortably serves single tables in the tens of millions of vectors. Partitioning is a _maintenance/operability_ tool here, not a _latency_ tool. So I'd separate the two concerns the ticket conflated:

- **For RAG latency:** a single `dl_vector.embeddings` table with a **per-model partial HNSW index** is the right first build. HNSW gives roughly log-time candidate lookup; millions of rows is well inside its envelope.
- **For storage/maintenance/cleanup** (the actual reasons the ticket lists): the `project_id`/`org_id` _columns_ + a composite btree already buy you fast project-scoped deletes and filtered queries. The FK is already `ON DELETE CASCADE`, so project deletion already cleans embeddings.

Where physical partitioning _does_ start to earn its keep at millions+:

- **Index build cost.** Building one HNSW index over, say, 20M × 1024-dim vectors needs a large `maintenance_work_mem` (many GB) and a long, memory-heavy build. Partitioning by project (or hash) shards that into smaller, independently-buildable, parallelizable indexes.
- **Bloat / VACUUM / rebuild isolation.** Per-partition maintenance is cheaper and doesn't touch the whole dataset.
- **Cleanup.** Dropping a project becomes `DROP PARTITION` instead of a mass `DELETE` + index churn.

**Recommendation:** build single-table first (columns + per-model partial HNSW), but write the read/write predicates so they're _partition-ready_ (always filter by `project_id` and `embedding_model`). Then partitioning can be introduced later with **zero application change**. If/when you pull the trigger, I'd lean **HASH partitioning on `project_id`** (bounded partition count, even distribution) over LIST/by-project (unbounded, skewed). The one schema cost to accept up front: a partitioned table's primary key must include the partition key, so the PK becomes `(id, project_id)` instead of `(id)`. Designing for that now is cheap; retrofitting it later is not.

Net: indexes-first is the correct call for your stated scale. Treat partitioning as a planned follow-up triggered by _build/maintenance_ pain, not query latency.

##### Re #2 — Cross-project search dropped

Acknowledged. Deliverable #4 is out. We still add `project_id` and `organization_id` to the table (needed for partitioning/cleanup/filtering regardless). This simplifies the read path considerably — every query stays within a single project, so there's no late-fusion / rank-merging / score-normalization work at all. I'll drop the cross-project phase from the breakdown.

##### Re #5 — `ontology_vector` out of scope

Acknowledged. Embeddings-only.

##### Re #3 — Distance metric: a write-up to pose to the AI engineers

> **Question for the AI/ML engineers — which similarity metric should Insight retrieval use, and are our embedding vectors normalized?**
>
> pgvector exposes three distance operators, each needing a matching index opclass. The index and the query operator **must agree**, and the choice affects retrieval quality, so we want your call before we build the ANN index.
>
> | Metric         | Operator | Opclass             | What it measures                               |
> | -------------- | -------- | ------------------- | ---------------------------------------------- |
> | Cosine         | `<=>`    | `vector_cosine_ops` | Angle between vectors; ignores magnitude       |
> | L2 / Euclidean | `<->`    | `vector_l2_ops`     | Straight-line distance; sensitive to magnitude |
> | Inner product  | `<#>`    | `vector_ip_ops`     | Dot product; fastest to compute                |
>
> **The crux — normalization.** If embedding vectors are **L2-normalized to unit length**, then cosine, inner product, and L2 all produce the **same ranking**, and the decision is purely about compute cost (inner product is cheapest). If vectors are **not** normalized, cosine and L2 give materially different results, because L2 lets vector _magnitude_ (e.g. text length / token count effects) influence ranking — usually undesirable for semantic search.
>
> **What we currently do:** the live query uses `<->` (L2). bge-m3 is conventionally used with **cosine** on normalized vectors, so the current L2 choice may be unintentional — if our embedding server does _not_ normalize, L2 is likely the wrong metric.
>
> **What we need from you:**
>
> 1. Does our embedding service (bge-m3 today, others later) return **normalized** vectors? If you're not sure, we can add a one-line normalization step at insert/query time to remove the ambiguity.
> 2. Which metric do you want retrieval to optimize for? (Default recommendation for text embeddings: **cosine**, or **inner product on explicitly-normalized vectors** if we care about query speed at scale.)
> 3. Should the metric be **fixed system-wide**, or **per-model** (different models may prefer different metrics)? This determines whether the metric becomes another per-model attribute on the embeddings table.

##### Re #4 — How to uniquely identify an embedding model (the "hash" question)

There is **no universal content hash** for embedding models the way there is for, say, a Docker image. But your instinct is reasonable — here are the real options, weakest to strongest:

1. **Model name string** (what we thread through today, e.g. `"bge-m3"`). Human-readable and already available from `AiModelConfig.ModelName`, but not collision-proof: two different servers could both call something "bge-m3" and produce different weights/spaces.
2. **Name + dimension.** Cheap and meaningfully safer — and dimension is something we must store anyway (it's what makes vectors index-compatible). A model can't silently change its output space without changing one of these.
3. **HuggingFace repo id + revision (commit SHA).** This is the closest thing to a true "model hash." HF models are git repos; a revision like `BAAI/bge-m3@<commit-sha>` pins the exact weights. The catch: our serving layer (vLLM / LiteLLM) is configured by _name_, and doesn't reliably report the revision back to us, so we'd have to capture it at config time.
4. **Server-reported digest.** Some runtimes expose one (e.g. Ollama gives a sha256 of the model blob); vLLM/LiteLLM generally do not. Not portable.

**What actually matters for correctness** is narrower than "globally unique": we must never (a) compare vectors from two different models, or (b) mix two dimensions under one ANN index. Both are guaranteed by an identity that is _stable per model_ and _implies the dimension_.

**Recommendation:** store two columns — `embedding_model` (the configured name string, governed by `AiModelConfig`) and `dimensions` (int). That pair is sufficient to partition the vector space and to drive per-model partial indexes. Optionally add a nullable `model_revision` if/when we can capture the HF commit SHA at config time, for stronger provenance. I'd **not** invent a synthetic hash — it adds no safety unless it's derived from something the running server actually exposes, which today it isn't.

(One governance note: because identity collapses to the configured name, the org owns uniqueness via `AiModelConfig`. If that's a concern, the HF-revision capture in option 3 is the upgrade path.)

##### Revised, Postgres-first work breakdown (cross-project removed)

- **Phase 0 — schema:** migration adding `organization_id`, `project_id`, `embedding_model`, `dimensions` (nullable to start); update `Embedding.cs` + EF snapshot. Keep vector DDL in raw `migrationBuilder.Sql`.
- **Phase 1 — backfill:** `UPDATE … FROM deeplynx.records` for org/project; constant backfill of `embedding_model='bge-m3'` / `dimensions=1024` for legacy rows (**J2 sign-off**); then `SET NOT NULL`.
- **Phase 2 — indexes:** per-model **partial HNSW** index using the opclass chosen in the #3 write-up, built `CONCURRENTLY` (note: can't run in EF's per-migration transaction — needs the suppress-transaction flag or an out-of-band script); plus composite btree on `(project_id, embedding_model)`.
- **Phase 3 — write path:** propagate `project_id` / `organization_id` / `embedding_model` / `dimensions` to the INSERT in `text_to_chunks.py` (extend the intermediate pika messages, or re-query `records` by `record_id`).
- **Phase 4 — read path:** add `project_id` + `embedding_model` predicates to the query; enable `hnsw.iterative_scan`; tune `ef_search`. Reconcile the operator with the chosen opclass.

Open dependency: Phases 2 and 4 are blocked on the AI engineers' answer to the #3 metric write-up.

## Implementation Tasks (Ordered)

**Locked decisions** (from human + AI-team answers above):

- Metric: **cosine** distance only, system-wide (`<=>` / `vector_cosine_ops`). Chosen because we're going multi-model — cosine is scale-invariant, so we do **not** rely on or enforce per-model normalization (bge-m3 normalized; others may not, and cosine handles both).
- Cross-project search: **out of scope**. Every query stays within one project.
- `ontology_vector`: **out of scope**.
- Storage: **single table + per-model partial HNSW**, designed partition-ready; physical partitioning deferred.
- Model identity: `embedding_model` (name) + `dimensions` (int) columns. No synthetic hash.

The tasks below are in execution order. They are grouped by bucket (DB schema → data → DB indexes → Python write → Python read → ops/test) per the Notes, but the numbering is the order we actually do them in.

### Bucket A — Database schema (`deeplynx.datalayer`)

1. **Add metadata columns to `dl_vector.embeddings`** via a new EF migration (raw `migrationBuilder.Sql`):
   - `organization_id BIGINT` (nullable for now)
   - `project_id BIGINT` (nullable for now)
   - `embedding_model TEXT` (nullable for now)
   - `dimensions INTEGER` (nullable for now)
2. **Relax the vector column** from `vector(1024)` to **untyped `vector`** (`ALTER COLUMN vector TYPE vector;`) so models of differing dimensionality can coexist in one column. (Per-model partial indexes will re-impose a fixed dimension where it matters — see Bucket C.)
3. **Update the EF model + snapshot:** add the four properties to `Models/Embedding.cs` and regenerate/confirm `DeeplynxContextModelSnapshot.cs`. Keep all vector DDL in raw SQL, not the fluent API.

#### Checking Bucket A for DL-1490

```
Step 1 — Get a fresh Postgres running

Your DB is the nx-postgres container (PG 18 + pgvector, built from Dockerfiles/database/Dockerfile.local). If you nuked by dropping the volume:

docker compose down -v          # removes the volume = the actual "nuke"
docker compose up -d nx-postgres

Bringing the container back up recreates an empty deeplynx database with the vector extension already available in the image, so migrations can build the schema from scratch.

Step 2 — Apply the migrations

Two options:

Option A — explicit (recommended for now; clear feedback):
dotnet ef database update -c DeeplynxContext --project deeplynx.datalayer --startup-project deeplynx.api
This runs every pending migration in order against the running DB, ending with your AddEmbeddingMetadataColumns. Use this so you see exactly where it succeeds/fails. (If dotnet ef isn't found, your .NET tools dir isn't on PATH — the README notes this at line 175.)

Option B — just start the app:
docker compose up server
MigrateAsync() runs on startup and applies the same pending migrations. Fine once you trust the migration, but a startup failure is noisier to read than Option A's output.

Either way it uses the app's configured Postgres connection (your .env / appsettings), so make sure that points at the freshly-recreated DB.

Step 3 — Verify it actually did what we intended

The two things from Bucket A that EF won't tell you about directly — confirm them after the run:

-- 1. The four new columns exist and are nullable
\d dl_vector.embeddings

-- 2. The vector column is now UNTYPED (no "(1024)") — proves the raw-SQL relax ran
SELECT format_type(atttypid, atttypmod)
FROM pg_attribute
WHERE attrelid = 'dl_vector.embeddings'::regclass AND attname = 'vector';
-- expect: "vector"   (NOT "vector(1024)")

If that second query still shows vector(1024), the ALTER COLUMN vector TYPE vector; line didn't make it into the migration — that's the one EF can't generate, so it's the most likely thing to have slipped.
```

### Bucket B — Data backfill (`deeplynx.datalayer`, J2 as owner)

4. **Backfill org/project** from the source of truth:
   `UPDATE dl_vector.embeddings e SET organization_id = r.organization_id, project_id = r.project_id FROM deeplynx.records r WHERE e.record_id = r.id;`
5. **Backfill legacy model identity** (assumption requiring **J2 sign-off**): all pre-existing rows were bge-m3 @ 1024 →
   `UPDATE dl_vector.embeddings SET embedding_model = 'bge-m3', dimensions = 1024 WHERE embedding_model IS NULL;`
6. **Enforce NOT NULL** on `organization_id`, `project_id`, `embedding_model`, `dimensions` once backfill is verified (separate migration step, after data validation).

### Bucket C — Indexes (`deeplynx.datalayer`, out-of-transaction)

> **Decision (DL-1490):** all of Bucket C is settled. The cheap, transactional DDL ships as an EF
> migration; the partial HNSW DDL ships out-of-band via checked-in scripts + a runbook. See
> [ADR 002](documentation/adr/002_embedding_ann_index_provisioning.md) and the
> [maintenance runbook](documentation/runbooks/embedding-index-maintenance.md).

7. **Per-model partial HNSW index, cosine opclass** — **out-of-band, not an EF migration.** Delivered
   as `deeplynx.datalayer/Scripts/Embeddings/create_hnsw_index_bge_m3.sql` (bge-m3 baseline, "entry
   #1") + `_new_model_index_template.sql` (every future model). `CONCURRENTLY` cannot run in EF's
   per-migration transaction, and we want the heavy build under operator control (maintenance window,
   tuned `maintenance_work_mem`) rather than firing during `MigrateAsync` at app startup.
8. **Composite btree** to support the standard filter path — ✅ **DONE in Bucket A.** Migration
   `20260617045633_AddEmbeddingMetadataColumns` created `idx_embeddings_org_project_model` on
   `(project_id, embedding_model)`. (It's cheap and transactional, so it correctly lives in the EF
   migration alongside the column adds. Name mentions "org" but indexes only `project_id`,
   `embedding_model` — cosmetic, left as-is.)
9. **Per-model index-provisioning procedure — DECIDED: release-gated ops runbook.** New embedding
   models get their partial HNSW index built as a documented runbook step in the release that
   introduces support for them (model serving + config land in the same release). Chosen over runtime
   DDL (API-registration / lazy-on-insert / reconciler) because at multi-million-row scale we want
   operator control of a slow, memory-heavy build, and to keep `CREATE` privilege out of the data
   plane. The runbook's one weakness — a forgotten step — is covered by a **detection query**
   (`detect_unindexed_models.sql`) that flags any model in the data lacking a valid HNSW index
   (degrades to correct-but-slow exact scan), intended to back a startup/health check in the
   companion source PR.

### Bucket D — Write path (`deeplynx.insight`, Python)

10. **Propagate context to the worker that inserts.** Extend the intermediate pika messages (`FileMessage` / `ImageMessage` / `TextMessage` in `app/utils/pika_utils.py`) to carry `project_id` and `organization_id` (they already exist on `PendingFileMessage` upstream; `embedding_model_name` is already threaded). Alternative if message changes are undesirable: re-query `deeplynx.records` by `record_id` inside the worker.
11. **Update the INSERT** in `app/rabbitmq/workers/text_to_chunks.py` to populate the new columns:
    `INSERT INTO dl_vector.embeddings (record_id, page_number, text_chunk, vector, organization_id, project_id, embedding_model, dimensions) VALUES (...)`.
    Derive `dimensions` from the actual embedding length (and assert it matches the model's expected dimension).

### Bucket E — Read path (`deeplynx.insight`, Python)

12. **Switch the query to cosine** in `app/fastapi/api/routes/query.py`: change the ordering operator from `<-> ` (L2) to **`<=>`** (cosine).
13. **Add the model + project predicates** so the query hits the correct partial index and only compares within one vector space:
    `... WHERE record_id = ANY(%s) AND embedding_model = %s ... ORDER BY vector <=> %s::vector LIMIT %s`.
    The project's embedding model is already known at query time (`embeddingConfig.ModelName` on the .NET side); thread it into the Insight `/query` request payload.
14. **Tune ANN session settings** before the search: enable `SET hnsw.iterative_scan = relaxed_order;` (so the `record_id` pre-filter doesn't starve the candidate set) and set an appropriate `SET hnsw.ef_search = N;`.

### Bucket F — Validation & rollout

15. **Recall/quality check:** compare ANN results against exact KNN on a sample to confirm acceptable recall at the chosen `ef_search`.
16. **Performance benchmark** at target scale (load to multi-million rows; measure query latency and index build time/memory).
17. **Integration tests** in `app/tests/` covering: insert populates all new columns; query filters by model and returns only same-model results; the cosine ordering path.
18. **Ops runbook:** document the `CONCURRENTLY` index build, `maintenance_work_mem` guidance, the legacy-row backfill assumption, and the new-model index-provisioning step from Task 9.

### Sequencing notes / dependencies

- A → B → C is strictly ordered (columns before backfill before NOT NULL before indexes).
- D and E can proceed in parallel with each other once A is merged, but **E (read path) should land with or after C (indexes)** so the cosine query has an index to use.
- Task 9 (new-model index provisioning) is the one genuinely new operational concept introduced by the partial-index design — don't let it slip; an unindexed model silently degrades to exact scan.
