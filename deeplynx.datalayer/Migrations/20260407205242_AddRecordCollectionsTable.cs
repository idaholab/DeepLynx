using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordCollectionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "record_collections",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    original_id = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("record_collections_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_record_collections_users_last_updated_by",
                        column: x => x.last_updated_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "record_collections_organization_id_fkey",
                        column: x => x.organization_id,
                        principalSchema: "deeplynx",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "record_collections_project_id_fkey",
                        column: x => x.project_id,
                        principalSchema: "deeplynx",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_collection_labels",
                schema: "deeplynx",
                columns: table => new
                {
                    record_collection_id = table.Column<long>(type: "bigint", nullable: false),
                    label_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("record_collection_labels_pkey", x => new { x.record_collection_id, x.label_id });
                    table.ForeignKey(
                        name: "record_collection_labels_label_id_fkey",
                        column: x => x.label_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "record_collection_labels_record_collection_id_fkey",
                        column: x => x.record_collection_id,
                        principalSchema: "deeplynx",
                        principalTable: "record_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_collection_records",
                schema: "deeplynx",
                columns: table => new
                {
                    record_collection_id = table.Column<long>(type: "bigint", nullable: false),
                    record_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("record_collection_records_pkey", x => new { x.record_collection_id, x.record_id });
                    table.ForeignKey(
                        name: "record_collection_records_record_collection_id_fkey",
                        column: x => x.record_collection_id,
                        principalSchema: "deeplynx",
                        principalTable: "record_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "record_collection_records_record_id_fkey",
                        column: x => x.record_id,
                        principalSchema: "deeplynx",
                        principalTable: "records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_collection_tags",
                schema: "deeplynx",
                columns: table => new
                {
                    record_collection_id = table.Column<long>(type: "bigint", nullable: false),
                    tag_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("record_collection_tags_pkey", x => new { x.record_collection_id, x.tag_id });
                    table.ForeignKey(
                        name: "record_collection_tags_record_collection_id_fkey",
                        column: x => x.record_collection_id,
                        principalSchema: "deeplynx",
                        principalTable: "record_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "record_collection_tags_tag_id_fkey",
                        column: x => x.tag_id,
                        principalSchema: "deeplynx",
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_labels_label_id",
                schema: "deeplynx",
                table: "record_collection_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_labels_record_collection_id",
                schema: "deeplynx",
                table: "record_collection_labels",
                column: "record_collection_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_records_record_collection_id",
                schema: "deeplynx",
                table: "record_collection_records",
                column: "record_collection_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_records_record_id",
                schema: "deeplynx",
                table: "record_collection_records",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_tags_record_collection_id",
                schema: "deeplynx",
                table: "record_collection_tags",
                column: "record_collection_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collection_tags_tag_id",
                schema: "deeplynx",
                table: "record_collection_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_id",
                schema: "deeplynx",
                table: "record_collections",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_last_updated_by",
                schema: "deeplynx",
                table: "record_collections",
                column: "last_updated_by");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_name",
                schema: "deeplynx",
                table: "record_collections",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_organization_id",
                schema: "deeplynx",
                table: "record_collections",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_original_id",
                schema: "deeplynx",
                table: "record_collections",
                column: "original_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_project_id",
                schema: "deeplynx",
                table: "record_collections",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_record_collections_properties",
                schema: "deeplynx",
                table: "record_collections",
                column: "properties")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "unique_record_collection_original_id",
                schema: "deeplynx",
                table: "record_collections",
                columns: new[] { "project_id", "original_id" },
                unique: true,
                filter: "original_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "record_collection_labels",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "record_collection_records",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "record_collection_tags",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "record_collections",
                schema: "deeplynx");
        }
    }
}
