using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRelationshipClassProjectScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION deeplynx.validate_relationship_class_project_scope()
            RETURNS trigger AS $$
            BEGIN
                IF TG_OP = 'UPDATE'
                AND NEW.origin_id IS NOT DISTINCT FROM OLD.origin_id
                AND NEW.destination_id IS NOT DISTINCT FROM OLD.destination_id
                AND NEW.project_id IS NOT DISTINCT FROM OLD.project_id
                AND NEW.organization_id IS NOT DISTINCT FROM OLD.organization_id
                THEN
                    RETURN NEW;
                END IF;

                IF NEW.origin_id IS NOT NULL THEN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM deeplynx.classes c
                        WHERE c.id = NEW.origin_id
                        AND c.organization_id = NEW.organization_id
                        AND c.project_id IS NOT DISTINCT FROM NEW.project_id
                    ) THEN
                        RAISE EXCEPTION 'Origin class with ID % is not valid for this relationship scope.', NEW.origin_id;
                    END IF;
                END IF;

                IF NEW.destination_id IS NOT NULL THEN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM deeplynx.classes c
                        WHERE c.id = NEW.destination_id
                        AND c.organization_id = NEW.organization_id
                        AND c.project_id IS NOT DISTINCT FROM NEW.project_id
                    ) THEN
                        RAISE EXCEPTION 'Destination class with ID % is not valid for this relationship scope.', NEW.destination_id;
                    END IF;
                END IF;

                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS validate_relationship_class_project_scope_trigger
            ON deeplynx.relationships;

            CREATE TRIGGER validate_relationship_class_project_scope_trigger
            BEFORE INSERT OR UPDATE OF origin_id, destination_id, project_id, organization_id
            ON deeplynx.relationships
            FOR EACH ROW
            WHEN (
                NEW.origin_id IS NOT NULL
                OR NEW.destination_id IS NOT NULL
            )
            EXECUTE FUNCTION deeplynx.validate_relationship_class_project_scope();
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS validate_relationship_class_project_scope_trigger
            ON deeplynx.relationships;

            DROP FUNCTION IF EXISTS deeplynx.validate_relationship_class_project_scope();
            """);
        }
    }
}

