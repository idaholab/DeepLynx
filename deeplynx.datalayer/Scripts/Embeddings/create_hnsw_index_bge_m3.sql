-- Partial HNSW index for the bge-m3 embedding model (1024 dims), cosine opclass.
-- This is "entry #1" of the per-model index set (see the embedding index maintenance runbook).
--
-- OUT-OF-BAND: CREATE INDEX CONCURRENTLY cannot run inside a transaction, so this DDL is NOT an
-- EF migration. Run it directly against the database in a maintenance window, as a role that has
-- CREATE on the dl_vector schema. Idempotent: safe to re-run (IF NOT EXISTS).
--
-- Tune build memory first (session-local; size to the host — larger = faster build):
--   SET maintenance_work_mem = '2GB';

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_embeddings_hnsw_bge_m3
    ON dl_vector.embeddings
    USING hnsw ((vector::vector(1024)) vector_cosine_ops)
    WHERE embedding_model = 'bge-m3';

-- Verify the build succeeded and the index is usable (expect: t | t):
--   SELECT indisvalid, indisready
--   FROM pg_index
--   WHERE indexrelid = 'dl_vector.idx_embeddings_hnsw_bge_m3'::regclass;
--
-- If indisvalid = f, the concurrent build failed and left an INVALID index. Drop and rebuild:
--   DROP INDEX CONCURRENTLY IF EXISTS dl_vector.idx_embeddings_hnsw_bge_m3;
--   (then re-run this script)
