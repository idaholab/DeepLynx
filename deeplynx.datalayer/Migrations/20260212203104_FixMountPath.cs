using Microsoft.EntityFrameworkCore.Migrations;
using DotNetEnv;


#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class FixMountPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Read mount path from environment variable
            Env.Load("../.env");
            var mountPath = Environment.GetEnvironmentVariable("STORAGE_DIRECTORY") ?? "/data/duckdb";
            
            // Escape the path for JSON
            var escapedPath = mountPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            // Step 1: Update Instance Default mount path to value from env variable
            migrationBuilder.Sql($@"
                UPDATE deeplynx.object_storages
                SET config = '{{""MountPath"": ""{escapedPath}"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}}'::jsonb
                WHERE name = 'Instance Default'
                    AND ""default"" = true
                    AND type = 'filesystem'
                    AND project_id IS NULL;
            ");

            // Step 2: Remap all records from Timeseries Default to Instance Default
            migrationBuilder.Sql(@"
                UPDATE deeplynx.records r
                SET object_storage_id = instance_storage.id
                FROM deeplynx.object_storages timeseries_storage
                JOIN deeplynx.object_storages instance_storage 
                    ON timeseries_storage.organization_id = instance_storage.organization_id
                WHERE r.object_storage_id = timeseries_storage.id
                    AND timeseries_storage.project_id IS NULL
                    AND timeseries_storage.name = 'Timeseries Default'
                    AND timeseries_storage.""default"" = false
                    AND instance_storage.project_id IS NULL
                    AND instance_storage.name = 'Instance Default'
                    AND instance_storage.""default"" = true;
            ");

            // Step 3: Delete Timeseries Default storages
            migrationBuilder.Sql(@"
                DELETE FROM deeplynx.object_storages
                WHERE project_id IS NULL
                    AND name = 'Timeseries Default'
                    AND ""default"" = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Revert Instance Default mount path
            migrationBuilder.Sql(@"
                UPDATE deeplynx.object_storages
                SET config = '{""MountPath"": ""../storage/"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb
                WHERE name = 'Instance Default'
                    AND ""default"" = true
                    AND type = 'filesystem'
                    AND project_id IS NULL;
            ");

            // Step 2: Recreate Timeseries Default for each organization
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.object_storages 
                    (name, type, config, project_id, organization_id, ""default"", last_updated_at, last_updated_by, is_archived)
                SELECT DISTINCT
                    'Timeseries Default',
                    'filesystem',
                    '{""MountPath"": ""/data/duckdb"", ""AzureObjectConfig"": null, ""AwsConnectionString"": null}'::jsonb,
                    NULL::bigint,
                    organization_id,
                    false,
                    NOW(),
                    1,
                    false
                FROM deeplynx.object_storages
                WHERE project_id IS NULL
                    AND name = 'Instance Default'
                    AND ""default"" = true;
            ");
        }
    }
}
