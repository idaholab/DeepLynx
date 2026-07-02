using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Project admin status now derives from project_members.is_project_admin, so the "Admin"
            // role is no longer needed. The project_members.role_id FK cascades on delete, so detach
            // members from the Admin role first; otherwise deleting the role would remove their
            // project membership entirely. These members already carry is_project_admin = true
            // (set by BackfillProjectAdminFromAdminRole), so they keep their admin access.
            migrationBuilder.Sql(@"
                UPDATE deeplynx.project_members
                SET role_id = NULL
                WHERE role_id IN (SELECT id FROM deeplynx.roles WHERE name = 'Admin');");

            // Remove the Admin role(s). Associated role_permissions rows cascade automatically.
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.roles WHERE name = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort restore. The original Admin role was seeded by application code that has
            // since been removed, so recreate the org-level Admin role per organization and relink
            // the project admins detached by Up. Role permission grants are NOT restored — that
            // seeding logic no longer exists in code.
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.roles (name, description, organization_id, project_id, is_archived, last_updated_at)
                SELECT 'Admin', 'Administrator role with full permissions', o.id, NULL, false,
                       (now() AT TIME ZONE 'utc')
                FROM deeplynx.organizations o
                WHERE NOT EXISTS (
                    SELECT 1 FROM deeplynx.roles r
                    WHERE r.name = 'Admin' AND r.organization_id = o.id AND r.project_id IS NULL);");

            migrationBuilder.Sql(@"
                UPDATE deeplynx.project_members pm
                SET role_id = r.id
                FROM deeplynx.projects p
                JOIN deeplynx.roles r
                    ON r.organization_id = p.organization_id AND r.name = 'Admin' AND r.project_id IS NULL
                WHERE pm.project_id = p.id
                  AND pm.is_project_admin = true
                  AND pm.role_id IS NULL;");
        }
    }
}
