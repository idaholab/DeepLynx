using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordConnectionToExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "source_record_id",
                schema: "deeplynx",
                table: "extractions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_extractions_source_record_id",
                schema: "deeplynx",
                table: "extractions",
                column: "source_record_id");

            migrationBuilder.AddForeignKey(
                name: "FK_extractions_records_source_record_id",
                schema: "deeplynx",
                table: "extractions",
                column: "source_record_id",
                principalSchema: "deeplynx",
                principalTable: "records",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_extractions_records_source_record_id",
                schema: "deeplynx",
                table: "extractions");

            migrationBuilder.DropIndex(
                name: "IX_extractions_source_record_id",
                schema: "deeplynx",
                table: "extractions");

            migrationBuilder.DropColumn(
                name: "source_record_id",
                schema: "deeplynx",
                table: "extractions");
        }
    }
}
