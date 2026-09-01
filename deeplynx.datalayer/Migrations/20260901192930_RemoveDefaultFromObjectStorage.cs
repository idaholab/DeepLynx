using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultFromObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default",
                schema: "deeplynx",
                table: "object_storages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "default",
                schema: "deeplynx",
                table: "object_storages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
