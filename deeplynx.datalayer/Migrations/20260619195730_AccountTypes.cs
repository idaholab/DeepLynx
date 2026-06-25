using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AccountTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete service accounts before migration
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.users
                WHERE is_service_account = true");

            migrationBuilder.DropColumn(
                name: "is_service_account",
                schema: "deeplynx",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "account_type",
                schema: "deeplynx",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "standard");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account_type",
                schema: "deeplynx",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "is_service_account",
                schema: "deeplynx",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
