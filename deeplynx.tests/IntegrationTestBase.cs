using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.tests;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

// Fixture to allow setting up and breaking down what is needed for each test suite
public class TestSuiteFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;

    public TestSuiteFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    public string PostgresConnectionString { get; private set; }
    public string RedisConnectionString { get; private set; }
    public DeeplynxContext Context { get; private set; }

    // Runs at the beginning of every test suite
    public async Task InitializeAsync()
    {
        // Start containers
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();

        // Set up configuration for redis cache tests
        RedisConnectionString = _redisContainer.GetConnectionString();
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", RedisConnectionString);

        PostgresConnectionString = _postgresContainer.GetConnectionString();

        var options = new DbContextOptionsBuilder<DeeplynxContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        Context = new DeeplynxContext(options);

        // Apply migrations only once
        await Context.Database.MigrateAsync();

        // Apply env variables without exposing values in tests
        var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        var envFilePath = Path.Combine(projectRoot, ".env");
        Env.Load(envFilePath);
        // ensure the notification service is tested
        Environment.SetEnvironmentVariable("ENABLE_NOTIFICATION_SERVICE", "true");
    }

    // Runs at the end of every test suite
    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}

// Defines a test collection named "Test Suite Collection".
// This collection uses the TestSuiteFixture class for setup and teardown.
[CollectionDefinition("Test Suite Collection")]
public class TestSuiteCollection : ICollectionFixture<TestSuiteFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and ICollectionFixture<> interfaces.
}

// Indicates that this test class is part of the "Test Suite Collection".
// The TestSuiteFixture setup and teardown code will be applied to this class.
[Collection("Test Suite Collection")]
public class IntegrationTestBase : IAsyncLifetime
{
    private readonly TestSuiteFixture _fixture;

    protected IntegrationTestBase(TestSuiteFixture fixture)
    {
        _fixture = fixture;
        Context = new DeeplynxContext(new DbContextOptionsBuilder<DeeplynxContext>()
            .UseNpgsql(_fixture.PostgresConnectionString)
            .Options);
    }

    protected DeeplynxContext Context { get; }

    // Runs before every test in the test suite
    public virtual async Task InitializeAsync()
    {
        await SeedTestDataAsync();
    }

    // Runs after every test in the test suite
    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("CACHE_PROVIDER_TYPE", null);
        await Context.DisposeAsync();
        await CacheService.Instance.FlushAsync();
    }
    
    /// <summary>
    /// Switch cache type for testing - just create a new instance
    /// </summary>
    protected void SwitchCacheType(string cacheType)
    {
        Environment.SetEnvironmentVariable("CACHE_PROVIDER_TYPE", cacheType);
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", _fixture.RedisConnectionString);
        CacheService.ResetCacheService();
    }

    /// <summary>
    ///     Clean database between tests
    /// </summary>
    protected async Task CleanDatabaseAsync()
    {
        var subscriptions = await Context.Subscriptions.ToListAsync();
        Context.Subscriptions.RemoveRange(subscriptions);
        await Context.SaveChangesAsync();

        var actions = await Context.Actions.ToListAsync();
        Context.Actions.RemoveRange(actions);
        await Context.SaveChangesAsync();

        var events = await Context.Events.ToListAsync();
        Context.Events.RemoveRange(events);
        await Context.SaveChangesAsync();

        var tokens = await Context.OauthTokens.ToListAsync();
        Context.OauthTokens.RemoveRange(tokens);
        await Context.SaveChangesAsync();

        var apiKeys = await Context.ApiKeys.ToListAsync();
        Context.ApiKeys.RemoveRange(apiKeys);
        await Context.SaveChangesAsync();

        var oauthApplications = await Context.OauthApplications.ToListAsync();
        Context.OauthApplications.RemoveRange(oauthApplications);
        await Context.SaveChangesAsync();

        var savedSearches = await Context.SavedSearches.ToListAsync();
        Context.SavedSearches.RemoveRange(savedSearches);
        await Context.SaveChangesAsync();

        var permissions = await Context.Permissions.ToListAsync();
        Context.Permissions.RemoveRange(permissions);
        await Context.SaveChangesAsync();

        var edges = await Context.Edges.ToListAsync();
        Context.Edges.RemoveRange(edges);
        await Context.SaveChangesAsync();

        var relationships = await Context.Relationships.ToListAsync();
        Context.Relationships.RemoveRange(relationships);
        await Context.SaveChangesAsync();

        var tags = await Context.Tags.ToListAsync();
        Context.Tags.RemoveRange(tags);
        await Context.SaveChangesAsync();

        var records = await Context.Records.ToListAsync();
        Context.Records.RemoveRange(records);
        await Context.SaveChangesAsync();

        var classes = await Context.Classes.ToListAsync();
        Context.Classes.RemoveRange(classes);
        await Context.SaveChangesAsync();

        var dataSources = await Context.DataSources.ToListAsync();
        Context.DataSources.RemoveRange(dataSources);
        await Context.SaveChangesAsync();

        var projectMembers = await Context.ProjectMembers.ToListAsync();
        Context.ProjectMembers.RemoveRange(projectMembers);
        await Context.SaveChangesAsync();

        var roles = await Context.Roles.ToListAsync();
        Context.Roles.RemoveRange(roles);
        await Context.SaveChangesAsync();

        var objectStorages = await Context.ObjectStorages.ToListAsync();
        Context.ObjectStorages.RemoveRange(objectStorages);
        await Context.SaveChangesAsync();

        var projects = await Context.Projects.ToListAsync();
        Context.Projects.RemoveRange(projects);
        await Context.SaveChangesAsync();

        // Delete parent entities last
        var users = await Context.Users.ToListAsync();
        Context.Users.RemoveRange(users);
        await Context.SaveChangesAsync();

        var organizations = await Context.Organizations.ToListAsync();
        Context.Organizations.RemoveRange(organizations);
        await Context.SaveChangesAsync();

        await CacheService.Instance.FlushAsync();
    }

    protected virtual async Task SeedTestDataAsync()
    {
        await CleanDatabaseAsync();
    }
}