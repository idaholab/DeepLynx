using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class MoveOsIdToOrgAndProjDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Get the default object storage ID for an organization and set it for all organizations where DefaultObjectStorageId is NULL
            migrationBuilder.Sql(@"
                UPDATE deeplynx.organizations o
                SET default_object_storage_id = (
                    SELECT id
                    FROM deeplynx.object_storages os
                    WHERE os.organization_id = o.id
                        AND os.""default"" = true
                        AND os.project_id IS NULL
                        LIMIT 1
                )
                WHERE o.default_object_storage_id IS NULL;
            ");

            // Step 2: Set all projects with a NULL DefaultObjectStorageId to the DefaultObjectStorageId of their parent organization
            migrationBuilder.Sql(@"
                UPDATE deeplynx.projects p
                SET default_object_storage_id = o.default_object_storage_id
                FROM deeplynx.organizations o
                WHERE p.organization_id = o.id
                    AND p.default_object_storage_id IS NULL
                    AND o.default_object_storage_id IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Revert DefaultObjectStorageId for organizations
            migrationBuilder.Sql(@"
                UPDATE deeplynx.organizations o
                SET default_object_storage_id = NULL
                WHERE o.default_object_storage_id IN (
                    SELECT id
                    FROM deeplynx.object_storages os
                    WHERE os.""default"" = true
                        AND os.project_id IS NULL
                )
                AND o.default_object_storage_id IS NOT NULL;
            ");

            // Step 2: Revert DefaultObjectStorageId for projects
            migrationBuilder.Sql(@"
                UPDATE deeplynx.projects p
                SET default_object_storage_id = NULL
                WHERE p.default_object_storage_id IN (
                    SELECT id
                    FROM deeplynx.object_storages os
                    WHERE os.""default"" = true
                        AND os.project_id IS NULL
                )
                AND p.default_object_storage_id IS NOT NULL;
            ");
        }
    }
}
