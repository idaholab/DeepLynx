using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_content_hash",
                schema: "deeplynx",
                table: "records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chunk_hash",
                schema: "dl_vector",
                table: "embeddings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_hash",
                schema: "dl_vector",
                table: "embeddings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "normalized_content_hash",
                schema: "deeplynx",
                table: "records");

            migrationBuilder.DropColumn(
                name: "chunk_hash",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "embedding_hash",
                schema: "dl_vector",
                table: "embeddings");
        }
    }
}
