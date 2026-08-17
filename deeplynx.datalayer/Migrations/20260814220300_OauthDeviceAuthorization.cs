using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class OauthDeviceAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oauth_device_authorization_requests",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_code_hash = table.Column<string>(type: "text", nullable: false),
                    user_code_hash = table.Column<string>(type: "text", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    polling_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    poll_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_polled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    denied_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    consumed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("oauth_device_auth_requests_pkey", x => x.id);
                    table.ForeignKey(
                        name: "oauth_device_auth_requests_application_id_fkey",
                        column: x => x.application_id,
                        principalSchema: "deeplynx",
                        principalTable: "oauth_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "oauth_device_auth_requests_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "oauth_refresh_tokens",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("oauth_refresh_tokens_pkey", x => x.id);
                    table.ForeignKey(
                        name: "oauth_refresh_tokens_application_id_fkey",
                        column: x => x.application_id,
                        principalSchema: "deeplynx",
                        principalTable: "oauth_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "oauth_refresh_tokens_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_application_id",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_device_code_hash",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "device_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_expires_at",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_requests_id",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_status",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_user_code_hash",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "user_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_oauth_device_auth_user_id",
                schema: "deeplynx",
                table: "oauth_device_authorization_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_refresh_tokens_application_id",
                schema: "deeplynx",
                table: "oauth_refresh_tokens",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_refresh_tokens_expires_at",
                schema: "deeplynx",
                table: "oauth_refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_refresh_tokens_id",
                schema: "deeplynx",
                table: "oauth_refresh_tokens",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_oauth_refresh_tokens_token_hash",
                schema: "deeplynx",
                table: "oauth_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_oauth_refresh_tokens_user_id",
                schema: "deeplynx",
                table: "oauth_refresh_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oauth_device_authorization_requests",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "oauth_refresh_tokens",
                schema: "deeplynx");
        }
    }
}
