using System.Text.Json;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class SavedSearchBusinessTests : IntegrationTestBase
{
    private SavedSearchBusiness _savedSearchBusiness = null!;
    private long pid; // project ID

    private long uid1; // user IDs
    private long uid2;

    public SavedSearchBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _savedSearchBusiness = new SavedSearchBusiness(Context);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create test users
        var user1 = new User
        {
            Name = "Test User 1",
            Email = "user1@test.com",
            Username = "testuser1",
            IsActive = true
        };
        var user2 = new User
        {
            Name = "Test User 2",
            Email = "user2@test.com",
            Username = "testuser2",
            IsActive = true
        };

        Context.Users.AddRange(user1, user2);
        await Context.SaveChangesAsync();

        uid1 = user1.Id;
        uid2 = user2.Id;

        // Create a test organization
        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();

        // Create a test project
        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project for saved searches",
            OrganizationId = organization.Id
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;
    }

    #region SaveSearch Tests

    [Fact]
    public async Task SaveSearch_Success_SavesSearchWithTextAndFilters()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "LIKE",
                Value = "test"
            }
        };
        var alias = "My Test Search";
        var textSearch = "test query";

        // Act
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);
        Assert.Equal(alias, savedSearch.Name);
        Assert.Equal(uid1, savedSearch.UserId);
        Assert.False(savedSearch.IsFavorite);
        Assert.NotNull(savedSearch.Search);
    }

    [Fact]
    public async Task SaveSearch_Success_SavesSearchAsFavorite()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "Captain Rex"
            }
        };
        var alias = "My Favorite Search";
        var textSearch = "Captain";

        // Act
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters, true);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);
        Assert.True(savedSearch.IsFavorite);
    }

    [Fact]
    public async Task SaveSearch_Success_SavesMultipleFilters()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = null,
                Filter = "name",
                Operator = "LIKE",
                Value = "Captain"
            },
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "original_id",
                Operator = "LIKE",
                Value = "CT-"
            }
        };
        var alias = "Complex Search";
        var textSearch = "clone trooper";

        // Act
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);

        // Deserialize and verify filters were saved correctly
        var searchData = JsonSerializer.Deserialize<CustomQueryDtos.CustomQueryResponseDto>(savedSearch.Search);
        Assert.NotNull(searchData);
        Assert.Equal(textSearch, searchData.textSearch);
        Assert.Equal(2, searchData.Filter.Length);
    }

    [Fact]
    public async Task SaveSearch_Success_SavesWithEmptyTextSearch()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "Tech"
            }
        };
        var alias = "Filter Only Search";
        var textSearch = "";

        // Act
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);
    }

    [Fact]
    public async Task SaveSearch_Success_SavesWithNullTextSearch()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "Hunter"
            }
        };
        var alias = "Null Text Search";
        string textSearch = null;

        // Act
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);
    }

    [Fact]
    public async Task SaveSearch_Failure_ThrowsExceptionIfFiltersNull()
    {
        // Arrange
        var alias = "Invalid Search";
        var textSearch = "test";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, null));

        Assert.Contains("Query filters cannot be null", exception.Message);
    }

    [Fact]
    public async Task SaveSearch_Success_AllowsMultipleSavedSearchesPerUser()
    {
        // Arrange
        var filters1 = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "Rex"
            }
        };
        var filters2 = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "Leia"
            }
        };

        // Act
        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "rex", filters1);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "leia", filters2);

        // Assert
        var savedSearches = await Context.SavedSearches
            .Where(s => s.UserId == uid1)
            .ToListAsync();

        Assert.Equal(2, savedSearches.Count);
    }

    #endregion

    #region GetSavedSearches Tests

    [Fact]
    public async Task GetSavedSearches_Success_ReturnsAllUserSearches()
    {
        // Arrange
        var filters1 = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "LIKE",
                Value = "Captain"
            }
        };
        var filters2 = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "original_id",
                Operator = "LIKE",
                Value = "CT-"
            }
        };

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "captain", filters1);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "clone", filters2, true);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.Contains(result, s => s.textSearch == "captain");
        Assert.Contains(result, s => s.textSearch == "clone");
    }

    [Fact]
    public async Task GetSavedSearches_Success_ReturnsOnlyUserSearches()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "AND",
                Filter = "name",
                Operator = "=",
                Value = "test"
            }
        };

        await _savedSearchBusiness.SaveSearch(uid1, "User 1 Search", "test1", filters);
        await _savedSearchBusiness.SaveSearch(uid2, "User 2 Search", "test2", filters);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test1", result[0].textSearch);
    }

    [Fact]
    public async Task GetSavedSearches_Success_ReturnsEmptyListWhenNoSearches()
    {
        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSavedSearches_Success_PreservesFilterStructure()
    {
        // Arrange
        var filters = new[]
        {
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = null,
                Filter = "name",
                Operator = "LIKE",
                Value = "Captain"
            },
            new CustomQueryDtos.CustomQueryRequestDto
            {
                Connector = "OR",
                Filter = "name",
                Operator = "=",
                Value = "Rex"
            }
        };

        await _savedSearchBusiness.SaveSearch(uid1, "Complex Search", "search text", filters);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var savedSearch = result[0];
        Assert.Equal("search text", savedSearch.textSearch);
        Assert.NotNull(savedSearch.Filter);
        Assert.Equal(2, savedSearch.Filter.Length);

        // Verify first filter
        Assert.Null(savedSearch.Filter[0].Connector);
        Assert.Equal("name", savedSearch.Filter[0].Filter);
        Assert.Equal("LIKE", savedSearch.Filter[0].Operator);
        Assert.Equal("Captain", savedSearch.Filter[0].Value);

        // Verify second filter
        Assert.Equal("OR", savedSearch.Filter[1].Connector);
        Assert.Equal("name", savedSearch.Filter[1].Filter);
        Assert.Equal("=", savedSearch.Filter[1].Operator);
        Assert.Equal("Rex", savedSearch.Filter[1].Value);
    }

    [Fact]
    public async Task GetSavedSearches_Success_HandlesEmptyFiltersArray()
    {
        // Arrange
        var filters = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();

        await _savedSearchBusiness.SaveSearch(uid1, "Empty Filters", "just text", filters);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("just text", result[0].textSearch);
        Assert.NotNull(result[0].Filter);
        Assert.Empty(result[0].Filter);
    }

    #endregion
}