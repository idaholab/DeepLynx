using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provenance_records",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    record_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    artifact_version_id = table.Column<string>(type: "text", nullable: false),
                    pipeline_run_id = table.Column<string>(type: "text", nullable: false),
                    prov_id = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provenance_json = table.Column<string>(type: "jsonb", nullable: false),
                    pipeline_version = table.Column<string>(type: "text", nullable: true),
                    processing_config_version = table.Column<string>(type: "text", nullable: true),
                    embedding_model_name = table.Column<string>(type: "text", nullable: true),
                    signature = table.Column<string>(type: "text", nullable: true),
                    signature_algorithm = table.Column<string>(type: "text", nullable: true),
                    signing_key_name = table.Column<string>(type: "text", nullable: true),
                    signing_key_version = table.Column<string>(type: "text", nullable: true),
                    signed_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    signed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    verification_status = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("provenance_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "provenance_records_organization_id_fkey",
                        column: x => x.organization_id,
                        principalSchema: "deeplynx",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "provenance_records_project_id_fkey",
                        column: x => x.project_id,
                        principalSchema: "deeplynx",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "provenance_records_record_id_fkey",
                        column: x => x.record_id,
                        principalSchema: "deeplynx",
                        principalTable: "records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_id",
                schema: "deeplynx",
                table: "provenance_records",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_organization_id",
                schema: "deeplynx",
                table: "provenance_records",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_project_id",
                schema: "deeplynx",
                table: "provenance_records",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_record_id",
                schema: "deeplynx",
                table: "provenance_records",
                column: "record_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provenance_records",
                schema: "deeplynx");
        }
    }
}
