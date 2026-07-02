using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class ServiceBooleanAndCreatedByApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_service_account",
                schema: "deeplynx",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "created_by",
                schema: "deeplynx",
                table: "api_keys",
                type: "bigint",
                nullable: true);

            // De-duplicate existing usernames before enforcing uniqueness. For any non-null
            // username shared by more than one row, keep the earliest (lowest id) as-is and
            // append a 6-character GUID-derived suffix to each subsequent duplicate so the
            // unique index below can be created without conflict.
            migrationBuilder.Sql(@"
                UPDATE deeplynx.users AS u
                SET username = u.username || '_' || left(replace(gen_random_uuid()::text, '-', ''), 6)
                FROM (
                    SELECT id,
                           row_number() OVER (PARTITION BY username ORDER BY id) AS rn
                    FROM deeplynx.users
                    WHERE username IS NOT NULL
                ) AS dup
                WHERE u.id = dup.id AND dup.rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "idx_users_username",
                schema: "deeplynx",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_api_keys_created_by",
                schema: "deeplynx",
                table: "api_keys",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "api_keys_created_by_fkey",
                schema: "deeplynx",
                table: "api_keys",
                column: "created_by",
                principalSchema: "deeplynx",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "api_keys_created_by_fkey",
                schema: "deeplynx",
                table: "api_keys");

            migrationBuilder.DropIndex(
                name: "idx_users_username",
                schema: "deeplynx",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_api_keys_created_by",
                schema: "deeplynx",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "is_service_account",
                schema: "deeplynx",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "deeplynx",
                table: "api_keys");
        }
    }
}
