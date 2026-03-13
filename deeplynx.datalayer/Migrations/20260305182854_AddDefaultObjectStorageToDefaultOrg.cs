using System.Text.Json;
using DotNetEnv;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultObjectStorageToDefaultOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Load environment variables
            try
            {
                Env.Load("../.env");
            }
            catch
            {
                // .env file might not exist in all environments
            }
            
            var fileStorageMethod = Environment.GetEnvironmentVariable("FILE_STORAGE_METHOD");
            
            string defaultObjectStorageMethod = "filesystem"; // Default to filesystem
            string configJson = null;
            
            // Try to create object storage based on configured method
            if (!string.IsNullOrWhiteSpace(fileStorageMethod))
            {
                if (fileStorageMethod == "filesystem")
                {
                    var mountPath = Environment.GetEnvironmentVariable("STORAGE_DIRECTORY");
                    
                    if (!string.IsNullOrWhiteSpace(mountPath))
                    {
                        configJson = JsonSerializer.Serialize(new { MountPath = mountPath });
                    }
                }
                else if (fileStorageMethod == "azure_object")
                {
                    var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_OBJECT_CONNECTION_STRING");
                    var azureContainerName = Environment.GetEnvironmentVariable("AZURE_CONTAINER_NAME");
                    
                    if (!string.IsNullOrWhiteSpace(azureConnectionString) && !string.IsNullOrWhiteSpace(azureContainerName))
                    {
                        defaultObjectStorageMethod = "azure_object";
                        configJson = JsonSerializer.Serialize(new 
                        { 
                            AzureObjectConfig = new 
                            {
                                AzureConnectionString = azureConnectionString,
                                AzureContainerName = azureContainerName
                            }
                        });
                    }
                }
                else if (fileStorageMethod == "aws_s3")
                {
                    var awsConnectionString = Environment.GetEnvironmentVariable("AWS_S3_CONNECTION_STRING");
                    
                    if (!string.IsNullOrWhiteSpace(awsConnectionString))
                    {
                        defaultObjectStorageMethod = "aws_s3";
                        configJson = JsonSerializer.Serialize(new { AwsConnectionString = awsConnectionString });
                    }
                }
            }
            
            // If configJson is still null, use filesystem fallback
            if (configJson == null)
            {
                // Read mount path from environment variable with fallback logic
                var mountPath = "";
                if (File.Exists("../.env"))
                {
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STORAGE_DIRECTORY")))
                    {
                        mountPath = Environment.GetEnvironmentVariable("STORAGE_DIRECTORY");
                    }
                    else
                    {
                        mountPath = "../data/duckdb";
                    }
                }
                else
                {
                    mountPath = "/data/duckdb";
                }
                
                defaultObjectStorageMethod = "filesystem";
                configJson = JsonSerializer.Serialize(new { MountPath = mountPath });
            }

            // Escape single quotes in JSON for SQL
            var escapedConfigJson = configJson.Replace("'", "''");

            // Insert default object storage for all organizations that don't have one
            migrationBuilder.Sql($@"
                INSERT INTO deeplynx.object_storages (
                    name,
                    type,
                    config,
                    ""default"",
                    organization_id,
                    project_id,
                    is_archived,
                    last_updated_at,
                    last_updated_by
                )
                SELECT 
                    'Instance Default',
                    '{defaultObjectStorageMethod}',
                    '{escapedConfigJson}'::jsonb,
                    true,
                    o.id,
                    NULL,
                    false,
                    NOW(),
                    NULL
                FROM deeplynx.organizations o
                WHERE NOT EXISTS (
                    SELECT 1 
                    FROM deeplynx.object_storages os 
                    WHERE os.organization_id = o.id 
                    AND os.""default"" = true
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migrations typically cannot be safely rolled back because we cannot 
            // reliably distinguish between records added by this migration vs. records 
            // added later through normal application flow.
            // 
            // If rollback is required, manually remove the object storages:
            // DELETE FROM deeplynx.object_storages 
            // WHERE name = 'Instance Default' 
            // AND "default" = true
            // AND organization_id IN (SELECT id FROM organizations WHERE [your criteria]);
            
            // No-op: Manual intervention required for rollback
        }
    }
}
