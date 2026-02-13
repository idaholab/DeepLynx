using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
 // Insert update permissions for each resource
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.permissions (name, description, action, resource, is_default, last_updated_at, is_archived)
                VALUES 
                    ('Update Projects', 'Permission to update project data', 'update', 'project', true, NOW(), false),
                    ('Update Object Storages', 'Permission to update object storage', 'update', 'object_storage', true, NOW(), false),
                    ('Update Data Sources', 'Permission to update data sources', 'update', 'data_source', true, NOW(), false),
                    ('Update Records', 'Permission to update records', 'update', 'record', true, NOW(), false),
                    ('Update Edges', 'Permission to update edges', 'update', 'edge', true, NOW(), false),
                    ('Update Files', 'Permission to update files', 'update', 'file', true, NOW(), false),
                    ('Update Tags', 'Permission to update tags', 'update', 'tag', true, NOW(), false),
                    ('Update Classes', 'Permission to update classes', 'update', 'class', true, NOW(), false),
                    ('Update Relationships', 'Permission to update relationships', 'update', 'relationship', true, NOW(), false),
                    ('Update Users', 'Permission to update users', 'update', 'user', true, NOW(), false),
                    ('Update Groups', 'Permission to update groups', 'update', 'group', true, NOW(), false),
                    ('Update Organizations', 'Permission to update organizations', 'update', 'organization', true, NOW(), false),
                    ('Update Roles', 'Permission to update roles', 'update', 'role', true, NOW(), false),
                    ('Update Permissions', 'Permission to update permissions', 'update', 'permission', true, NOW(), false),
                    ('Update Sensitivity Labels', 'Permission to update sensitivity labels', 'update', 'sensitivity_label', true, NOW(), false);
            ");

            // Link all update permissions to Admin role
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM deeplynx.roles r
                CROSS JOIN deeplynx.permissions p
                WHERE r.name = 'Admin'
                AND p.action = 'update'
                AND p.is_default = true;
            ");

            // Link update permissions to User role for specific resources
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                FROM deeplynx.roles r
                CROSS JOIN deeplynx.permissions p
                WHERE r.name = 'User'
                AND p.action = 'update'
                AND p.resource IN ('data_source', 'record', 'edge', 'file', 'tag', 'class', 'relationship')
                AND p.is_default = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove role-permission associations for update permissions
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.role_permissions
                WHERE permission_id IN (
                    SELECT id FROM deeplynx.permissions
                    WHERE action = 'update' AND is_default = true
                );
            ");

            // Remove all update permissions
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.permissions
                WHERE action = 'update' AND is_default = true;
            ");
        }
    }
}
