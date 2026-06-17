-- TEMPLATE — create a per-model partial HNSW index for a NEW embedding model.
-- Copy this file, fill the placeholders, and run via the embedding index maintenance runbook
-- (Procedure B) as part of the release that introduces support for the model.
--
-- Placeholders:
--   <MODEL_NAME>  exact string stored in dl_vector.embeddings.embedding_model (e.g. 'bge-large-en')
--   <MODEL_SLUG>  filesystem/identifier-safe form of the name (e.g. bge_large_en) for the index name
--   <DIMS>        the model's vector dimension (integer), e.g. 768
--
-- Index naming convention (keep it deterministic so the detection query and ops can find it):
--   idx_embeddings_hnsw_<MODEL_SLUG>
--
-- OUT-OF-BAND: CONCURRENTLY => not inside a transaction. Run in a maintenance window.
--   SET maintenance_work_mem = '2GB';

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_embeddings_hnsw_<MODEL_SLUG>
    ON dl_vector.embeddings
    USING hnsw ((vector::vector(<DIMS>)) vector_cosine_ops)
    WHERE embedding_model = '<MODEL_NAME>';

-- Verify (expect: t | t):
--   SELECT indisvalid, indisready
--   FROM pg_index
--   WHERE indexrelid = 'dl_vector.idx_embeddings_hnsw_<MODEL_SLUG>'::regclass;
