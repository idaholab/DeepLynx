using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordCollectionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ====================================================================
            // STEP 1: Insert new Record Collection permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.permissions (name, description, action, resource, label_id, is_archived, is_default, organization_id, project_id)
                VALUES
                    ('Read Record Collection', 'Permission to read a record collection', 'read', 'record_collection', NULL, false, true, NULL, NULL),
                    ('Write Record Collection', 'Permission to write a record collection', 'write', 'record_collection', NULL, false, true, NULL, NULL);
            ");
            
            // ====================================================================
            // STEP 2: Add Record Collection permissions to Admin role
            // ====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.role_permissions (role_id, permission_id)
                SELECT
                    r.id AS role_id,
                    p.id AS permission_id
                FROM
                    deeplynx.roles r
                CROSS JOIN
                    deeplynx.permissions p
                WHERE
                    r.name = 'Admin'
                    AND r.is_archived = FALSE
                    AND p.is_default = TRUE
                    AND p.is_archived = FALSE
                    AND p.project_id IS NULL
                    AND p.organization_id IS NULL
                    AND p.label_id IS NULL
                    AND p.resource = 'record_collection'
                    AND p.action IN ('read', 'write');
            ");
            
            // ====================================================================
            // STEP 3: Add Record Collection permissions to User role
            // ====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.role_permissions (role_id, permission_id)
                SELECT
                    r.id AS role_id,
                    p.id AS permission_id
                FROM
                    deeplynx.roles r
                CROSS JOIN
                    deeplynx.permissions p
                WHERE
                    r.name = 'User'
                    AND r.is_archived = FALSE
                    AND p.is_default = TRUE
                    AND p.is_archived = FALSE
                    AND p.project_id IS NULL
                    AND p.organization_id IS NULL
                    AND p.label_id IS NULL
                    AND p.resource = 'record_collection'
                    AND p.action IN ('read', 'write');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ====================================================================
            // STEP 1: Remove Record Collection permissions from role_permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.role_permissions
                WHERE permission_id IN (
                    SELECT id FROM deeplynx.permissions
                    WHERE resource = 'record_collection'
                    AND is_default = TRUE
                );
            ");
            
            // ====================================================================
            // STEP 2: Delete Record Collection permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.permissions
                WHERE name IN ('Read Record Collection', 'Write Record Collection');
            ");
        }
    }
}
