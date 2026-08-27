using Npgsql;

public static class PgvectorExtensionValidator
{
    /// <summary>
    /// Ensures that pgvector is available on the PostgreSQL server and enabled
    /// in the configured database. Enables it automatically when necessary.
    /// </summary>
    /// <param name="connectionString">Database connection credentials.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The enabled pgvector version.</returns>
    public static async Task<string> EnsureExtensionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var extensionInfo = await GetExtensionInfoAsync(
                connectionString,
                cancellationToken);

            if (extensionInfo.InstalledVersion is not null)
            {
                WriteSuccess(
                    $"pgvector extension {extensionInfo.InstalledVersion} " +
                    $"is enabled in database '{extensionInfo.DatabaseName}'");

                return extensionInfo.InstalledVersion;
            }

            Console.WriteLine(
                $"pgvector extension is not enabled in database " +
                $"'{extensionInfo.DatabaseName}' -- Attempting enable");

            var enabledVersion = await EnableExtensionAsync(
                connectionString,
                cancellationToken);

            WriteSuccess(
                $"pgvector extension {enabledVersion} was successfully enabled  " +
                $"in database '{extensionInfo.DatabaseName}'.");

            return enabledVersion;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                "The configured DeepLynx Nexus PostgreSQL database could not be reached.",
                ex);
        }
    }

    /// <summary>
    /// Enables pgvector in the configured database.
    /// Note: This method mutates the database.
    /// </summary>
    /// <param name="connectionString">Database connection credentials.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The enabled pgvector version.</returns>
    public static async Task<string> EnableExtensionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var availabilityCommand = connection.CreateCommand();
            availabilityCommand.CommandText = """
                SELECT default_version
                FROM pg_available_extensions
                WHERE name = 'vector';
                """;

            var availableVersion =
                await availabilityCommand.ExecuteScalarAsync(cancellationToken)
                as string;

            if (availableVersion is null)
            {
                throw new InvalidOperationException(
                    "pgvector is not available on the PostgreSQL server. " +
                    "Install pgvector on the database before starting " +
                    "the application");
            }

            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                "CREATE EXTENSION IF NOT EXISTS vector;";

            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = """
                SELECT extversion
                FROM pg_extension
                WHERE extname = 'vector';
                """;

            var installedVersion =
                await versionCommand.ExecuteScalarAsync(cancellationToken)
                as string;

            if (installedVersion is null)
            {
                throw new InvalidOperationException(
                    "CREATE EXTENSION completed, but pgvector was not found " +
                    $"in database '{connection.Database}'.");
            }

            return installedVersion;
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException(
                "pgvector is available on the PostgreSQL server but could not " +
                "be enabled in the configured database. Ensure the database " +
                "user has permission to run CREATE EXTENSION.",
                ex);
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                "The PostgreSQL database could not be reached while attempting " +
                "to enable pgvector.",
                ex);
        }
    }

    private static async Task<ExtensionInfo> GetExtensionInfoAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT extversion
            FROM pg_extension
            WHERE extname = 'vector';
            """;

        var installedVersion =
            await command.ExecuteScalarAsync(cancellationToken)
            as string;

        return new ExtensionInfo(
            connection.Database,
            connection.PostgreSqlVersion,
            installedVersion);
    }

    private static void WriteSuccess(string message)
    {
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {message}");
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    private sealed record ExtensionInfo(
        string DatabaseName,
        Version PostgreSqlVersion,
        string? InstalledVersion);
}