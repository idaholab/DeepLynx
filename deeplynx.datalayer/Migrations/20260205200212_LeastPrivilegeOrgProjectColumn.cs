using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class LeastPrivilegeOrgProjectColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_sensitivity_label",
                schema: "deeplynx",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_sensitivity_label",
                schema: "deeplynx",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "require_sensitivity_label",
                schema: "deeplynx",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "require_sensitivity_label",
                schema: "deeplynx",
                table: "organizations");
        }
    }
}
