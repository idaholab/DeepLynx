using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class OntologyVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ontology_vector",
                schema: "dl_vector",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vector = table.Column<Vector>(type: "vector", nullable: false),
                    class_id = table.Column<long>(type: "bigint", nullable: true),
                    relationship_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ontology_vectors_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ontology_vectors_class_id_fkey",
                        column: x => x.class_id,
                        principalSchema: "deeplynx",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ontology_vectors_relationship_id_fkey",
                        column: x => x.relationship_id,
                        principalSchema: "deeplynx",
                        principalTable: "relationships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ontology_vectors_class_id",
                schema: "dl_vector",
                table: "ontology_vector",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_ontology_vectors_id",
                schema: "dl_vector",
                table: "ontology_vector",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_ontology_vectors_relationship_id",
                schema: "dl_vector",
                table: "ontology_vector",
                column: "relationship_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ontology_vector",
                schema: "dl_vector");
        }
    }
}
