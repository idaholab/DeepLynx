using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

// No namespace on purpose - this matches TestSuiteFixture and IntegrationTestBase,
// which also live in the global namespace so every test file can use them without
// an extra "using" statement.

/// <summary>
///     Boots the real application - the exact same Program.cs that runs in production -
///     in memory, so tests can send real HTTP requests through the real routing,
///     API versioning, and exception-handling middleware.
///
///     Unlike the controller tests in deeplynx.tests/Controllers (which call controller
///     methods directly in C# and never touch routing), tests built on this factory are
///     true black-box HTTP tests: they only know what a real client would know.
///
///     This does NOT start its own database or cache. It points the app at the same
///     Postgres/Redis containers that <see cref="TestSuiteFixture"/> already started for
///     the rest of the suite, and enables the app's own local-development authentication
///     bypass (see NexusAuthenticationMiddleware) so tests don't need to generate real
///     signed JWTs.
/// </summary>
/// <example>
///     using var factory = new NexusWebApplicationFactory(fixture);
///     using var client = factory.CreateClient();
///     var response = await client.GetAsync("/api/v1/health");
/// </example>
public class NexusWebApplicationFactory : WebApplicationFactory<Program>
{
    public NexusWebApplicationFactory(TestSuiteFixture fixture)
    {
        // Program.cs reads all of the settings below from Environment variables directly
        // (not from IConfiguration), so we set them the same way TestSuiteFixture already
        // does for Redis. This must happen before CreateClient() is called, because that's
        // what actually triggers Program.cs to run and read them.

        SetPostgresEnvironmentVariables(fixture.PostgresConnectionString);
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", fixture.RedisConnectionString);

        // Program.cs requires these to be non-empty at startup, but their actual values
        // don't matter here: DISABLE_BACKEND_AUTHENTICATION below means no real token is
        // ever validated against them.
        Environment.SetEnvironmentVariable("JWT_ISSUER", "https://test.local");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-secret-key-not-used-for-real-auth");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "test-audience");

        // The important one: with no Authorization header on the request, this makes
        // NexusAuthenticationMiddleware log the caller in as a local sysadmin user
        // instead of requiring a real signed JWT. Being a sysadmin also means every
        // [Auth]/[SysAdmin]/[OrgAdmin]/[ProjectAdmin] check in AuthMiddleware passes
        // automatically, so tests don't need to seed roles or permission grants.
        Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    /// <summary>
    ///     Program.cs (via ConnectionStringsProvider.GetPostgresConnectionString) builds its
    ///     Postgres connection string from five individual environment variables rather than
    ///     one connection string. This splits the Testcontainers connection string into those
    ///     same five pieces so the app talks to the same database the rest of the suite uses.
    /// </summary>
    private static void SetPostgresEnvironmentVariables(string postgresConnectionString)
    {
        var parsed = new NpgsqlConnectionStringBuilder(postgresConnectionString);

        Environment.SetEnvironmentVariable("POSTGRES_USER", parsed.Username);
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", parsed.Password);
        Environment.SetEnvironmentVariable("POSTGRES_DB_HOST", parsed.Host);
        Environment.SetEnvironmentVariable("POSTGRES_PORT", parsed.Port.ToString());
        Environment.SetEnvironmentVariable("POSTGRES_DB_NAME", parsed.Database);
    }
}