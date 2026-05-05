using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaintextObjectStorageConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "config",
                schema: "deeplynx",
                table: "object_storages");

            migrationBuilder.AlterColumn<string>(
                name: "config_encrypted",
                schema: "deeplynx",
                table: "object_storages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "config_encrypted",
                schema: "deeplynx",
                table: "object_storages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "config",
                schema: "deeplynx",
                table: "object_storages",
                type: "jsonb",
                nullable: true);
        }
    }
}
