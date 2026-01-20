using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMountPathObjectStorages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE deeplynx.object_storages 
                SET config = jsonb_set(config, '{mountPath}', '""/data/duckdb""') 
                WHERE config->>'mountPath' = '../storage/';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE deeplynx.object_storages 
                SET config = jsonb_set(config, '{mountPath}', '""../storage/""') 
                WHERE config->>'mountPath' = '/data/duckdb';
            ");
        }
    }
}
