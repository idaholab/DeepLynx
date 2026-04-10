using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations.Staging
{
    /// <inheritdoc />
    public partial class InitialStagingMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staging");

            migrationBuilder.CreateTable(
                name: "classes",
                schema: "staging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    uuid = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    project_id = table.Column<long>(type: "bigint", nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    extraction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("classes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "classes_extraction_id_fkey",
                        column: x => x.extraction_id,
                        principalSchema: "deeplynx",
                        principalTable: "extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "records",
                schema: "staging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uri = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: false),
                    original_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    class_id = table.Column<long>(type: "bigint", nullable: true),
                    data_source_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    object_storage_id = table.Column<long>(type: "bigint", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    file_type = table.Column<string>(type: "text", nullable: true),
                    embedded = table.Column<bool>(type: "boolean", nullable: false),
                    extraction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "records_class_id_fkey",
                        column: x => x.class_id,
                        principalSchema: "staging",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "records_extraction_id_fkey",
                        column: x => x.extraction_id,
                        principalSchema: "deeplynx",
                        principalTable: "extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "relationships",
                schema: "staging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    uuid = table.Column<string>(type: "text", nullable: true),
                    origin_id = table.Column<long>(type: "bigint", nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    destination_id = table.Column<long>(type: "bigint", nullable: true),
                    project_id = table.Column<long>(type: "bigint", nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    extraction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("relationships_pkey", x => x.id);
                    table.ForeignKey(
                        name: "relationships_destination_id_fkey",
                        column: x => x.destination_id,
                        principalSchema: "staging",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "relationships_extraction_id_fkey",
                        column: x => x.extraction_id,
                        principalSchema: "deeplynx",
                        principalTable: "extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "relationships_origin_id_fkey",
                        column: x => x.origin_id,
                        principalSchema: "staging",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "edges",
                schema: "staging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    origin_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_id = table.Column<long>(type: "bigint", nullable: false),
                    relationship_id = table.Column<long>(type: "bigint", nullable: true),
                    data_source_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    extraction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("edges_pkey", x => x.id);
                    table.CheckConstraint("CK_edges_origin_destination_different", "origin_id <> destination_id");
                    table.ForeignKey(
                        name: "edges_destination_id_fkey",
                        column: x => x.destination_id,
                        principalSchema: "staging",
                        principalTable: "records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "edges_extraction_id_fkey",
                        column: x => x.extraction_id,
                        principalSchema: "deeplynx",
                        principalTable: "extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "edges_origin_id_fkey",
                        column: x => x.origin_id,
                        principalSchema: "staging",
                        principalTable: "records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "edges_relationship_id_fkey",
                        column: x => x.relationship_id,
                        principalSchema: "staging",
                        principalTable: "relationships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_classes_extraction_id",
                schema: "staging",
                table: "classes",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_id",
                schema: "staging",
                table: "classes",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_name",
                schema: "staging",
                table: "classes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_classes_organization_id",
                schema: "staging",
                table: "classes",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_project_id",
                schema: "staging",
                table: "classes",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_uuid",
                schema: "staging",
                table: "classes",
                column: "uuid");

            migrationBuilder.CreateIndex(
                name: "unique_class_name",
                schema: "staging",
                table: "classes",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "unique_organization_class_name",
                schema: "staging",
                table: "classes",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "project_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "unique_project_class_name",
                schema: "staging",
                table: "classes",
                columns: new[] { "organization_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_edges_data_source_id",
                schema: "staging",
                table: "edges",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_destination_id",
                schema: "staging",
                table: "edges",
                column: "destination_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_extraction_id",
                schema: "staging",
                table: "edges",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_id",
                schema: "staging",
                table: "edges",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_organization_id",
                schema: "staging",
                table: "edges",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_origin_id",
                schema: "staging",
                table: "edges",
                column: "origin_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_project_id",
                schema: "staging",
                table: "edges",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_edges_relationship_id",
                schema: "staging",
                table: "edges",
                column: "relationship_id");

            migrationBuilder.CreateIndex(
                name: "unique_edge_record_ids",
                schema: "staging",
                table: "edges",
                columns: new[] { "project_id", "origin_id", "destination_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_records_class_id",
                schema: "staging",
                table: "records",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_data_source_id",
                schema: "staging",
                table: "records",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_extraction_id",
                schema: "staging",
                table: "records",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_id",
                schema: "staging",
                table: "records",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_records_name",
                schema: "staging",
                table: "records",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_records_object_storage_id",
                schema: "staging",
                table: "records",
                column: "object_storage_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_organization_id",
                schema: "staging",
                table: "records",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_original_id",
                schema: "staging",
                table: "records",
                column: "original_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_project_id",
                schema: "staging",
                table: "records",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_records_properties",
                schema: "staging",
                table: "records",
                column: "properties")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "unique_record_original_id",
                schema: "staging",
                table: "records",
                columns: new[] { "project_id", "data_source_id", "original_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_relationships_destination_id",
                schema: "staging",
                table: "relationships",
                column: "destination_id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_extraction_id",
                schema: "staging",
                table: "relationships",
                column: "extraction_id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_id",
                schema: "staging",
                table: "relationships",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_name",
                schema: "staging",
                table: "relationships",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_organization_id",
                schema: "staging",
                table: "relationships",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_origin_id",
                schema: "staging",
                table: "relationships",
                column: "origin_id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_project_id",
                schema: "staging",
                table: "relationships",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_relationships_uuid",
                schema: "staging",
                table: "relationships",
                column: "uuid");

            migrationBuilder.CreateIndex(
                name: "unique_organization_relationship_name",
                schema: "staging",
                table: "relationships",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "project_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_name",
                schema: "staging",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "unique_relationship_name",
                schema: "staging",
                table: "relationships",
                columns: new[] { "project_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "edges",
                schema: "staging");

            migrationBuilder.DropTable(
                name: "records",
                schema: "staging");

            migrationBuilder.DropTable(
                name: "relationships",
                schema: "staging");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "staging");
        }
    }
}
