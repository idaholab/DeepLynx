using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class TagsCollectionsTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
             // function to remove record_collection_tags rows when a tag is archived,
            // so archived tags no longer appear on record collections
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.unlink_archived_tag_from_record_collections()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.is_archived = TRUE
                       AND OLD.is_archived = FALSE THEN

                        DELETE FROM deeplynx.record_tags
                        WHERE tag_id = NEW.id;

                        DELETE FROM deeplynx.record_collection_tags
                        WHERE tag_id = NEW.id;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // trigger to remove record collection associations when a tag transitions to archived
            migrationBuilder.Sql(@"
                CREATE OR REPLACE TRIGGER unlink_archived_tag_from_record_collections_trigger
                AFTER UPDATE OF is_archived
                ON deeplynx.tags
                FOR EACH ROW
                EXECUTE FUNCTION deeplynx.unlink_archived_tag_from_record_collections();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS unlink_archived_tag_from_record_collections_trigger ON deeplynx.tags;");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS deeplynx.unlink_archived_tag_from_record_collections();");
        }
    }
}
