# Runbook: Embedding ANN Index Maintenance

Operational procedures for the per-model partial HNSW indexes on `dl_vector.embeddings`.
For the rationale behind this design, see
[ADR 002](../adr/002_embedding_ann_index_provisioning.md).

## Background (read first)

- ANN search uses **per-model partial HNSW indexes** (cosine / `vector_cosine_ops`). Each index
  casts to one model's dimension and is predicated on one `embedding_model`.
- These index builds use `CREATE INDEX CONCURRENTLY`, which **cannot run in a transaction** and so
  are **not** EF migrations. They live as checked-in scripts under
  `deeplynx.datalayer/Scripts/Embeddings/` and are run by an operator.
- A model **without** its index is not broken — queries degrade to (slow) exact scan. So a missing
  index is a performance issue, never a correctness issue.

## Prerequisites

- PostgreSQL 18 + pgvector 0.8.1 (the `nx-postgres` image).
- A DB role with `CREATE` on the `dl_vector` schema.
- A maintenance window for large builds, and an elevated session `maintenance_work_mem`
  (e.g. `SET maintenance_work_mem = '2GB';` — size to the host; larger = faster build).
- The Bucket A migration `20260617045633_AddEmbeddingMetadataColumns` already applied (adds the
  metadata columns, relaxes the vector column to untyped, and creates the composite btree).

---

## Procedure A — Initial rollout (one time, after the migration deploys)

> Steps A1–A2 are **J2-owned** and mutate existing data; A2 carries an assumption requiring
> sign-off. See `Scripts/Embeddings/backfill_and_enforce.sql`.

1. **A1. Backfill org/project** from `deeplynx.records` (clean join on `record_id`).
2. **A2. Backfill legacy model identity** — set `embedding_model = 'bge-m3'`, `dimensions = 1024`
   for all pre-existing rows. **Requires J2 sign-off** (assumes the MVP only ran bge-m3 @ 1024).
3. **A3. Verify no NULLs remain**, then enforce `NOT NULL`. Preferred delivery is a follow-up EF
   migration (with `Models/Embedding.cs` flipped to non-nullable) so EF and the DB agree; the manual
   `ALTER` is included (commented) in the backfill script for an ops-only rollout.
4. **A4. Build the bge-m3 partial HNSW index** — run `Scripts/Embeddings/create_hnsw_index_bge_m3.sql`
   in the maintenance window. On a large table this is the slow step; set `maintenance_work_mem`
   first. Verify `indisvalid AND indisready`.

---

## Procedure B — Add a new embedding model (per release)

Do this in the release that introduces support for the model (the model's serving backend and
config land in the same release).

1. Determine the exact `embedding_model` string and the model's vector `dimensions`.
2. Copy `Scripts/Embeddings/_new_model_index_template.sql`, fill `<MODEL_NAME>`, `<MODEL_SLUG>`,
   `<DIMS>`. Keep the index name `idx_embeddings_hnsw_<MODEL_SLUG>` (the detection query relies on
   the convention).
3. `SET maintenance_work_mem` high, then run the script (`CREATE INDEX CONCURRENTLY`). For a brand
   new model the matching row set is small, so the build is cheap; cost grows with adoption.
4. Verify the index is valid:
   ```sql
   SELECT indisvalid, indisready
   FROM pg_index
   WHERE indexrelid = 'dl_vector.idx_embeddings_hnsw_<MODEL_SLUG>'::regclass;
   ```
5. Record the new index in the release notes / change log.

---

## Procedure C — Detect un-indexed models (safety net)

Run `Scripts/Embeddings/detect_unindexed_models.sql`. Any row with
`has_valid_hnsw_index = false` is a model whose queries are falling back to exact scan — build its
index via Procedure B. (This query is the intended backing for a startup/health check, to be added
in the companion source-code PR.)

---

## Procedure D — Recover a failed / INVALID index build

A `CREATE INDEX CONCURRENTLY` that is interrupted leaves an **INVALID** index behind: it imposes
write overhead but is never used by queries, and a plain existence check will wrongly report the
model as indexed.

1. Find invalid indexes:
   ```sql
   SELECT c.relname
   FROM pg_index i
   JOIN pg_class c ON c.oid = i.indexrelid
   JOIN pg_class t ON t.oid = i.indrelid
   JOIN pg_namespace n ON n.oid = t.relnamespace
   WHERE n.nspname = 'dl_vector' AND t.relname = 'embeddings' AND NOT i.indisvalid;
   ```
2. Drop and rebuild:
   ```sql
   DROP INDEX CONCURRENTLY IF EXISTS dl_vector.<index_name>;
   -- then re-run the relevant build script (Procedure A4 or B)
   ```

---

## Out of scope here (companion source-code PR)

- Read-path switch to cosine (`<=>`) and the `embedding_model` / `project_id` query predicates.
- Write-path propagation of `organization_id` / `project_id` / `embedding_model` / `dimensions`
  into the INSERT.
- ANN session tuning (`hnsw.iterative_scan`, `hnsw.ef_search`).
- The startup/health check that runs Procedure C automatically.
