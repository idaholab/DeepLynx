using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceChainHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chain_hash",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_hash",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_record_id_id",
                schema: "deeplynx",
                table: "provenance_records",
                columns: new[] { "record_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_provenance_records_record_id_previous_hash",
                schema: "deeplynx",
                table: "provenance_records",
                columns: new[] { "record_id", "previous_hash" },
                unique: true,
                filter: "previous_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_provenance_records_record_id_id",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropIndex(
                name: "ux_provenance_records_record_id_previous_hash",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "chain_hash",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "previous_hash",
                schema: "deeplynx",
                table: "provenance_records");
        }
    }
}
