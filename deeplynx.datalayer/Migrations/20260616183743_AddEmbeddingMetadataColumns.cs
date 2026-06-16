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

            // Relax the vector type to accomodate any model with any dimensions
            migrationBuilder.Sql("ALTER TABLE dl_vector.embeddings ALTER COLUMN vector TYPE vector;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            // The rollback will fail if vectors with dimensions other than 1024 are in the table
            migrationBuilder.Sql("ALTER TABLE dl_vector.embeddings ALTER COLUMN vector TYPE vector(1024);");
        }
    }
}
