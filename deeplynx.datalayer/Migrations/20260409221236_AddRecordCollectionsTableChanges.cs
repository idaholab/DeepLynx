using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordCollectionsTableChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_record_collections_original_id",
                schema: "deeplynx",
                table: "record_collections");

            migrationBuilder.DropIndex(
                name: "unique_record_collection_original_id",
                schema: "deeplynx",
                table: "record_collections");

            migrationBuilder.DropColumn(
                name: "original_id",
                schema: "deeplynx",
                table: "record_collections");

            migrationBuilder.CreateIndex(
                name: "unique_record_collection_name",
                schema: "deeplynx",
                table: "record_collections",
                columns: new[] { "project_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "unique_record_collection_name",
                schema: "deeplynx",
                table: "record_collections");

            migrationBuilder.AddColumn<string>(
                name: "original_id",
                schema: "deeplynx",
                table: "record_collections",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_original_id",
                schema: "deeplynx",
                table: "record_collections",
                column: "original_id");

            migrationBuilder.CreateIndex(
                name: "unique_record_collection_original_id",
                schema: "deeplynx",
                table: "record_collections",
                columns: new[] { "project_id", "original_id" },
                unique: true,
                filter: "original_id IS NOT NULL");
        }
    }
}
