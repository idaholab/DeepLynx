using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations.Lattice
{
    /// <inheritdoc />
    public partial class AddValidationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lattice");

            migrationBuilder.CreateTable(
                name: "extraction_classes",
                schema: "lattice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    extraction_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    promoted_id = table.Column<long>(type: "bigint", nullable: true),
                    validation_status = table.Column<string>(type: "text", nullable: true),
                    ontology_class_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extraction_records",
                schema: "lattice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    extraction_id = table.Column<long>(type: "bigint", nullable: false),
                    extraction_class_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    data_source_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    promoted_id = table.Column<long>(type: "bigint", nullable: true),
                    validation_status = table.Column<string>(type: "text", nullable: true),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    llm_score = table.Column<double>(type: "double precision", nullable: false),
                    embedding_plausibility = table.Column<double>(type: "double precision", nullable: false),
                    statistical_frequency = table.Column<double>(type: "double precision", nullable: false),
                    structural_consistency = table.Column<double>(type: "double precision", nullable: false),
                    ensemble_score = table.Column<double>(type: "double precision", nullable: false),
                    deeplynx_record_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_records", x => x.id);
                    table.ForeignKey(
                        name: "extraction_records_extraction_class_id_fkey",
                        column: x => x.extraction_class_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extraction_relationships",
                schema: "lattice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    extraction_id = table.Column<long>(type: "bigint", nullable: false),
                    origin_class_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_class_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    promoted_id = table.Column<long>(type: "bigint", nullable: true),
                    validation_status = table.Column<string>(type: "text", nullable: true),
                    ontology_relationship_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_relationships", x => x.id);
                    table.ForeignKey(
                        name: "extraction_relationships_destination_class_id_fkey",
                        column: x => x.destination_class_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "extraction_relationships_origin_class_id_fkey",
                        column: x => x.origin_class_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extraction_edges",
                schema: "lattice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    extraction_id = table.Column<long>(type: "bigint", nullable: false),
                    extraction_relationship_id = table.Column<long>(type: "bigint", nullable: false),
                    origin_record_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_record_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    data_source_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    promoted_id = table.Column<long>(type: "bigint", nullable: true),
                    validation_status = table.Column<string>(type: "text", nullable: true),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    llm_score = table.Column<double>(type: "double precision", nullable: false),
                    embedding_plausibility = table.Column<double>(type: "double precision", nullable: false),
                    statistical_frequency = table.Column<double>(type: "double precision", nullable: false),
                    structural_consistency = table.Column<double>(type: "double precision", nullable: false),
                    ensemble_score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_edges", x => x.id);
                    table.ForeignKey(
                        name: "extraction_edges_destination_record_id_fkey",
                        column: x => x.destination_record_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "extraction_edges_extraction_relationship_id_fkey",
                        column: x => x.extraction_relationship_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_relationships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "extraction_edges_origin_record_id_fkey",
                        column: x => x.origin_record_id,
                        principalSchema: "lattice",
                        principalTable: "extraction_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_extraction_edges_destination_record_id",
                schema: "lattice",
                table: "extraction_edges",
                column: "destination_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_edges_extraction_relationship_id",
                schema: "lattice",
                table: "extraction_edges",
                column: "extraction_relationship_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_edges_origin_record_id",
                schema: "lattice",
                table: "extraction_edges",
                column: "origin_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_records_extraction_class_id",
                schema: "lattice",
                table: "extraction_records",
                column: "extraction_class_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_relationships_destination_class_id",
                schema: "lattice",
                table: "extraction_relationships",
                column: "destination_class_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_relationships_origin_class_id",
                schema: "lattice",
                table: "extraction_relationships",
                column: "origin_class_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extraction_edges",
                schema: "lattice");

            migrationBuilder.DropTable(
                name: "extraction_records",
                schema: "lattice");

            migrationBuilder.DropTable(
                name: "extraction_relationships",
                schema: "lattice");

            migrationBuilder.DropTable(
                name: "extraction_classes",
                schema: "lattice");
        }
    }
}
