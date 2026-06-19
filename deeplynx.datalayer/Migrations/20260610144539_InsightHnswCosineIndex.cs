using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class InsightHnswCosineIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HNSW cosine index on dl_vector.embeddings.vector
            //
            // Improves approximate nearest-neighbor search for unfiltered
            // cross-document queries.  For filtered queries (scoped to
            // specific record_ids), query.py uses a MATERIALIZED CTE that
            // bypasses this index intentionally — see the HNSW pre-filter
            // bug note below.
            //
            // ┌──────────────────────────────────────────────────────────┐
            // │  HNSW PRE-FILTER WARNING                                │
            // │                                                         │
            // │  HNSW does ANN scan FIRST (globally), then applies      │
            // │  WHERE filters.  When filtering to a single project     │
            // │  out of 100K+ rows, the ANN scan returns globally       │
            // │  nearest rows from OTHER projects.  The filter discards │
            // │  them → 0 results.                                      │
            // │                                                         │
            // │  query.py works around this with:                       │
            // │                                                         │
            // │    WITH subset AS MATERIALIZED (                        │
            // │        SELECT ... WHERE record_id = ANY(%s)             │
            // │    )                                                    │
            // │    SELECT ... FROM subset                               │
            // │    ORDER BY vector <=> %s::vector                       │
            // │                                                         │
            // │  DO NOT remove MATERIALIZED from the CTE without        │
            // │  understanding this interaction.                        │
            // └──────────────────────────────────────────────────────────┘
            //
            // NOTE: EF Core migrations run inside a transaction, so we
            // cannot use CONCURRENTLY.  This means the table will lock
            // during the HNSW build, which can take several minutes on
            // large tables.  Plan for a maintenance window if the
            // embeddings table has 500K+ rows.
            //
            // If the build fails partway, an INVALID index may be left:
            //   SELECT * FROM pg_indexes
            //   WHERE indexname = 'idx_embeddings_vector_cosine';
            // Drop it manually before retrying.
            //
            // For bulk loads, drop this index before the load and rebuild
            // after.  Set maintenance_work_mem to >= 1-2 GB for the rebuild.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_embeddings_vector_cosine
                ON dl_vector.embeddings
                USING hnsw (vector vector_cosine_ops)
                WITH (m = 24, ef_construction = 128);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS dl_vector.idx_embeddings_vector_cosine;
            ");
        }
    }
}