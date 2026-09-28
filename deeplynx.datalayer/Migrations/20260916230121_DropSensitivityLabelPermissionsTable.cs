using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class DropSensitivityLabelPermissionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensitivity_label_permissions",
                schema: "deeplynx");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensitivity_label_permissions",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_by = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    name = table.Column<string>(type: "text", nullable: false)
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

            // replace the contents of the sensitivity_label_permissions table
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.sensitivity_label_permissions 
                    (label_id, action, name, description,
                    last_updated_by, last_updated_at)
                SELECT DISTINCT ON (slg.label_id, slp.name)
                    slg.label_id, slp.name, sl.name, slp.description, 
                    slg.granted_by, slg.granted_at
                FROM deeplynx.sensitivity_label_permission_actions slp
                JOIN deeplynx.sensitivity_label_grants slg
                    ON slg.label_permission_id = slp.id
                JOIN deeplynx.sensitivity_labels sl
                    ON sl.id = slg.label_id;
            ");
        }
    }
}
