using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectDefaultCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
            // Update organization-level role descriptions
            migrationBuilder.Sql(@"
                UPDATE deeplynx.roles
                SET description = CASE 
                    WHEN name = 'Admin' THEN 'Administrator role with full permissions'
                    WHEN name = 'User' THEN 'User role with limited permissions'
                END,
                last_updated_at = NOW()
                WHERE project_id IS NULL
                  AND name IN ('Admin', 'User');
            "); 
            
            // Update project_members to use organization-level roles
            migrationBuilder.Sql(@"
                UPDATE deeplynx.project_members pm
                SET role_id = org_role.id
                FROM deeplynx.roles proj_role
                JOIN deeplynx.roles org_role ON 
                    org_role.organization_id = proj_role.organization_id 
                    AND org_role.name = proj_role.name
                    AND org_role.project_id IS NULL
                WHERE pm.role_id = proj_role.id
                  AND proj_role.project_id IS NOT NULL
                  AND proj_role.name IN ('Admin', 'User');
            ");

            // Delete role-permission associations for project-level Admin/User roles
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.role_permissions
                WHERE role_id IN (
                    SELECT id FROM deeplynx.roles 
                    WHERE project_id IS NOT NULL 
                    AND name IN ('Admin', 'User')
                );
            ");

            // Delete project-level Admin and User roles
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.roles
                WHERE project_id IS NOT NULL 
                  AND name IN ('Admin', 'User');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This rollback recreates project-level roles but cannot restore
            // the exact previous state. Project members will be reassigned to newly
            // created project-level roles, but original role IDs will be different.
            
            migrationBuilder.Sql(@"
                -- Recreate project-level Admin and User roles for each project
                INSERT INTO deeplynx.roles (name, description, organization_id, project_id, is_archived, last_updated_at)
                SELECT 
                    org_role.name,
                    CASE 
                        WHEN org_role.name = 'Admin' THEN 'Project administrator with full permissions'
                        WHEN org_role.name = 'User' THEN 'Standard project user with limited permissions'
                    END as description,
                    p.organization_id,
                    p.id as project_id,
                    false as is_archived,
                    NOW() as last_updated_at
                FROM deeplynx.projects p
                CROSS JOIN deeplynx.roles org_role
                WHERE org_role.project_id IS NULL
                  AND org_role.name IN ('Admin', 'User')
                  AND org_role.organization_id = p.organization_id
                  AND NOT EXISTS (
                      SELECT 1 FROM deeplynx.roles existing
                      WHERE existing.project_id = p.id
                        AND existing.name = org_role.name
                  );

                -- Update project_members back to project-level roles
                UPDATE deeplynx.project_members pm
                SET role_id = proj_role.id
                FROM deeplynx.roles org_role
                JOIN deeplynx.roles proj_role ON 
                    proj_role.organization_id = org_role.organization_id 
                    AND proj_role.name = org_role.name
                    AND proj_role.project_id = pm.project_id
                WHERE pm.role_id = org_role.id
                  AND org_role.project_id IS NULL
                  AND org_role.name IN ('Admin', 'User');

                -- Recreate role-permission associations for project-level roles
                INSERT INTO deeplynx.role_permissions (role_id, permission_id)
                SELECT DISTINCT
                    proj_role.id as role_id,
                    rp.permission_id
                FROM deeplynx.roles proj_role
                JOIN deeplynx.roles org_role ON 
                    org_role.organization_id = proj_role.organization_id
                    AND org_role.name = proj_role.name
                    AND org_role.project_id IS NULL
                JOIN deeplynx.role_permissions rp ON rp.role_id = org_role.id
                WHERE proj_role.project_id IS NOT NULL
                  AND proj_role.name IN ('Admin', 'User')
                  AND NOT EXISTS (
                      SELECT 1 FROM deeplynx.role_permissions existing
                      WHERE existing.role_id = proj_role.id
                        AND existing.permission_id = rp.permission_id
                  );
            ");

        }
    }
}
