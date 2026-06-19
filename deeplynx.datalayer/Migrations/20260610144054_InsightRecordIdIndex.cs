using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class InsightRecordIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Btree index on dl_vector.embeddings.record_id
            //
            // query.py filters with WHERE record_id = ANY(%s) before the
            // vector similarity sort.  Without this index every query does
            // a full sequential scan of the embeddings table.
            //
            // Also required for the MATERIALIZED CTE in query.py to be
            // fast once HNSW indexing is enabled.
            //
            // NOTE: EF Core migrations run inside a transaction, so we
            // cannot use CREATE INDEX CONCURRENTLY here.  The btree build
            // on record_id is fast and the brief table lock is acceptable.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_embeddings_record_id
                ON dl_vector.embeddings (record_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS dl_vector.idx_embeddings_record_id;
            ");
        }
    }
}
