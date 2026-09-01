using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ProvenanceRecordsDropCascadingForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "provenance_records_historical_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropForeignKey(
                name: "provenance_records_organization_id_fkey",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropForeignKey(
                name: "provenance_records_project_id_fkey",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropForeignKey(
                name: "provenance_records_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "provenance_records_historical_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records",
                column: "historical_record_id",
                principalSchema: "deeplynx",
                principalTable: "historical_records",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "provenance_records_organization_id_fkey",
                schema: "deeplynx",
                table: "provenance_records",
                column: "organization_id",
                principalSchema: "deeplynx",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "provenance_records_project_id_fkey",
                schema: "deeplynx",
                table: "provenance_records",
                column: "project_id",
                principalSchema: "deeplynx",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "provenance_records_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records",
                column: "record_id",
                principalSchema: "deeplynx",
                principalTable: "records",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
