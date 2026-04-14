using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations.Staging
{
    /// <inheritdoc />
    public partial class AddCrossSchemaEdges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_name",
                schema: "staging",
                table: "relationships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_name",
                schema: "staging",
                table: "relationships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "class_name",
                schema: "staging",
                table: "records",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cross_schema_edges",
                schema: "staging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    extraction_id = table.Column<long>(type: "bigint", nullable: false),
                    data_source_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    origin_original_id = table.Column<string>(type: "text", nullable: true),
                    deeplynx_origin_original_id = table.Column<string>(type: "text", nullable: true),
                    destination_original_id = table.Column<string>(type: "text", nullable: true),
                    deeplynx_destination_original_id = table.Column<string>(type: "text", nullable: true),
                    relationship_name = table.Column<string>(type: "text", nullable: true),
                    deeplynx_relationship_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cross_schema_edges_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cross_schema_edges_extraction_id_fkey",
                        column: x => x.extraction_id,
                        principalSchema: "deeplynx",
                        principalTable: "extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_cross_schema_edges_extraction_id",
                schema: "staging",
                table: "cross_schema_edges",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "idx_cross_schema_edges_project_id",
                schema: "staging",
                table: "cross_schema_edges",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cross_schema_edges",
                schema: "staging");

            migrationBuilder.DropColumn(
                name: "destination_name",
                schema: "staging",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "origin_name",
                schema: "staging",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "class_name",
                schema: "staging",
                table: "records");
        }
    }
}
