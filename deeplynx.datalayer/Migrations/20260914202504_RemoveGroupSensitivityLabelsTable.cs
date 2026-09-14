using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGroupSensitivityLabelsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_sensitivity_labels",
                schema: "deeplynx");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_sensitivity_labels",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    granted_by = table.Column<long>(type: "bigint", nullable: true),
                    group_id = table.Column<long>(type: "bigint", nullable: false),
                    label_id = table.Column<long>(type: "bigint", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("group_sensitivity_labels_pkey", x => x.id);
                    table.ForeignKey(
                        name: "group_sensitivity_labels_granted_by_fkey",
                        column: x => x.granted_by,
                        principalSchema: "deeplynx",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "group_sensitivity_labels_group_id_fkey",
                        column: x => x.group_id,
                        principalSchema: "deeplynx",
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "group_sensitivity_labels_label_id_fkey",
                        column: x => x.label_id,
                        principalSchema: "deeplynx",
                        principalTable: "sensitivity_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_group_sensitivity_labels_group_id",
                schema: "deeplynx",
                table: "group_sensitivity_labels",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "idx_group_sensitivity_labels_id",
                schema: "deeplynx",
                table: "group_sensitivity_labels",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_group_sensitivity_labels_label_id",
                schema: "deeplynx",
                table: "group_sensitivity_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_sensitivity_labels_granted_by",
                schema: "deeplynx",
                table: "group_sensitivity_labels",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "unique_group_sensitivity_label",
                schema: "deeplynx",
                table: "group_sensitivity_labels",
                columns: new[] { "group_id", "label_id" },
                unique: true);
        }
    }
}
