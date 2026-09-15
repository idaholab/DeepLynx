using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ReviseSensitivityLabelGrantTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_sensitivity_labels",
                schema: "deeplynx");

            migrationBuilder.CreateTable(
                name: "sensitivity_label_grants",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    group_id = table.Column<long>(type: "bigint", nullable: true),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    label_permission_id = table.Column<long>(type: "bigint", nullable: true),
                    granted_by = table.Column<long>(type: "bigint", nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("sensitivity_label_grants_pkey", x => x.id);
                    table.CheckConstraint("chk_sensitivity_label_grants_user_xor_group", "(user_id IS NOT NULL AND group_id IS NULL) OR (user_id IS NULL AND group_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "sensitivity_label_grants_granted_by_fkey",
                        column: x => x.granted_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "sensitivity_label_grants_group_id_fkey",
                        column: x => x.group_id,
                        principalSchema: "deeplynx",
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "sensitivity_label_grants_label_id_fkey",
                        column: x => x.label_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "sensitivity_label_grants_label_permission_id_fkey",
                        column: x => x.label_permission_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_label_permission_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "sensitivity_label_grants_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_grants_id",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_grants_label_id",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_grants_label_permission_id",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                column: "label_permission_id");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_grants_user_id",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensitivity_label_grants_granted_by",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "unique_sensitivity_label_grant_group",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                columns: new[] { "group_id", "label_id", "label_permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "unique_sensitivity_label_grant_user",
                schema: "deeplynx",
                table: "sensitivity_label_grants",
                columns: new[] { "user_id", "label_id", "label_permission_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensitivity_label_grants",
                schema: "deeplynx");

            migrationBuilder.CreateTable(
                name: "user_sensitivity_labels",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    granted_by = table.Column<long>(type: "bigint", nullable: true),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    label_permission_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_sensitivity_labels_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_sensitivity_labels_granted_by_fkey",
                        column: x => x.granted_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "user_sensitivity_labels_label_id_fkey",
                        column: x => x.label_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_sensitivity_labels_label_permission_id_fkey",
                        column: x => x.label_permission_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_label_permission_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "user_sensitivity_labels_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_sensitivity_labels_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_user_sensitivity_labels_label_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_sensitivity_labels_label_permission_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "label_permission_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_sensitivity_labels_user_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_sensitivity_labels_granted_by",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "unique_user_sensitivity_label",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                columns: new[] { "user_id", "label_id", "label_permission_id" },
                unique: true);
        }
    }
}
