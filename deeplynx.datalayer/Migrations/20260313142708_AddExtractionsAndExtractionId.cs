using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionsAndExtractionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "extraction_id",
                schema: "deeplynx",
                table: "relationships",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "extraction_id",
                schema: "deeplynx",
                table: "records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "extraction_id",
                schema: "deeplynx",
                table: "edges",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "extraction_id",
                schema: "deeplynx",
                table: "classes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "extractions",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("extractions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_extractions_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_relationships_extraction_id",
                schema: "deeplynx",
                table: "relationships",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "IX_records_extraction_id",
                schema: "deeplynx",
                table: "records",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "IX_edges_extraction_id",
                schema: "deeplynx",
                table: "edges",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_extraction_id",
                schema: "deeplynx",
                table: "classes",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "IX_extractions_created_by",
                schema: "deeplynx",
                table: "extractions",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "classes_extraction_id_fkey",
                schema: "deeplynx",
                table: "classes",
                column: "extraction_id",
                principalSchema: "deeplynx",
                principalTable: "extractions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "edges_extraction_id_fkey",
                schema: "deeplynx",
                table: "edges",
                column: "extraction_id",
                principalSchema: "deeplynx",
                principalTable: "extractions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "records_extraction_id_fkey",
                schema: "deeplynx",
                table: "records",
                column: "extraction_id",
                principalSchema: "deeplynx",
                principalTable: "extractions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "relationships_extraction_id_fkey",
                schema: "deeplynx",
                table: "relationships",
                column: "extraction_id",
                principalSchema: "deeplynx",
                principalTable: "extractions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "classes_extraction_id_fkey",
                schema: "deeplynx",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "edges_extraction_id_fkey",
                schema: "deeplynx",
                table: "edges");

            migrationBuilder.DropForeignKey(
                name: "records_extraction_id_fkey",
                schema: "deeplynx",
                table: "records");

            migrationBuilder.DropForeignKey(
                name: "relationships_extraction_id_fkey",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropTable(
                name: "extractions",
                schema: "deeplynx");

            migrationBuilder.DropIndex(
                name: "IX_relationships_extraction_id",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropIndex(
                name: "IX_records_extraction_id",
                schema: "deeplynx",
                table: "records");

            migrationBuilder.DropIndex(
                name: "IX_edges_extraction_id",
                schema: "deeplynx",
                table: "edges");

            migrationBuilder.DropIndex(
                name: "IX_classes_extraction_id",
                schema: "deeplynx",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "extraction_id",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "extraction_id",
                schema: "deeplynx",
                table: "records");

            migrationBuilder.DropColumn(
                name: "extraction_id",
                schema: "deeplynx",
                table: "edges");

            migrationBuilder.DropColumn(
                name: "extraction_id",
                schema: "deeplynx",
                table: "classes");
        }
    }
}
