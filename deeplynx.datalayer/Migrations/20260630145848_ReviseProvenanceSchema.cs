using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ReviseProvenanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_provenance_records_content_hash",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "artifact_version_id",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "embedding_model_name",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "pipeline_run_id",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "pipeline_version",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "processing_config_version",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "signature_algorithm",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "signed_at",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "signed_payload_hash",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "signing_key_name",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "signing_key_version",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "chunk_hash",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.DropColumn(
                name: "embedding_hash",
                schema: "dl_vector",
                table: "embeddings");

            migrationBuilder.RenameColumn(
                name: "content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                newName: "file_content_hash");

            migrationBuilder.RenameColumn(
                name: "normalized_content_hash",
                schema: "deeplynx",
                table: "records",
                newName: "file_content_hash");

            migrationBuilder.AlterColumn<string>(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "records",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<long>(
                name: "historical_record_id",
                schema: "deeplynx",
                table: "provenance_records",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "historical_records",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_file_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                column: "file_content_hash");

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_historical_record_id",
                schema: "deeplynx",
                table: "provenance_records",
                column: "historical_record_id");

            migrationBuilder.AddForeignKey(
                name: "provenance_records_historical_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records",
                column: "historical_record_id",
                principalSchema: "deeplynx",
                principalTable: "historical_records",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "provenance_records_historical_record_id_fkey",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropIndex(
                name: "idx_provenance_records_file_content_hash",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropIndex(
                name: "idx_provenance_records_historical_record_id",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.DropColumn(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "historical_records");

            migrationBuilder.DropColumn(
                name: "historical_record_id",
                schema: "deeplynx",
                table: "provenance_records");

            migrationBuilder.AlterColumn<string>(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "records",
                newName: "normalized_content_hash");

            migrationBuilder.RenameColumn(
                name: "file_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                newName: "content_hash");

            migrationBuilder.AddColumn<string>(
                name: "artifact_version_id",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "embedding_model_name",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pipeline_run_id",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "pipeline_version",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_config_version",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_algorithm",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "signed_at",
                schema: "deeplynx",
                table: "provenance_records",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_payload_hash",
                schema: "deeplynx",
                table: "provenance_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signing_key_name",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signing_key_version",
                schema: "deeplynx",
                table: "provenance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chunk_hash",
                schema: "dl_vector",
                table: "embeddings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_hash",
                schema: "dl_vector",
                table: "embeddings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_provenance_records_content_hash",
                schema: "deeplynx",
                table: "provenance_records",
                column: "content_hash");
        }
    }
}
