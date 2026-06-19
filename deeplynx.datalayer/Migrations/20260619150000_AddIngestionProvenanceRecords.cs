using System;
using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DeeplynxContext))]
    [Migration("20260619150000_AddIngestionProvenanceRecords")]
    public partial class AddIngestionProvenanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_provenance_records",
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
                    table.PrimaryKey("ingestion_provenance_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ingestion_provenance_records_record_id_fkey",
                        column: x => x.record_id,
                        principalSchema: "deeplynx",
                        principalTable: "records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_provenance_records_artifact_version_id",
                schema: "deeplynx",
                table: "ingestion_provenance_records",
                column: "artifact_version_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_provenance_records_content_hash",
                schema: "deeplynx",
                table: "ingestion_provenance_records",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_provenance_records_id",
                schema: "deeplynx",
                table: "ingestion_provenance_records",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_provenance_records_prov_id",
                schema: "deeplynx",
                table: "ingestion_provenance_records",
                column: "prov_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_provenance_records_record_id",
                schema: "deeplynx",
                table: "ingestion_provenance_records",
                column: "record_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_provenance_records",
                schema: "deeplynx");
        }
    }
}
