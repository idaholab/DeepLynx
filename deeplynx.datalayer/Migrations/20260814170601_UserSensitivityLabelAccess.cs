using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class UserSensitivityLabelAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the new tables (additive, permissions.label_id still intact)
            migrationBuilder.CreateTable(
                name: "sensitivity_label_permissions",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sensitivity_label_permissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensitivity_label_permissions_users_last_updated_by",
                        column: x => x.last_updated_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "sensitivity_label_permissions_label_id_fkey",
                        column: x => x.label_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sensitivity_labels",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    granted_by = table.Column<long>(type: "bigint", nullable: true),
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
                        name: "user_sensitivity_labels_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_permissions_action",
                schema: "deeplynx",
                table: "sensitivity_label_permissions",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_permissions_id",
                schema: "deeplynx",
                table: "sensitivity_label_permissions",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_permissions_label_id",
                schema: "deeplynx",
                table: "sensitivity_label_permissions",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_permissions_last_updated_by",
                schema: "deeplynx",
                table: "sensitivity_label_permissions",
                column: "last_updated_by");

            migrationBuilder.CreateIndex(
                name: "unique_sensitivity_label_permission_label_action",
                schema: "deeplynx",
                table: "sensitivity_label_permissions",
                columns: new[] { "label_id", "action" },
                unique: true);

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
                columns: new[] { "user_id", "label_id" },
                unique: true);

            // 2. Backfill sensitivity_label_permissions 1:1 from the existing label-scoped permission rows
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.sensitivity_label_permissions
                    (label_id, action, name, description, last_updated_by, last_updated_at, is_archived)
                SELECT label_id, action, name, description, last_updated_by, last_updated_at, is_archived
                FROM deeplynx.permissions
                WHERE label_id IS NOT NULL;
            ");

            // 3. Backfill user_sensitivity_labels: union every label reachable via any action,
            //    through direct or group project membership, exactly as today's role-based check computes it.
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.user_sensitivity_labels (user_id, label_id, granted_at)
                SELECT combined.user_id, combined.label_id, CURRENT_TIMESTAMP
                FROM (
                    SELECT pm.user_id AS user_id, p.label_id AS label_id
                    FROM deeplynx.project_members pm
                    JOIN deeplynx.roles r ON r.id = pm.role_id
                    JOIN deeplynx.role_permissions rp ON rp.role_id = pm.role_id
                    JOIN deeplynx.permissions p ON p.id = rp.permission_id
                    WHERE pm.user_id IS NOT NULL
                      AND p.label_id IS NOT NULL
                      AND p.is_archived = false
                      AND r.is_archived = false

                    UNION

                    SELECT gu.user_id AS user_id, p.label_id AS label_id
                    FROM deeplynx.project_members pm
                    JOIN deeplynx.groups g ON g.id = pm.group_id
                    JOIN deeplynx.group_users gu ON gu.group_id = pm.group_id
                    JOIN deeplynx.roles r ON r.id = pm.role_id
                    JOIN deeplynx.role_permissions rp ON rp.role_id = pm.role_id
                    JOIN deeplynx.permissions p ON p.id = rp.permission_id
                    WHERE pm.group_id IS NOT NULL
                      AND p.label_id IS NOT NULL
                      AND p.is_archived = false
                      AND r.is_archived = false
                      AND g.is_archived = false
                ) combined;
            ");

            // 4. Now that both new tables are fully backfilled, remove the label-scoped permission rows
            //    (cascades to role_permissions) and drop label_id from permissions entirely.
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.permissions WHERE label_id IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "permissions_label_id_fkey",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "idx_permissions_label_id",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "permissions_unique_org_label_action",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "permissions_unique_project_label_action",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_default_permissions_no_org_project_label",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "label_id",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.AddCheckConstraint(
                name: "chk_default_permissions_no_org_project_label",
                schema: "deeplynx",
                table: "permissions",
                sql: "is_default = false OR (organization_id IS NULL AND project_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_default_permissions_no_org_project_label",
                schema: "deeplynx",
                table: "permissions");

            migrationBuilder.AddColumn<long>(
                name: "label_id",
                schema: "deeplynx",
                table: "permissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_permissions_label_id",
                schema: "deeplynx",
                table: "permissions",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "permissions_unique_org_label_action",
                schema: "deeplynx",
                table: "permissions",
                columns: new[] { "organization_id", "label_id", "action" },
                unique: true,
                filter: "project_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "permissions_unique_project_label_action",
                schema: "deeplynx",
                table: "permissions",
                columns: new[] { "organization_id", "project_id", "label_id", "action" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_default_permissions_no_org_project_label",
                schema: "deeplynx",
                table: "permissions",
                sql: "is_default = false OR (organization_id IS NULL AND project_id IS NULL AND label_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "permissions_label_id_fkey",
                schema: "deeplynx",
                table: "permissions",
                column: "label_id",
                principalSchema: "deeplynx",
                principalTable: "sensitivity_labels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Note: this does not restore the deleted label-scoped permission rows or their
            // role_permissions links, nor the data in the tables dropped below — this migration's
            // Down is for schema rollback only, not a full data restore.
            migrationBuilder.DropTable(
                name: "sensitivity_label_permissions",
                schema: "deeplynx");

            migrationBuilder.DropTable(
                name: "user_sensitivity_labels",
                schema: "deeplynx");
        }
    }
}