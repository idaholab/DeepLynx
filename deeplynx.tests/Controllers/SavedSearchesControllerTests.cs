using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class SavedSearchesControllerTests : IDisposable
{
    private readonly Mock<ISavedSearchBusiness> _mockSavedSearchBusiness;
    private readonly Mock<ILogger<SavedSearchController>> _mockLogger;
    private readonly SavedSearchController _savedSearchController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private static readonly string Alias = "aliasString";
    private static readonly string TextSearch = "searchString";
    private const long SavedSearchId = 22L;
    private readonly CustomQueryDtos.CustomQueryRequestDto[] Filters =
    {
        new()
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "test-record",
            Json = null
        },
        new()
        {
            Connector = "OR",
            Filter = "status",
            Operator = "!=",
            Value = "archived",
            Json = null
        }
    };
    private readonly SavedSearchRequestDtos.FilterSavedQueryRequestDto SearchFilter = new()
    {
        Name = "test-query",
        TextSearch = "record search",
        LastUpdatedBefore = DateTime.UtcNow.AddDays(1),
        LastUpdatedAfter = DateTime.UtcNow.AddDays(-7),
        PageNumber = 1,
        PageSize = 25
    };


    public SavedSearchesControllerTests()
    {
        _mockSavedSearchBusiness = new Mock<ISavedSearchBusiness>();
        _mockLogger = new Mock<ILogger<SavedSearchController>>();

        _savedSearchController = new SavedSearchController(
            _mockSavedSearchBusiness.Object,
            _mockLogger.Object
        );

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    // =========================================================================
    // SaveSearch Tests
    // =========================================================================

    #region SaveSearch Tests

    [Fact]
    public async Task SaveSearch_Returns200_WithBool()
    {
        // Arrange


        _mockSavedSearchBusiness
            .Setup(b => b.SaveSearch(UserId, Alias, TextSearch, Filters))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _savedSearchController.SaveSearch(TextSearch, Alias, Filters);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SaveSearch_Returns500_OnUnexpectedException()
    {
        // Arrange

        _mockSavedSearchBusiness
            .Setup(b => b.SaveSearch(UserId, Alias, TextSearch, Filters))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _savedSearchController.SaveSearch(TextSearch, Alias, Filters);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task SaveSearch_PassesToBusinessLayer()
    {
        // Arrange

        _mockSavedSearchBusiness
            .Setup(b => b.SaveSearch(UserId, Alias, TextSearch, Filters))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _savedSearchController.SaveSearch(TextSearch, Alias, Filters);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.SaveSearch(UserId, Alias, TextSearch, Filters),
            Times.Once);
    }

    [Fact]
    public void SaveSearch_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(SavedSearchController.SaveSearch),
            "textSearch",
            "alias",
            "filterArray");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // GetSavedSearches Tests
    // =========================================================================

    #region GetSavedSearches Tests

    [Fact]
    public async Task GetSavedSearches_Returns200_WithSavedSearches()
    {
        // Arrange
        var expected = new PaginatedResponse<SavedSearchResponseDto>();
        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearches(UserId, SearchFilter))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearches(
            SearchFilter);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSavedSearches_Returns200_WithEmptyResponse()
    {
        // Arrange

        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearches(UserId, SearchFilter))
            .ReturnsAsync((PaginatedResponse<SavedSearchResponseDto>)null!);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearches(
            SearchFilter);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetSavedSearches_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearches(UserId, SearchFilter))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _savedSearchController.GetSavedSearches(
            SearchFilter);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetSavedSearches_PassesToBusinessLayer()
    {
        // Arrange
        var expected = new PaginatedResponse<SavedSearchResponseDto>();

        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearches(UserId, SearchFilter))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearches(
            SearchFilter);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.GetSavedSearches(UserId, SearchFilter),
            Times.Once);
    }

    [Fact]
    public void GetSavedSearches_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(SavedSearchController.GetSavedSearches),
            "searchFilters");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // GetSavedSearchById Tests
    // =========================================================================

    #region GetSavedSearchById Tests

    [Fact]
    public async Task GetSavedSearchById_Returns200_WithSavedSearches()
    {
        // Arrange
        var expected = new SavedSearchResponseDto();
        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearchById(UserId, SavedSearchId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearchById(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSavedSearchById_Returns200_WithEmptyResponse()
    {
        // Arrange

        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearchById(UserId, SavedSearchId))
            .ReturnsAsync((SavedSearchResponseDto)null!);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearchById(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetSavedSearchById_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearchById(UserId, SavedSearchId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _savedSearchController.GetSavedSearchById(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetSavedSearchById_PassesToBusinessLayer()
    {
        // Arrange
        var expected = new SavedSearchResponseDto();

        _mockSavedSearchBusiness
            .Setup(b => b.GetSavedSearchById(UserId, SavedSearchId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.GetSavedSearchById(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.GetSavedSearchById(UserId, SavedSearchId),
            Times.Once);
    }

    [Fact]
    public void GetSavedSearchById_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SavedSearchController.GetSavedSearchById),
            "savedSearchId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // ExecuteSavedSearch Tests
    // =========================================================================

    #region ExecuteSavedSearch Tests

    [Fact]
    public async Task ExecuteSavedSearch_Returns200_WithDtoList()
    {
        // Arrange
        IEnumerable<QueryRecordViewResponseDto> expected =
        new List<QueryRecordViewResponseDto>();

        _mockSavedSearchBusiness
            .Setup(b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.ExecuteSavedSearch(
            OrgId,
            ProjectList,
            SavedSearchId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task ExecuteSavedSearchReturns500_OnUnexpectedException()
    {
        _mockSavedSearchBusiness
            .Setup(b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, false, false, false))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _savedSearchController.ExecuteSavedSearch(
            OrgId,
            ProjectList,
            SavedSearchId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task ExecuteSavedSearch_PassesToBusinessLayer()
    {
        // Arrange
        IEnumerable<QueryRecordViewResponseDto> expected =
        new List<QueryRecordViewResponseDto>();

        _mockSavedSearchBusiness
            .Setup(b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.ExecuteSavedSearch(
            OrgId,
            ProjectList,
            SavedSearchId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, false, false, false),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteSavedSearch_PassesAdminFlagTrueToBusinessLayer()
    {
        // Arrange
        IEnumerable<QueryRecordViewResponseDto> expected =
        new List<QueryRecordViewResponseDto>();

        UserContextStorage.IsSysAdmin = true;

        _mockSavedSearchBusiness
            .Setup(b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, true, false, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _savedSearchController.ExecuteSavedSearch(
            OrgId,
            ProjectList,
            SavedSearchId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.ExecuteSavedSearch(SavedSearchId, UserId, OrgId, ProjectList, true, false, false),
            Times.Once);
    }

    [Fact]
    public void ExecuteSavedSearch_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SavedSearchController.ExecuteSavedSearch),
            "organizationId",
            "projectIds",
            "savedSearchId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteSavedSearch Tests
    // =========================================================================

    #region DeleteSavedSearch Tests

    [Fact]
    public async Task DeleteSavedSearch_Returns200()
    {
        // Arrange

        _mockSavedSearchBusiness
            .Setup(b => b.DeleteSavedSearch(UserId, SavedSearchId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _savedSearchController.DeleteSavedSearch(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal(true, result.Value);
    }


    [Fact]
    public async Task DeleteSavedSearch_Returns500_OnUnexpectedException()
    {
        _mockSavedSearchBusiness
            .Setup(b => b.DeleteSavedSearch(UserId, SavedSearchId))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _savedSearchController.DeleteSavedSearch(
            SavedSearchId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteSavedSearch_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSavedSearchBusiness
            .Setup(b => b.DeleteSavedSearch(UserId, SavedSearchId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _savedSearchController.DeleteSavedSearch(
            SavedSearchId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockSavedSearchBusiness.Verify(
            b => b.DeleteSavedSearch(UserId, SavedSearchId),
            Times.Once);
    }

    [Fact]
    public void DeleteSavedSearch_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(SavedSearchController.DeleteSavedSearch),
            "savedSearchId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================


    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(SavedSearchController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}