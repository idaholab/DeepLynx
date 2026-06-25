using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations.Lattice
{
    /// <inheritdoc />
    public partial class AddSourceRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "source_record_id",
                schema: "lattice",
                table: "extraction_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "source_record_id",
                schema: "lattice",
                table: "extraction_edges",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_record_id",
                schema: "lattice",
                table: "extraction_records");

            migrationBuilder.DropColumn(
                name: "source_record_id",
                schema: "lattice",
                table: "extraction_edges");
        }
    }
}
