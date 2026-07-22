using Npgsql;

public static class PgvectorExtensionValidator
{
    public static async Task EnsureExtensionAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var availabilityCommand = connection.CreateCommand();
            availabilityCommand.CommandText = """
                SELECT default_version
                FROM pg_available_extensions
                WHERE name = 'vector';
                """;

            var availableVersion = await availabilityCommand.ExecuteScalarAsync() as string;
            if (availableVersion is null)
            {
                throw new InvalidOperationException(
                    "pgvector is not available on the configured PostgreSQL server. " +
                    "Install pgvector on the database host, or use postgres Docker service with the extension."
                );
            }

            await using var installedCommand = connection.CreateCommand();
            installedCommand.CommandText = """
                SELECT extversion
                FROM pg_extension
                WHERE extname = 'vector';
                """;

            var installedVersion = await installedCommand.ExecuteScalarAsync() as string;
            if (installedVersion is null)
            {
                try
                {
                    await using var createCommand = connection.CreateCommand();
                    createCommand.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
                    await createCommand.ExecuteNonQueryAsync();
                }
                catch (PostgresException ex)
                {
                    throw new InvalidOperationException(
                        "pgvector is available but could not be enabled in the configured database. " +
                        "Ensure the database user can run CREATE EXTENSION, or have an administrator enable the vector extension.",
                        ex
                    );
                }

                installedVersion = availableVersion;
            }

            Console.WriteLine(
                $"pgvector extension {installedVersion} is enabled " +
                $"(PostgreSQL {connection.PostgreSqlVersion.Major})."
            );
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                "Cannot verify pgvector because the configured PostgreSQL database could not be reached.",
                ex
            );
        }
    }
}
