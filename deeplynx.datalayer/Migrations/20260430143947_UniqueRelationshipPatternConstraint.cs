using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class UniqueRelationshipPatternConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "origin_id", "name", "destination_id" },
                unique: true,
                filter: "project_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships");
        }
    }
}
