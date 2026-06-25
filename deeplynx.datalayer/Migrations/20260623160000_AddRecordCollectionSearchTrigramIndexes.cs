using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    [Migration("20260623160000_AddRecordCollectionSearchTrigramIndexes")]
    public partial class AddRecordCollectionSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
            """);

            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_record_collections_name_trgm
                ON deeplynx.record_collections USING gin (name gin_trgm_ops);
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_record_collections_description_trgm
                ON deeplynx.record_collections USING gin (description gin_trgm_ops);
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_tags_name_trgm
                ON deeplynx.tags USING gin (name gin_trgm_ops);
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_sensitivity_labels_name_trgm
                ON deeplynx.sensitivity_labels USING gin (name gin_trgm_ops);
            """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS deeplynx.idx_sensitivity_labels_name_trgm;
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS deeplynx.idx_tags_name_trgm;
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS deeplynx.idx_record_collections_description_trgm;
            """, suppressTransaction: true);

            migrationBuilder.Sql("""
                DROP INDEX CONCURRENTLY IF EXISTS deeplynx.idx_record_collections_name_trgm;
            """, suppressTransaction: true);
        }
    }
}
