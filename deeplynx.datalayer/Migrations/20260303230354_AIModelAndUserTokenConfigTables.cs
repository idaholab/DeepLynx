using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AIModelAndUserTokenConfigTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_model_configs",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: true),
                    server_url = table.Column<string>(type: "text", nullable: false),
                    model_name = table.Column<string>(type: "text", nullable: false),
                    model_type = table.Column<string>(type: "text", nullable: false),
                    requires_token = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    @default = table.Column<bool>(name: "default", type: "boolean", nullable: false, defaultValue: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_model_configs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_model_configs_users_last_updated_by",
                        column: x => x.last_updated_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ai_model_configs_organization_id_fkey",
                        column: x => x.organization_id,
                        principalSchema: "deeplynx",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_model_configs_project_id_fkey",
                        column: x => x.project_id,
                        principalSchema: "deeplynx",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_model_tokens",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    ai_model_config_id = table.Column<long>(type: "bigint", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_model_tokens_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_model_tokens_ai_model_config_id_fkey",
                        column: x => x.ai_model_config_id,
                        principalSchema: "deeplynx",
                        principalTable: "ai_model_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_model_tokens_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ai_model_configs_id",
                schema: "deeplynx",
                table: "ai_model_configs",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_model_configs_last_updated_by",
                schema: "deeplynx",
                table: "ai_model_configs",
                column: "last_updated_by");

            migrationBuilder.CreateIndex(
                name: "idx_ai_model_configs_organization_id",
                schema: "deeplynx",
                table: "ai_model_configs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_model_configs_project_id",
                schema: "deeplynx",
                table: "ai_model_configs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_model_tokens_ai_model_config_id",
                schema: "deeplynx",
                table: "user_model_tokens",
                column: "ai_model_config_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_model_tokens_id",
                schema: "deeplynx",
                table: "user_model_tokens",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_user_model_tokens_user_id",
                schema: "deeplynx",
                table: "user_model_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_model_tokens",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "ai_model_configs",
                schema: "deeplynx");
        }
    }
}
