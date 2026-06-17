-- SAFETY NET — list embedding models present in the data that lack a VALID partial HNSW index.
-- Any row returned with has_valid_hnsw_index = false needs its index built (runbook Procedure B);
-- until then that model's queries fall back to (correct but slow) exact scan.
--
-- Run manually after a release, and/or wire into a startup/health check in the companion source PR.
-- Relies on the naming/predicate convention: each per-model index is partial with predicate
-- (embedding_model = '<name>') and uses the hnsw access method.

WITH models_in_data AS (
    SELECT DISTINCT embedding_model
    FROM dl_vector.embeddings
    WHERE embedding_model IS NOT NULL
),
hnsw_partial_indexes AS (
    SELECT
        -- extract the quoted model literal out of the partial-index predicate
        substring(pg_get_expr(i.indpred, i.indrelid) FROM '''([^'']+)''') AS indexed_model,
        (i.indisvalid AND i.indisready)                                   AS usable
    FROM pg_index i
    JOIN pg_class     c  ON c.oid = i.indexrelid
    JOIN pg_class     t  ON t.oid = i.indrelid
    JOIN pg_namespace n  ON n.oid = t.relnamespace
    JOIN pg_am        am ON am.oid = c.relam
    WHERE n.nspname = 'dl_vector'
      AND t.relname = 'embeddings'
      AND am.amname = 'hnsw'
      AND i.indpred IS NOT NULL          -- partial index only
)
SELECT
    m.embedding_model,
    COALESCE(bool_or(h.usable), false) AS has_valid_hnsw_index
FROM models_in_data m
LEFT JOIN hnsw_partial_indexes h ON h.indexed_model = m.embedding_model
GROUP BY m.embedding_model
ORDER BY has_valid_hnsw_index, m.embedding_model;
