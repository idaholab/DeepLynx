using Microsoft.EntityFrameworkCore;
using Npgsql;

public class DatabaseVersionChecker
{
    private const int RequiredPostgresVersion = 18;

    public static async Task CheckDatabaseVersion(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SHOW server_version;";

            var versionString = (string)await command.ExecuteScalarAsync();
            var majorVersion = int.Parse(versionString.Split('.')[0]);

            if (majorVersion < RequiredPostgresVersion)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n==========================================");
                Console.WriteLine("❌ DATABASE VERSION MISMATCH");
                Console.WriteLine("==========================================\n");
                Console.ResetColor();

                Console.WriteLine($"Your PostgreSQL version: {majorVersion}");
                Console.WriteLine($"Required version: {RequiredPostgresVersion} or higher\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ACTION REQUIRED:");
                Console.ResetColor();
                Console.WriteLine("Run the migration script to upgrade your database:\n");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  ./Dockerfiles/database/migrate_pg_local.sh\n");
                Console.ResetColor();

                Console.WriteLine("This will:");
                Console.WriteLine("  1. Backup your existing data");
                Console.WriteLine("  2. Upgrade to PostgreSQL 18 and install pgvector");
                Console.WriteLine("  3. Restore your data\n");

                Console.WriteLine("==========================================\n");

                throw new InvalidOperationException(
                    $"Database version mismatch: Found PostgreSQL {majorVersion}, requires {RequiredPostgresVersion}+"
                );
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Database version check passed (PostgreSQL {majorVersion})");
            Console.ResetColor();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Warning: Could not verify database version: {ex.Message}");
            Console.ResetColor();
        }
    }
}