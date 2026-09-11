using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSLPermissionsFKToUserLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "unique_user_sensitivity_label",
                schema: "deeplynx",
                table: "user_sensitivity_labels");

            migrationBuilder.AddColumn<long>(
                name: "label_permission_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_sensitivity_label_permission_actions_label_permission_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "label_permission_id");

            migrationBuilder.CreateIndex(
                name: "unique_user_sensitivity_label",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                columns: new[] { "user_id", "label_id", "label_permission_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "user_sensitivity_labels_label_permission_id_fkey",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                column: "label_permission_id",
                principalSchema: "deeplynx",
                principalTable: "sensitivity_label_permission_actions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // copy permissions from the new table into the linking table
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.user_sensitivity_labels (user_id, label_id, granted_by, granted_at, label_permission_id)
                SELECT DISTINCT usl.user_id, usl.label_id, usl.granted_by, usl.granted_at, spa.id AS ""spa_id""
	                FROM deeplynx.user_sensitivity_labels usl
	                JOIN deeplynx.sensitivity_label_permissions slp
		                ON slp.label_id = usl.label_id
	                JOIN deeplynx.sensitivity_label_permission_actions spa
		                ON spa.name = slp.action;
            ");
            
            // delete simplistic permissions from the linking table
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.user_sensitivity_labels
                WHERE label_permission_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // re-insert simplistic permissions into the linking table
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.user_sensitivity_labels (user_id, label_id, granted_by, granted_at, label_permission_id)
                SELECT DISTINCT ON (usl.user_id, usl.label_id)
                    usl.user_id, usl.label_id, usl.granted_by, usl.granted_at, NULL
                FROM deeplynx.user_sensitivity_labels usl
                WHERE usl.label_permission_id IS NOT NULL
                ORDER BY usl.user_id, usl.label_id, usl.granted_at ASC;
            ");
            
            // delete linking table entries with permissions from the new table
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.user_sensitivity_labels
                WHERE label_permission_id IS NOT NULL;
            ");
            
            migrationBuilder.DropForeignKey(
                name: "user_sensitivity_labels_label_permission_id_fkey",
                schema: "deeplynx",
                table: "user_sensitivity_labels");

            migrationBuilder.DropIndex(
                name: "idx_user_sensitivity_label_permission_actions_label_permission_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels");

            migrationBuilder.DropIndex(
                name: "unique_user_sensitivity_label",
                schema: "deeplynx",
                table: "user_sensitivity_labels");

            migrationBuilder.DropColumn(
                name: "label_permission_id",
                schema: "deeplynx",
                table: "user_sensitivity_labels");

            migrationBuilder.CreateIndex(
                name: "unique_user_sensitivity_label",
                schema: "deeplynx",
                table: "user_sensitivity_labels",
                columns: new[] { "user_id", "label_id" },
                unique: true);
        }
    }
}
