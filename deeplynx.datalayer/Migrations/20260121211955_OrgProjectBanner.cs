using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class OrgProjectBanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "config",
                schema: "deeplynx",
                table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "banner",
                schema: "deeplynx",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "banner",
                schema: "deeplynx",
                table: "organizations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "banner",
                schema: "deeplynx",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "banner",
                schema: "deeplynx",
                table: "organizations");

            migrationBuilder.AddColumn<string>(
                name: "config",
                schema: "deeplynx",
                table: "projects",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"tagsMutable\": false, \"ontologyMutable\": false, \"edgeRecordsMutable\": false}'::jsonb");
        }
    }
}
