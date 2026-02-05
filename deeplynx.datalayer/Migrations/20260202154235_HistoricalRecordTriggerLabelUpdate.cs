using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalRecordTriggerLabelUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "labels",
                schema: "deeplynx",
                table: "historical_records",
                type: "jsonb",
                nullable: true);
            
            // Update the historical_records_trigger_func to include sensitivity labels
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags, labels,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        NEW.id, NEW.uri, NEW.name, NEW.description, NEW.properties, NEW.original_id,
                        NEW.class_id, NEW.data_source_id, NEW.project_id, NEW.organization_id, NEW.object_storage_id,
                        NEW.last_updated_by, NEW.last_updated_at, NEW.is_archived,
                        json_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        json_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
                    LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = NEW.id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
            
            // Update the historical_records_insert_tag_trigger_func to include sensitivity labels
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_insert_tag_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags, labels,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        NEW.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        json_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
                    LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = NEW.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Update the historical_records_delete_tag_trigger_func to include sensitivity labels
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_delete_tag_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags, labels,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        OLD.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        json_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
                    LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = OLD.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function for label insertions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_insert_label_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags, labels,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        NEW.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        json_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
                    LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = NEW.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger for label insertions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE TRIGGER historical_records_insert_label_trigger
                AFTER INSERT OR UPDATE ON deeplynx.record_labels
                FOR EACH ROW
                EXECUTE FUNCTION deeplynx.historical_records_insert_label_trigger_func();
            ");

            // Create function for label deletions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_delete_label_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags, labels,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        OLD.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        json_agg(DISTINCT jsonb_build_object('id', sl.id, 'name', sl.name)) FILTER (WHERE sl.id IS NOT NULL AND sl.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.record_labels rl ON r.id = rl.record_id
                    LEFT JOIN deeplynx.sensitivity_labels sl ON sl.id = rl.label_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = OLD.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger for label deletions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE TRIGGER historical_records_delete_label_trigger
                AFTER DELETE ON deeplynx.record_labels
                FOR EACH ROW
                EXECUTE FUNCTION deeplynx.historical_records_delete_label_trigger_func();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop label triggers
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS historical_records_delete_label_trigger ON deeplynx.record_labels;");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS deeplynx.historical_records_delete_label_trigger_func();");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS historical_records_insert_label_trigger ON deeplynx.record_labels;");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS deeplynx.historical_records_insert_label_trigger_func();");

            migrationBuilder.DropColumn(
                name: "labels",
                schema: "deeplynx",
                table: "historical_records");
            
            // Revert to previous version of historical_records_trigger_func (with organization_id but without labels)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        NEW.id, NEW.uri, NEW.name, NEW.description, NEW.properties, NEW.original_id,
                        NEW.class_id, NEW.data_source_id, NEW.project_id, NEW.organization_id, NEW.object_storage_id,
                        NEW.last_updated_by, NEW.last_updated_at, NEW.is_archived,
                        json_agg(jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = NEW.id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Revert to previous version of historical_records_insert_tag_trigger_func
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_insert_tag_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        NEW.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = NEW.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Revert to previous version of historical_records_delete_tag_trigger_func
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.historical_records_delete_tag_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Insert the new historical record
                    INSERT INTO deeplynx.historical_records (
                        record_id, uri, name, description, properties, original_id,
                        class_id, data_source_id, project_id, organization_id, object_storage_id,
                        last_updated_by, last_updated_at, is_archived, tags,
                        class_name, data_source_name, project_name, object_storage_name)
                    SELECT
                        OLD.record_id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, LOCALTIMESTAMP, r.is_archived,
                        json_agg(jsonb_build_object('id', t.id, 'name', t.name)) FILTER (WHERE t.id IS NOT NULL AND t.name IS NOT NULL),
                        c.name, d.name, p.name, o.name
                    FROM deeplynx.records r
                    LEFT JOIN deeplynx.record_tags rt ON r.id = rt.record_id
                    LEFT JOIN deeplynx.tags t ON t.id = rt.tag_id
                    LEFT JOIN deeplynx.classes c ON c.id = r.class_id
                    LEFT JOIN deeplynx.object_storages o ON o.id = r.object_storage_id
                    JOIN deeplynx.data_sources d ON d.id = r.data_source_id
                    JOIN deeplynx.projects p ON p.id = r.project_id
                    WHERE r.id = OLD.record_id
                    GROUP BY r.id, r.uri, r.name, r.description, r.properties, r.original_id,
                        r.class_id, r.data_source_id, r.project_id, r.organization_id, r.object_storage_id,
                        r.last_updated_by, r.last_updated_at, r.is_archived, c.name, d.name, p.name, o.name;
                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }
    }
}