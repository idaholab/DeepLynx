using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ProvenanceRecordsImmutabilityTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // function + trigger to make provenance_records append-only: chain integrity
            // depends on rows never being altered or removed after insert.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION deeplynx.block_provenance_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'provenance_records is append-only: % is not permitted', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE TRIGGER block_provenance_mutation_trigger
                BEFORE UPDATE OR DELETE ON deeplynx.provenance_records
                FOR EACH ROW
                EXECUTE FUNCTION deeplynx.block_provenance_mutation();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS block_provenance_mutation_trigger ON deeplynx.provenance_records;");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS deeplynx.block_provenance_mutation();");
        }
    }
}
