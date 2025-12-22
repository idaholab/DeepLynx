using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Moq;
using deeplynx.helpers;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class RedisCacheBusinessTests : IntegrationTestBase
{

    private long organizationId;
    public RedisCacheBusinessTests(TestSuiteFixture fixture) : base(fixture) { }
    
    public override async Task InitializeAsync()
    {
        // Set the cache provider
        SwitchCacheType("redis");
        await base.InitializeAsync();
    }

    [Fact]
    public async Task ConfirmTestingCorrectCacheType()
    {
        var type = CacheService.Instance.CacheType;
        Assert.True(type == "redis");
    }
    
    [Fact]
    public async Task RedisCacheImpl_TestSetAsyncAndGetCache()
    {
        // Arrange
        var key = "projects";
        var value = Context.Projects.Select(project => new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            LastUpdatedAt = project.LastUpdatedAt,
            LastUpdatedBy = project.LastUpdatedBy,
            IsArchived = project.IsArchived
        }).ToList();

        // Act
        await CacheService.Instance.SetAsync(key, value, (TimeSpan?)null);
        var cachedValue = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(key);
        
        // Assert
        Assert.Equal(value.Count, cachedValue.Count);
        for (int i = 0; i < value.Count; i++)
        {
            Assert.Equal(value[i].Id, cachedValue[i].Id);
            Assert.Equal(value[i].Name, cachedValue[i].Name);
            Assert.Equal(value[i].Description, cachedValue[i].Description);
            Assert.Equal(value[i].Abbreviation, cachedValue[i].Abbreviation);
            Assert.Equal(value[i].LastUpdatedAt, cachedValue[i].LastUpdatedAt);
            Assert.Equal(value[i].LastUpdatedBy, cachedValue[i].LastUpdatedBy);
            Assert.Equal(value[i].IsArchived, cachedValue[i].IsArchived);
        }
    }

    [Fact]
    public async Task RedisCacheImpl_TestDeleteCache()
    {
        // Arrange
        var key = "projects";
        var value = Context.Projects.Select(project => new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            LastUpdatedAt = project.LastUpdatedAt,
            LastUpdatedBy = project.LastUpdatedBy,
            IsArchived = project.IsArchived
        }).ToList();
        await CacheService.Instance.SetAsync(key, value, (TimeSpan?)null);

        // Act
        await CacheService.Instance.DeleteAsync(key);
        var cachedValue = await CacheService.Instance.GetAsync<string>(key);

        // Assert
        Assert.Null(cachedValue);
    }

    [Fact]
    public async Task RedisCacheImpl_TestFlushCache()
    {
        // Arrange
        var key1 = "projects-key1";
        var key2 = "projects-key2";
        var value = Context.Projects.Select(project => new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            LastUpdatedAt = project.LastUpdatedAt,
            LastUpdatedBy = project.LastUpdatedBy,
            IsArchived = project.IsArchived
        }).ToList();
        await CacheService.Instance.SetAsync(key1, value, (TimeSpan?)null);
        await CacheService.Instance.SetAsync(key2, value, (TimeSpan?)null);

        // Act
        await CacheService.Instance.FlushAsync();
        var cachedValue1 = await CacheService.Instance.GetAsync<string>(key1);
        var cachedValue2 = await CacheService.Instance.GetAsync<string>(key2);

        // Assert
        Assert.Null(cachedValue1);
        Assert.Null(cachedValue2);
    }
    
    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        
        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        // Add projects
        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test project 2 for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
    }
}