using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSLPermissionsLookupTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensitivity_label_permission_actions",
                schema: "deeplynx",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sensitivity_label_permission_actions_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_sensitivity_label_permission_actions_id",
                schema: "deeplynx",
                table: "sensitivity_label_permission_actions",
                column: "id");
            
            migrationBuilder.Sql(@"
                INSERT INTO deeplynx.sensitivity_label_permission_actions(name, description)
                VALUES  ('read record', 'Permission to read records with the given label'),
                        ('create record', 'Permission to create records with the given label'),
                        ('update record', 'Permission to update records with the given label'),
                        ('delete record', 'Permission to delete records with the given label'),
                        ('download file', 'Permission to download files with the given label'),
                        ('upload file', 'Permission to upload files with the given label'),
                        ('update file', 'Permission to update files with the given label'),
                        ('delete file', 'Permission to delete files with the given label');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensitivity_label_permission_actions",
                schema: "deeplynx");
        }
    }
}
