using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectStorageIdProjectOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "object_storage_id",
                schema: "deeplynx",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "object_storage_id",
                schema: "deeplynx",
                table: "organizations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "object_storage_id",
                schema: "deeplynx",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "object_storage_id",
                schema: "deeplynx",
                table: "organizations");
        }
    }
}
