using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class TrackEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS so this migration is safe to apply against databases
            // where the dl_vector.embeddings table was created by the earlier PgVector
            // migration (20260209221659) before EF Core began tracking it.
            migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS dl_vector;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS dl_vector.embeddings (
                    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    record_id BIGINT NOT NULL,
                    page_number INTEGER NOT NULL,
                    text_chunk TEXT NOT NULL,
                    vector vector(1024) NOT NULL,
                    last_updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT embeddings_record_id_fkey
                        FOREIGN KEY (record_id)
                        REFERENCES deeplynx.records(id)
                        ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_embeddings_id ON dl_vector.embeddings(id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_embeddings_record_id ON dl_vector.embeddings(record_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "embeddings",
                schema: "dl_vector");
        }
    }
}
