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

            // Check PostgreSQL version
            var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SHOW server_version;";

            var versionString = (string)await versionCommand.ExecuteScalarAsync();
            var majorVersion = int.Parse(versionString.Split('.')[0]);

            // Check pgvector extension availability
            var pgvectorCommand = connection.CreateCommand();
            pgvectorCommand.CommandText = @"
                SELECT COUNT(*) 
                FROM pg_available_extensions 
                WHERE name = 'vector';";

            var pgvectorAvailable = (long)await pgvectorCommand.ExecuteScalarAsync() > 0;

            // Determine if upgrade is needed
            bool versionMismatch = majorVersion < RequiredPostgresVersion;
            bool pgvectorMissing = !pgvectorAvailable;

            if (versionMismatch || pgvectorMissing)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n==========================================");
                Console.WriteLine("❌ DATABASE REQUIREMENTS NOT MET");
                Console.WriteLine("==========================================\n");
                Console.ResetColor();

                if (versionMismatch)
                {
                    Console.WriteLine($"❌ PostgreSQL version: {majorVersion} (Required: {RequiredPostgresVersion}+)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ PostgreSQL version: {majorVersion}");
                    Console.ResetColor();
                }

                if (pgvectorMissing)
                {
                    Console.WriteLine($"❌ pgvector extension: Not available");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ pgvector extension: Available");
                    Console.ResetColor();
                }

                Console.WriteLine();
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

                var issues = new List<string>();
                if (versionMismatch) issues.Add($"PostgreSQL {majorVersion} (requires {RequiredPostgresVersion}+)");
                if (pgvectorMissing) issues.Add("pgvector extension not available");

                throw new InvalidOperationException(
                    $"Database requirements not met: {string.Join(", ", issues)}"
                );
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Database version check passed (PostgreSQL {majorVersion})");
            Console.WriteLine($"✓ pgvector extension available");
            Console.ResetColor();
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw version mismatch exceptions
        }
        catch (NpgsqlException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n==========================================");
            Console.WriteLine("❌ DATABASE CONNECTION FAILED");
            Console.WriteLine("==========================================\n");
            Console.ResetColor();

            Console.WriteLine($"Error: {ex.Message}\n");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Possible causes:");
            Console.ResetColor();
            Console.WriteLine("  • PostgreSQL is not running");
            Console.WriteLine("  • Database container is not started");
            Console.WriteLine("  • Connection string is incorrect");
            Console.WriteLine("  • Network/firewall blocking connection\n");

            Console.WriteLine("==========================================\n");

            throw new InvalidOperationException(
                "Cannot start application: Database connection failed. Ensure PostgreSQL is running.",
                ex
            );
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Unexpected error during database verification: {ex.Message}");
            Console.ResetColor();
            throw; // Re-throw unexpected exceptions to prevent startup
        }
    }
}