using System.Text.Json;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class SavedSearchBusinessTests : IntegrationTestBase
{
    private SavedSearchBusiness _savedSearchBusiness = null!;
    private QueryBusiness _queryBusiness = null!;
    private SensitivityLabelService _sensitivityLabelService = null!;
    private long pid; // project ID

    private long uid1; // user IDs
    private long uid2;

    public SavedSearchBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _queryBusiness = new QueryBusiness(Context, _sensitivityLabelService);
        _savedSearchBusiness = new SavedSearchBusiness(Context, _queryBusiness);
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
        var result = await _savedSearchBusiness.SaveSearch(uid1, alias, textSearch, filters);

        // Assert
        Assert.True(result);

        var savedSearch = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid1 && s.Name == alias);

        Assert.NotNull(savedSearch);
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
        Assert.Equal(textSearch, searchData.TextSearch);
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
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "clone", filters2);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result.Items);
        Assert.Equal(2, result.Items.Count);

        Assert.Contains(result.Items, s => s.Query.TextSearch == "captain");
        Assert.Contains(result.Items, s => s.Query.TextSearch == "clone");
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
        Assert.Single(result.Items);
        Assert.Equal("test1", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_Success_ReturnsEmptyListWhenNoSearches()
    {
        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1);

        // Assert
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
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
        Assert.NotNull(result.Items);
        Assert.Single(result.Items);

        var savedSearch = result.Items[0];
        Assert.Equal("search text", savedSearch.Query.TextSearch);
        Assert.NotNull(savedSearch.Query.Filter);
        Assert.Equal(2, savedSearch.Query.Filter.Length);

        // Verify first filter
        Assert.Null(savedSearch.Query.Filter[0].Connector);
        Assert.Equal("name", savedSearch.Query.Filter[0].Filter);
        Assert.Equal("LIKE", savedSearch.Query.Filter[0].Operator);
        Assert.Equal("Captain", savedSearch.Query.Filter[0].Value);

        // Verify second filter
        Assert.Equal("OR", savedSearch.Query.Filter[1].Connector);
        Assert.Equal("name", savedSearch.Query.Filter[1].Filter);
        Assert.Equal("=", savedSearch.Query.Filter[1].Operator);
        Assert.Equal("Rex", savedSearch.Query.Filter[1].Value);
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
        Assert.NotNull(result.Items);
        Assert.Single(result.Items);
        Assert.Equal("just text", result.Items[0].Query.TextSearch);
        Assert.NotNull(result.Items[0].Query.Filter);
        Assert.Empty(result.Items[0].Query.Filter);
    }

    #endregion

    #region GetSavedSearchesFilters

    [Fact]
    public async Task GetSavedSearches_FilterOnName_ReturnsMatchingResults()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Clone Trooper Search", "rex", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Jedi Search", "yoda", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Clone Commander Search", "cody", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto { Name = "clone" };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, s => s.Query.TextSearch == "rex");
        Assert.Contains(result.Items, s => s.Query.TextSearch == "cody");
        Assert.DoesNotContain(result.Items, s => s.Query.TextSearch == "yoda");
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnName_IsCaseInsensitive()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Clone Trooper Search", "rex", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Jedi Search", "yoda", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto { Name = "CLONE" };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("rex", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnName_ReturnsEmptyWhenNoMatch()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Clone Trooper Search", "rex", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto { Name = "Sith" };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnTextSearch_ReturnsMatchingResults()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "clone trooper", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "jedi master", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 3", "clone commander", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto { TextSearch = "clone" };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, s => s.Query.TextSearch == "clone trooper");
        Assert.Contains(result.Items, s => s.Query.TextSearch == "clone commander");
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnTextSearch_IsCaseInsensitive()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "Clone Trooper", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "jedi master", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto { TextSearch = "CLONE" };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Clone Trooper", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnLastUpdatedBefore_ReturnsMatchingResults()
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

        var pastDate = DateTime.UtcNow.AddDays(-10);
        var futureDate = DateTime.UtcNow.AddDays(10);

        await _savedSearchBusiness.SaveSearch(uid1, "Old Search", "old", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "New Search", "new", filters);

        // Manually set LastUpdatedAt on the old search to simulate an older record
        var oldSearch = await Context.SavedSearches.FirstAsync(s => s.UserId == uid1 && s.Name == "Old Search");
        oldSearch.LastUpdatedAt = DateTime.SpecifyKind(pastDate, DateTimeKind.Unspecified);
        await Context.SaveChangesAsync();

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto
        {
            LastUpdatedBefore = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-5), DateTimeKind.Unspecified)
        };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("old", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_FilterOnLastUpdatedAfter_ReturnsMatchingResults()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Old Search", "old", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "New Search", "new", filters);

        // Manually set LastUpdatedAt on the old search to simulate an older record
        var oldSearch = await Context.SavedSearches.FirstAsync(s => s.UserId == uid1 && s.Name == "Old Search");
        oldSearch.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-10), DateTimeKind.Unspecified);
        await Context.SaveChangesAsync();

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto
        {
            LastUpdatedAfter = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-5), DateTimeKind.Unspecified)
        };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("new", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_CombinedFilters_ReturnsMatchingResults()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Clone Search", "clone trooper", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Clone Old Search", "clone commander", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Jedi Search", "jedi master", filters);

        // Age the "Clone Old Search" record
        var oldSearch = await Context.SavedSearches.FirstAsync(s => s.UserId == uid1 && s.Name == "Clone Old Search");
        oldSearch.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-10), DateTimeKind.Unspecified);
        await Context.SaveChangesAsync();

        // Filter by name "clone" AND only recently updated records
        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto
        {
            Name = "clone",
            LastUpdatedAfter = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-5), DateTimeKind.Unspecified)
        };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("clone trooper", result.Items[0].Query.TextSearch);
    }

    [Fact]
    public async Task GetSavedSearches_NullFilters_ReturnsAllUserSearches()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "rex", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "cody", filters);

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, null);

        // Assert
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetSavedSearches_Pagination_ReturnsCorrectPage()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "text1", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "text2", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 3", "text3", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 4", "text4", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 5", "text5", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto
        {
            PageNumber = 2,
            PageSize = 2
        };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetSavedSearches_Pagination_LastPageReturnsRemainder()
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

        await _savedSearchBusiness.SaveSearch(uid1, "Search 1", "text1", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 2", "text2", filters);
        await _savedSearchBusiness.SaveSearch(uid1, "Search 3", "text3", filters);

        var searchFilters = new SavedSearchRequestDtos.FilterSavedQueryRequestDto
        {
            PageNumber = 2,
            PageSize = 2
        };

        // Act
        var result = await _savedSearchBusiness.GetSavedSearches(uid1, searchFilters);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }

    #endregion

    #region ExecuteSavedSearch Tests

    [Fact]
    public async Task ExecuteSavedSearch_InvalidId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _savedSearchBusiness.ExecuteSavedSearch(99999, uid1, pid, [pid]));

        Assert.Contains("Saved Search does not exist", exception.Message);
    }

    [Fact]
    public async Task ExecuteSavedSearch_WrongUser_ThrowsKeyNotFoundException()
    {
        // Arrange - Save a search under uid1
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
        await _savedSearchBusiness.SaveSearch(uid1, "User 1 Search", "test", filters);

        var savedSearch = await Context.SavedSearches
            .FirstAsync(s => s.UserId == uid1 && s.Name == "User 1 Search");

        // Act & Assert - uid2 attempts to execute uid1's saved search
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _savedSearchBusiness.ExecuteSavedSearch(savedSearch.Id, uid2, pid, [pid]));

        Assert.Contains("Saved Search does not exist", exception.Message);
    }

    [Fact]
    public async Task ExecuteSavedSearch_CorruptedSearchJson_ThrowsArgumentException()
    {
        // Arrange - Manually insert a saved search with invalid/empty filter JSON
        var badSearch = new SavedSearch
        {
            UserId = uid1,
            Name = "Bad Search",
            Search = JsonSerializer.Serialize(new { TextSearch = "test", Filter = (object)null })
        };
        Context.SavedSearches.Add(badSearch);
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _savedSearchBusiness.ExecuteSavedSearch(badSearch.Id, uid1, pid, [pid]));

        Assert.Contains("invalid or empty query", exception.Message);
    }

    [Fact]
    public async Task ExecuteSavedSearch_ValidSearch_ReturnsResults()
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
        await _savedSearchBusiness.SaveSearch(uid1, "Valid Search", "test", filters);

        var savedSearch = await Context.SavedSearches
            .FirstAsync(s => s.UserId == uid1 && s.Name == "Valid Search");

        // Act
        var result = await _savedSearchBusiness.ExecuteSavedSearch(
            savedSearch.Id, uid1, pid, [pid], isSysAdmin: true);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region DeleteSavedSearch Tests

    [Fact]
    public async Task DeleteSavedSearch_Success_DeletesSavedSearch()
    {
        // Arrange
        var filters = new[]
        {
        new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "Rex"
        }
    };
        await _savedSearchBusiness.SaveSearch(uid1, "Search To Delete", "rex", filters);

        var savedSearch = await Context.SavedSearches
            .FirstAsync(s => s.UserId == uid1 && s.Name == "Search To Delete");

        // Act
        var result = await _savedSearchBusiness.DeleteSavedSearch(uid1, savedSearch.Id);

        // Assert
        Assert.True(result);
        var deleted = await Context.SavedSearches.FirstOrDefaultAsync(s => s.Id == savedSearch.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteSavedSearch_InvalidId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _savedSearchBusiness.DeleteSavedSearch(uid1, 99999));

        Assert.Contains("Saved search not found", exception.Message);
    }

    [Fact]
    public async Task DeleteSavedSearch_WrongUser_ThrowsKeyNotFoundException()
    {
        // Arrange - Save a search under uid1
        var filters = new[]
        {
        new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "Rex"
        }
    };
        await _savedSearchBusiness.SaveSearch(uid1, "User 1 Search", "rex", filters);

        var savedSearch = await Context.SavedSearches
            .FirstAsync(s => s.UserId == uid1 && s.Name == "User 1 Search");

        // Act & Assert - uid2 attempts to delete uid1's saved search
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _savedSearchBusiness.DeleteSavedSearch(uid2, savedSearch.Id));

        Assert.Contains("Saved search not found", exception.Message);
    }

    [Fact]
    public async Task DeleteSavedSearch_Success_DoesNotDeleteOtherUserSearches()
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
        await _savedSearchBusiness.SaveSearch(uid1, "User 1 Search", "rex", filters);
        await _savedSearchBusiness.SaveSearch(uid2, "User 2 Search", "cody", filters);

        var uid1Search = await Context.SavedSearches
            .FirstAsync(s => s.UserId == uid1 && s.Name == "User 1 Search");

        // Act
        await _savedSearchBusiness.DeleteSavedSearch(uid1, uid1Search.Id);

        // Assert - uid2's search should be untouched
        var uid2Search = await Context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == uid2 && s.Name == "User 2 Search");
        Assert.NotNull(uid2Search);
    }

    #endregion
}