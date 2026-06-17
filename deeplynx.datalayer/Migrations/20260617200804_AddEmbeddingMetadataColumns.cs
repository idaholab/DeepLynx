using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dimensions",
                schema: "dl_vector",
                table: "embeddings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                schema: "dl_vector",
                table: "embeddings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "organization_id",
                schema: "dl_vector",
                table: "embeddings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "project_id",
                schema: "dl_vector",
                table: "embeddings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_embeddings_project_model",
                schema: "dl_vector",
                table: "embeddings",
                columns: new[] { "project_id", "embedding_model" });

            // Relax vector column dimension to accommodate vectors of any size.
            // Raw SQL: EF cannot express the pgvector typmod change.
            migrationBuilder.Sql("ALTER TABLE dl_vector.embeddings ALTER COLUMN vector TYPE vector;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_embeddings_project_model",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "dimensions",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "project_id",
                schema: "dl_vector",
                table: "embeddings");

            // Restore the original fixed dimension (bge-m3, 1024).
            migrationBuilder.Sql("ALTER TABLE dl_vector.embeddings ALTER COLUMN vector TYPE vector(1024);");
        }
    }
}
