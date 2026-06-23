using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProjectAdminFromAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: preserve existing behavior where project admin was derived from the "Admin" role.
            // The runtime ProjectAdminCheck now keys off is_project_admin instead of the role name.
            migrationBuilder.Sql(@"
                UPDATE deeplynx.project_members pm
                SET is_project_admin = true
                FROM deeplynx.roles r
                WHERE pm.role_id = r.id
                  AND r.name = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE deeplynx.project_members pm
                SET is_project_admin = false
                FROM deeplynx.roles r
                WHERE pm.role_id = r.id
                  AND r.name = 'Admin';");
        }
    }
}
