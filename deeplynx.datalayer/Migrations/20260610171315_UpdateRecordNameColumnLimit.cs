using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRecordNameColumnLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail fast instead of waiting indefinitely for DDL locks.
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");
            migrationBuilder.Sql("SET LOCAL statement_timeout = '5min';");

            // Enforce the 500-character name limit for new/updated rows only.
            // Existing rows can be validated later with:
            // ALTER TABLE deeplynx.records VALIDATE CONSTRAINT ck_records_name_length_500;
            migrationBuilder.Sql(@"
                ALTER TABLE deeplynx.records
                ADD CONSTRAINT ck_records_name_length_500
                CHECK (name IS NULL OR char_length(name) <= 500)
                NOT VALID;
            ");

            migrationBuilder.Sql(@"
                DROP VIEW deeplynx.query_records;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW deeplynx.query_records AS
                SELECT
                    r.id,
                    r.uri,
                    r.properties,
                    r.original_id,
                    r.name,
                    r.description,
                    r.class_id,
                    c.name AS class_name,
                    r.data_source_id,
                    d.name AS data_source_name,
                    r.object_storage_id,
                    o.name AS object_storage_name,
                    r.project_id,
                    p.name AS project_name,
                    r.organization_id,
                    r.file_type,
                    r.file_size,
                    COALESCE(tags.tags, '[]'::jsonb) AS tags,
                    COALESCE(labels.labels, '[]'::jsonb) AS labels,
                    r.last_updated_at,
                    r.last_updated_by,
                    r.is_archived
                FROM deeplynx.records r
                    LEFT JOIN deeplynx.classes c
                        ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o
                        ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d
                        ON d.id = r.data_source_id
                    JOIN deeplynx.projects p
                        ON p.id = r.project_id
                    LEFT JOIN LATERAL (
                        SELECT jsonb_agg(
                            DISTINCT jsonb_build_object('id', t.id, 'name', t.name)
                        ) AS tags
                        FROM deeplynx.record_tags rt
                        JOIN deeplynx.tags t
                            ON t.id = rt.tag_id
                        WHERE rt.record_id = r.id
                          AND t.name IS NOT NULL
                    ) tags ON true
                    LEFT JOIN LATERAL (
                        SELECT jsonb_agg(
                            DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)
                        ) AS labels
                        FROM deeplynx.record_labels rl
                        JOIN deeplynx.sensitivity_labels sl
                            ON sl.id = rl.label_id
                        WHERE rl.record_id = r.id
                          AND sl.name IS NOT NULL
                    ) labels ON true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");
            migrationBuilder.Sql("SET LOCAL statement_timeout = '5min';");

            migrationBuilder.Sql(@"
                DROP VIEW deeplynx.query_records;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE deeplynx.records
                DROP CONSTRAINT IF EXISTS ck_records_name_length_500;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW deeplynx.query_records AS
                SELECT
                    r.id,
                    r.uri,
                    r.properties,
                    r.original_id,
                    r.name,
                    r.description,
                    r.class_id,
                    c.name AS class_name,
                    r.data_source_id,
                    d.name AS data_source_name,
                    r.object_storage_id,
                    o.name AS object_storage_name,
                    r.project_id,
                    p.name AS project_name,
                    r.organization_id,
                    r.file_type,
                    r.file_size,
                    COALESCE(tags.tags, '[]'::jsonb) AS tags,
                    COALESCE(labels.labels, '[]'::jsonb) AS labels,
                    r.last_updated_at,
                    r.last_updated_by,
                    r.is_archived
                FROM deeplynx.records r
                    LEFT JOIN deeplynx.classes c
                        ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o
                        ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d
                        ON d.id = r.data_source_id
                    JOIN deeplynx.projects p
                        ON p.id = r.project_id
                    LEFT JOIN LATERAL (
                        SELECT jsonb_agg(
                            DISTINCT jsonb_build_object('id', t.id, 'name', t.name)
                        ) AS tags
                        FROM deeplynx.record_tags rt
                        JOIN deeplynx.tags t
                            ON t.id = rt.tag_id
                        WHERE rt.record_id = r.id
                          AND t.name IS NOT NULL
                    ) tags ON true
                    LEFT JOIN LATERAL (
                        SELECT jsonb_agg(
                            DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)
                        ) AS labels
                        FROM deeplynx.record_labels rl
                        JOIN deeplynx.sensitivity_labels sl
                            ON sl.id = rl.label_id
                        WHERE rl.record_id = r.id
                          AND sl.name IS NOT NULL
                    ) labels ON true;
            ");
        }
    }
}