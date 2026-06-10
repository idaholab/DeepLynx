using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class UserAccountTypesAndUniqueUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_type",
                schema: "deeplynx",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "human");

            // Resolve duplicate usernames before enforcing uniqueness. Keep the
            // earliest (lowest id) row per username untouched and suffix the rest
            // with an 8-char GUID fragment. NULL usernames are left alone (Postgres
            // treats NULLs as distinct in a unique index).
            migrationBuilder.Sql(@"
                UPDATE deeplynx.users
                SET username = username || '_' || left(gen_random_uuid()::text, 8)
                WHERE username IS NOT NULL
                  AND id NOT IN (
                      SELECT MIN(id) FROM deeplynx.users
                      WHERE username IS NOT NULL
                      GROUP BY username
                  );");

            migrationBuilder.CreateIndex(
                name: "idx_users_username",
                schema: "deeplynx",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_users_username",
                schema: "deeplynx",
                table: "users");

            migrationBuilder.DropColumn(
                name: "account_type",
                schema: "deeplynx",
                table: "users");
        }
    }
}
