using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class LatticeFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "properties",
                schema: "deeplynx",
                table: "relationships",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "properties",
                schema: "deeplynx",
                table: "edges",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "properties",
                schema: "deeplynx",
                table: "classes",
                type: "jsonb",
                nullable: true);
            
             migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.get_lattice_relationships(
                    p_organization_id BIGINT, p_project_id BIGINT)
                RETURNS TABLE(
                    origin_class_name TEXT,
                    relationship_name TEXT,
                    relationship_description TEXT,
                    relationship_properties JSONB,
                    destination_class_name TEXT
                )
                LANGUAGE plpgsql AS $$
                BEGIN
                    RETURN QUERY
                    SELECT DISTINCT ON (
                        oc.name, r.name, r.description, dc.name
                    )
                        oc.name AS origin_class_name,
                        r.name AS relationship_name,
                        r.description AS relationship_description,
                        r.properties AS relationship_properties,
                        dc.name AS destination_class_name
                    FROM deeplynx.relationships r
                        LEFT JOIN deeplynx.classes oc ON r.origin_id = oc.id
                        LEFT JOIN deeplynx.classes dc ON r.destination_id = dc.id
                    WHERE r.organization_id = p_organization_id
                        AND r.project_id = p_project_id;
                END;
            $$;");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.get_lattice_classes(
                    p_organization_id BIGINT, p_project_id BIGINT)
                RETURNS TABLE(
                    class_name TEXT,
                    class_description TEXT,
                    class_properties JSONB
                )
                LANGUAGE plpgsql AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        c.name AS class_name,
                        c.description AS class_description,
                        c.properties AS class_properties
                    FROM deeplynx.classes c 
                    WHERE c.organization_id = p_organization_id 
                        AND c.project_id = p_project_id;
                END;
            $$;");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.get_lattice_edges(
                    p_organization_id BIGINT, p_project_id BIGINT)
                RETURNS TABLE(
                    origin_name TEXT,
                    origin_class_name TEXT,
                    relationship_name TEXT,
                    edge_properties JSONB,
                    destination_name TEXT,
                    destination_class_name TEXT
                )
                LANGUAGE plpgsql AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        o.name AS origin_name,
                        oc.name AS origin_class_name,
                        r.name AS relationship_name,
                        e.properties AS edge_properties,
                        d.name AS destination_name,
                        dc.name AS destination_class_name
                    FROM deeplynx.edges e
                        LEFT JOIN deeplynx.relationships r ON e.relationship_id = r.id
                        LEFT JOIN deeplynx.records o ON e.origin_id = o.id
                        LEFT JOIN deeplynx.records d ON e.destination_id = d.id
                        LEFT JOIN deeplynx.classes oc ON o.class_id = oc.id
                        LEFT JOIN deeplynx.classes dc ON d.class_id = dc.id
                    WHERE e.organization_id = p_organization_id
                        AND e.project_id = p_project_id;
                END;
            $$;");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.get_lattice_records(
                    p_organization_id BIGINT, p_project_id BIGINT)
                RETURNS TABLE(
                    record_name TEXT,
                    record_description TEXT,
                    record_properties JSONB,
                    class_name TEXT
                )
                LANGUAGE plpgsql AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        rec.name AS record_name,
                        rec.description AS record_description,
                        rec.properties AS record_properties,
                        c.name AS class_name
                    FROM deeplynx.records rec
                        LEFT JOIN deeplynx.classes c ON rec.class_id = c.id
                    WHERE rec.organization_id = p_organization_id
                        AND rec.project_id = p_project_id;
                END;
            $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS deeplynx.get_lattice_classes;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS deeplynx.get_lattice_relationships;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS deeplynx.get_lattice_records;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS deeplynx.get_lattice_edges;");
            
            migrationBuilder.DropColumn(
                name: "properties",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "properties",
                schema: "deeplynx",
                table: "edges");

            migrationBuilder.DropColumn(
                name: "properties",
                schema: "deeplynx",
                table: "classes");
        }
    }
}
