using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class QueryRecordView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW deeplynx.query_records AS
				SELECT r.id, r.uri, r.properties, r.original_id, r.name, r.description, r.class_id, c.name AS class_name, 
					r.data_source_id, d.name AS data_source_name, r.object_storage_id, o.name AS object_storage_name,
					r.project_id, p.name AS project_name, r.organization_id, 
					r.file_type, r.file_size, 
					jsonb_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL) AS tags,
					jsonb_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL) AS labels,
					r.last_updated_at, r.last_updated_by, r.is_archived
				FROM deeplynx.records r
				LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
				LEFT JOIN deeplynx.tags t on t.id = rt.tag_id
				LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
				LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
				LEFT JOIN deeplynx.classes c ON c.id = r.class_id
				LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
				JOIN deeplynx.data_sources d ON d.id = r.data_source_id
				JOIN deeplynx.projects p ON p.id = r.project_id
				GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id, r.class_id, r.data_source_id,
					r.project_id, r.organization_id, r.object_storage_id, r.last_updated_by, r.last_updated_at,
					r.is_archived, r.file_type, r.file_size, c.name, d.name, p.name, o.name;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql(@"DROP VIEW IF EXISTS deeplynx.query_records");
        }
    }
}
