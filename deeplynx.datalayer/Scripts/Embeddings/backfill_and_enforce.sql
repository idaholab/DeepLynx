-- Bucket B — data backfill + NOT NULL enforcement for dl_vector.embeddings.
--
-- OWNER: J2. Steps 1-2 mutate existing data; step 2 encodes an ASSUMPTION that requires sign-off.
-- Execute AFTER this migration is deployed, in a maintenance window. NOT run automatically and
-- NOT an EF migration. Steps 1-3 are transactional and may be wrapped in a single transaction.

-- 1. Backfill org/project from the source of truth (records). Clean join on record_id.
UPDATE dl_vector.embeddings e
SET organization_id = r.organization_id,
    project_id      = r.project_id
FROM deeplynx.records r
WHERE e.record_id = r.id
  AND (e.organization_id IS NULL OR e.project_id IS NULL);

-- 2. Backfill legacy model identity.
--    ASSUMPTION (REQUIRES J2 SIGN-OFF): every pre-existing row was produced by bge-m3 @ 1024,
--    the only embedding model the MVP ever ran. Legacy rows carry no record of their model, so
--    this is the only option. Do not run until signed off.
UPDATE dl_vector.embeddings
SET embedding_model = 'bge-m3',
    dimensions      = 1024
WHERE embedding_model IS NULL;

-- 3. Verify there are NO remaining NULLs before enforcing NOT NULL (expect 0):
--   SELECT count(*) FROM dl_vector.embeddings
--   WHERE organization_id IS NULL OR project_id IS NULL
--      OR embedding_model IS NULL OR dimensions IS NULL;

-- 4. Enforce NOT NULL once step 3 returns 0.
--    NOTE: prefer delivering this as a follow-up EF migration (and flipping the four properties on
--    Models/Embedding.cs to non-nullable) so the EF model and the DB stay in sync. The statement
--    below is the manual equivalent for an ops-only rollout.
-- ALTER TABLE dl_vector.embeddings
--     ALTER COLUMN organization_id SET NOT NULL,
--     ALTER COLUMN project_id      SET NOT NULL,
--     ALTER COLUMN embedding_model SET NOT NULL,
--     ALTER COLUMN dimensions      SET NOT NULL;
