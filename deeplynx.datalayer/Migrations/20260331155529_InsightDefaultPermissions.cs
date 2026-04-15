using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class InsightDefaultPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ====================================================================
            // STEP 1: Insert new Insight permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.permissions (name, description, action, resource, label_id, is_archived, is_default, organization_id, project_id)
                VALUES
                    ('Read Insight', 'Permission to read results from Insight', 'read', 'insight', NULL, false, true, NULL, NULL),
                    ('Write Insight', 'Permission to embed files in Insight', 'write', 'insight', NULL, false, true, NULL, NULL);
            ");

            // ====================================================================
            // STEP 2: Add Insight permissions to Admin role
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
                    AND p.resource = 'insight'
                    AND p.action IN ('read', 'write');
            ");

            // ====================================================================
            // STEP 3: Add Insight permissions to User role
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
                    AND p.resource = 'insight'
                    AND p.action IN ('read', 'write');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ====================================================================
            // STEP 1: Remove Insight permissions from role_permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.role_permissions
                WHERE permission_id IN (
                    SELECT id FROM deeplynx.permissions
                    WHERE resource = 'insight'
                    AND is_default = TRUE
                );
            ");

            // ====================================================================
            // STEP 2: Delete Insight permissions
            // ====================================================================
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.permissions
                WHERE name IN ('Read Insight', 'Write Insight');
            ");
        }
    }
}