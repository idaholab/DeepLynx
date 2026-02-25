using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectDefaultOSCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
         // Create org-level Instance Default 
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.object_storages 
                    (name, type, config, project_id, organization_id, ""default"", last_updated_at, last_updated_by, is_archived)
                SELECT DISTINCT
                    'Instance Default',
                    'filesystem',
                    '{""MountPath"": ""../storage/"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb,
                    NULL::bigint,
                    organization_id,
                    true,
                    NOW(),
                    1,
                    false
                FROM deeplynx.object_storages
                WHERE project_id IS NOT NULL
                    AND name = 'Instance Default'
                    AND ""default"" = true;
            ");

            // Create org-level Timeseries Default 
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.object_storages 
                    (name, type, config, project_id, organization_id, ""default"", last_updated_at, last_updated_by, is_archived)
                SELECT DISTINCT
                    'Timeseries Default',
                    'filesystem',
                    '{""MountPath"": ""../duckdb/"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb,
                    NULL::bigint,
                    organization_id,
                    false,
                    NOW(),
                    1,
                    false
                FROM deeplynx.object_storages
                WHERE project_id IS NOT NULL
                    AND name = 'Timeseries Default'
                    AND ""default"" = false;
            ");

            // Reassign records from project-level Instance Default to org-level
            migrationBuilder.Sql(@"
                UPDATE deeplynx.records r
                SET object_storage_id = org_storage.id
                FROM deeplynx.object_storages proj_storage
                JOIN deeplynx.object_storages org_storage 
                    ON proj_storage.organization_id = org_storage.organization_id
                WHERE r.object_storage_id = proj_storage.id
                    AND proj_storage.project_id IS NOT NULL
                    AND proj_storage.name = 'Instance Default'
                    AND proj_storage.""default"" = true
                    AND org_storage.project_id IS NULL
                    AND org_storage.name = 'Instance Default';
            ");

            // Reassign records from project-level Timeseries Default to org-level
            migrationBuilder.Sql(@"
                UPDATE deeplynx.records r
                SET object_storage_id = org_storage.id
                FROM deeplynx.object_storages proj_storage
                JOIN deeplynx.object_storages org_storage 
                    ON proj_storage.organization_id = org_storage.organization_id
                WHERE r.object_storage_id = proj_storage.id
                    AND proj_storage.project_id IS NOT NULL
                    AND proj_storage.name = 'Timeseries Default'
                    AND proj_storage.""default"" = false
                    AND org_storage.project_id IS NULL
                    AND org_storage.name = 'Timeseries Default';
            ");

            // Delete project-level defaults
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.object_storages
                WHERE project_id IS NOT NULL
                    AND (
                        (name = 'Instance Default' AND ""default"" = true)
                        OR (name = 'Timeseries Default' AND ""default"" = false)
                    );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Recreate project-level defaults for all projects
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.object_storages 
                    (name, type, config, project_id, organization_id, ""default"", last_updated_at, last_updated_by, is_archived)
                SELECT 
                    'Instance Default',
                    'filesystem',
                    '{""MountPath"": ""../storage/"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb,
                    p.id,
                    p.organization_id,
                    true,
                    NOW(),
                    1,
                    false
                FROM deeplynx.projects p
                WHERE NOT EXISTS (
                    SELECT 1 FROM deeplynx.object_storages os
                    WHERE os.project_id = p.id
                        AND os.name = 'Instance Default'
                        AND os.""default"" = true
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.object_storages 
                    (name, type, config, project_id, organization_id, ""default"", last_updated_at, last_updated_by, is_archived)
                SELECT 
                    'Timeseries Default',
                    'filesystem',
                    '{""MountPath"": ""../duckdb/"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb,
                    p.id,
                    p.organization_id,
                    false,
                    NOW(),
                    1,
                    false
                FROM deeplynx.projects p
                WHERE NOT EXISTS (
                    SELECT 1 FROM deeplynx.object_storages os
                    WHERE os.project_id = p.id
                        AND os.name = 'Timeseries Default'
                        AND os.""default"" = false
                );
            ");

            migrationBuilder.Sql(@"
                UPDATE deeplynx.records r
                SET object_storage_id = proj_storage.id
                FROM deeplynx.object_storages org_storage
                JOIN deeplynx.object_storages proj_storage 
                    ON org_storage.organization_id = proj_storage.organization_id
                WHERE r.object_storage_id = org_storage.id
                    AND proj_storage.project_id = r.project_id
                    AND org_storage.project_id IS NULL
                    AND org_storage.name = 'Instance Default'
                    AND org_storage.""default"" = true
                    AND proj_storage.project_id IS NOT NULL
                    AND proj_storage.name = 'Instance Default';
            ");

            migrationBuilder.Sql(@"
                UPDATE deeplynx.records r
                SET object_storage_id = proj_storage.id
                FROM deeplynx.object_storages org_storage
                JOIN deeplynx.object_storages proj_storage 
                    ON org_storage.organization_id = proj_storage.organization_id
                WHERE r.object_storage_id = org_storage.id
                    AND proj_storage.project_id = r.project_id
                    AND org_storage.project_id IS NULL
                    AND org_storage.name = 'Timeseries Default'
                    AND org_storage.""default"" = false
                    AND proj_storage.project_id IS NOT NULL
                    AND proj_storage.name = 'Timeseries Default';
            ");

            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.object_storages
                WHERE project_id IS NULL
                    AND (
                        (name = 'Instance Default' AND ""default"" = true)
                        OR (name = 'Timeseries Default' AND ""default"" = false)
                    );
            ");
        }
    }
}
