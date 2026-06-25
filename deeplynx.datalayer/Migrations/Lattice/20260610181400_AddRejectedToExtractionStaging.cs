using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations.Lattice
{
    /// <inheritdoc />
    public partial class AddRejectedToExtractionStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "rejected",
                schema: "lattice",
                table: "extraction_relationships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "rejected",
                schema: "lattice",
                table: "extraction_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "rejected",
                schema: "lattice",
                table: "extraction_edges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "rejected",
                schema: "lattice",
                table: "extraction_classes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rejected",
                schema: "lattice",
                table: "extraction_relationships");

            migrationBuilder.DropColumn(
                name: "rejected",
                schema: "lattice",
                table: "extraction_records");

            migrationBuilder.DropColumn(
                name: "rejected",
                schema: "lattice",
                table: "extraction_edges");

            migrationBuilder.DropColumn(
                name: "rejected",
                schema: "lattice",
                table: "extraction_classes");
        }
    }
}
