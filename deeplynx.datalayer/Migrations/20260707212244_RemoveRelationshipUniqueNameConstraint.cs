using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRelationshipUniqueNameConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "unique_organization_relationship_name",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropIndex(
                name: "unique_project_relationship_name",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_name_no_origin_destination",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL AND origin_id IS NULL AND destination_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "origin_id", "name", "destination_id" },
                unique: true,
                filter: "project_id IS NOT NULL AND origin_id IS NOT NULL AND destination_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "unique_project_relationship_name_no_origin_destination",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.DropIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships");

            migrationBuilder.CreateIndex(
                name: "unique_organization_relationship_name",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "project_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_name",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "unique_project_relationship_origin_name_destination",
                schema: "deeplynx",
                table: "relationships",
                columns: new[] { "organization_id", "project_id", "origin_id", "name", "destination_id" },
                unique: true,
                filter: "project_id IS NOT NULL");
        }
    }
}
