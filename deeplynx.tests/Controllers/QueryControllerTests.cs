using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="QueryController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class QueryControllerTests : IDisposable
{
    private readonly Mock<IQueryBusiness> _mockQueryBusiness;
    private readonly Mock<ILogger<QueryController>> _mockLogger;
    private readonly QueryController _QueryController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private const long UserId = 10L;
    private const long RecordIdConst = 7L;
    private const string ViewName = "test-view";
    private const long partNumber = 1L;
    private const string Query = "SELECT UserId, OrgId, FROM table_name;";
    public QueryControllerTests()
    {
        _mockQueryBusiness = new Mock<IQueryBusiness>();
        _mockLogger = new Mock<ILogger<QueryController>>();

        _QueryController = new QueryController(
            _mockQueryBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        // Reset to safe sentinels so a mutated value never bleeds into another class's tests
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    // =========================================================================
    // SearchRecords Tests
    // =========================================================================

    #region SearchRecords Tests

    [Fact]
    public async Task SearchRecords_Returns200_WithRecordResponse()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness.Setup(b => b.Search(
            UserId, Query, OrgId, ProjectList, false, false, false, false))
            .ReturnsAsync(expected);

        var result = (await _QueryController.SearchRecords(
            OrgId,
            Query,
            ProjectList,
            true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task SearchRecords_Returns200_WithEmptyList()
    {

        _mockQueryBusiness.Setup(b => b.Search(
            UserId, Query, OrgId, ProjectList, false, false, false, false))
            .ReturnsAsync([]);

        var result = (await _QueryController.SearchRecords(
            OrgId,
            Query,
            ProjectList,
            true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task SearchRecords_Returns500_UnexpectedException()
    {


        _mockQueryBusiness.Setup(b => b.Search(
            UserId, Query, OrgId, ProjectList, true, false, false, false))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _QueryController.SearchRecords(
            OrgId,
            Query,
            ProjectList,
            true);

        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task SearchRecords_PassesToBusinessLayer()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness.Setup(b => b.Search(
            UserId, Query, OrgId, ProjectList, true, false, false, false))
            .ReturnsAsync(expected);

        var result = (await _QueryController.SearchRecords(
            OrgId,
            Query,
            ProjectList,
            true)).Result as OkObjectResult;


        _mockQueryBusiness.Verify(
            b => b.Search(
                UserId,
                Query,
                OrgId,
                ProjectList,
                true,
                false,
                false,
                false),
            Times.Once);
    }

    [Fact]
    public void SearchRecords_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(QueryController.SearchRecords),
            "userQuery");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // QueryBuilder Tests
    // =========================================================================

    #region QueryBuilder Tests

    [Fact]
    public async Task QueryBuilder_Returns200_WithRecordResponse()
    {
        // Arrange
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();

        _mockQueryBusiness
            .Setup(b => b.QueryBuilder(
                UserId,
                request,
                OrgId,
                ProjectList,
                Query,
                false,
                false,
                false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _QueryController.QueryBuilder(
            OrgId,
            Query,
            ProjectList,
            request);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task QueryBuilder_Returns200_WithEmptyList()
    {
        // Arrange
        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();

        _mockQueryBusiness
            .Setup(b => b.QueryBuilder(
                UserId,
                request,
                OrgId,
                ProjectList,
                Query,
                false,
                false,
                false))
            .ReturnsAsync(new List<QueryRecordViewResponseDto>());

        // Act
        var actionResult = await _QueryController.QueryBuilder(
            OrgId,
            Query,
            ProjectList,
            request);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        Assert.Equal(200, result.StatusCode);

        var records = Assert.IsAssignableFrom<IEnumerable<QueryRecordViewResponseDto>>(result.Value);
        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Returns500_UnexpectedException()
    {
        // Arrange
        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();

        _mockQueryBusiness
            .Setup(b => b.QueryBuilder(
                UserId,
                request,
                OrgId,
                ProjectList,
                Query,
                false,
                false,
                false))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _QueryController.QueryBuilder(
            OrgId,
            Query,
            ProjectList,
            request);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task QueryBuilder_PassesToBusinessLayer()
    {
        // Arrange
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();

        _mockQueryBusiness
            .Setup(b => b.QueryBuilder(
                UserId,
                request,
                OrgId,
                ProjectList,
                Query,
                false,
                false,
                false))
            .ReturnsAsync(expected);

        var result = (await _QueryController.QueryBuilder(
            OrgId,
            Query,
            ProjectList,
            request)).Result as OkObjectResult;


        _mockQueryBusiness.Verify(
            b => b.QueryBuilder(
                UserId,
                request,
                OrgId,
                ProjectList,
                Query,
                false,
                false,
                false),
            Times.Once);
    }

    [Fact]
    public async Task QueryBuilderPaginated_Returns200_WithPaginatedResponse()
    {
        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();
        var paginatedDto = new PaginatedRequestDto { PageNumber = 1, PageSize = 25 };
        var expected = new PaginatedResponse<QueryRecordViewResponseDto>
        {
            Items = new List<QueryRecordViewResponseDto>(),
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockQueryBusiness
            .Setup(b => b.QueryBuilderPaginated(
                UserId,
                request,
                OrgId,
                ProjectList,
                paginatedDto,
                Query,
                false,
                false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.QueryBuilderPaginated(
            OrgId,
            Query,
            ProjectList,
            paginatedDto,
            request);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task QueryBuilderPaginated_PassesToBusinessLayer()
    {
        var request = Array.Empty<CustomQueryDtos.CustomQueryRequestDto>();
        var paginatedDto = new PaginatedRequestDto { PageNumber = 2, PageSize = 10 };
        var expected = new PaginatedResponse<QueryRecordViewResponseDto>();

        _mockQueryBusiness
            .Setup(b => b.QueryBuilderPaginated(
                UserId,
                request,
                OrgId,
                ProjectList,
                paginatedDto,
                Query,
                false,
                false))
            .ReturnsAsync(expected);

        await _QueryController.QueryBuilderPaginated(
            OrgId,
            Query,
            ProjectList,
            paginatedDto,
            request);

        _mockQueryBusiness.Verify(
            b => b.QueryBuilderPaginated(
                UserId,
                request,
                OrgId,
                ProjectList,
                paginatedDto,
                Query,
                false,
                false),
            Times.Once);
    }

    [Fact]
    public void QueryBuilderPaginated_HasAdvancedPaginatedHttpPost()
    {
        var method = GetControllerMethod(
            nameof(QueryController.QueryBuilderPaginated),
            "filterArray",
            "paginatedDto");

        var httpPost = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "HttpPostAttribute");
        Assert.Equal("records/advanced/paginated", httpPost.ConstructorArguments[0].Value);
    }

    [Fact]
    public void QueryBuilder_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(QueryController.QueryBuilder),
            "filterArray");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // GetMultiProjectRecords Tests
    // =========================================================================

    #region GetMultiProjectRecords Tests

    [Fact]
    public async Task GetMultiProjectRecords_Returns200_WithRecordResponse()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness
            .Setup(b => b.GetMultiProjectRecords(
                UserId, OrgId, ProjectList, false, false, false, false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.GetMultiProjectRecords(
            OrgId,
            ProjectList,
            true);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetMultiProjectRecords_Returns200_WithEmptyList()
    {

        _mockQueryBusiness
            .Setup(b => b.GetMultiProjectRecords(
                UserId, OrgId, ProjectList, false, false, false, false))
            .ReturnsAsync([]);

        var actionResult = await _QueryController.GetMultiProjectRecords(
            OrgId,
            ProjectList,
            true);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task GetMultiProjectRecords_Returns500_UnexpectedException()
    {
        _mockQueryBusiness
            .Setup(b => b.GetMultiProjectRecords(
                UserId, OrgId, ProjectList, true, false, false, false))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _QueryController.GetMultiProjectRecords(
            OrgId,
            ProjectList,
            true);

        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetMultiProjectRecords_PassesToBusinessLayer()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness
            .Setup(b => b.GetMultiProjectRecords(
                UserId, OrgId, ProjectList, true, false, false, false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.GetMultiProjectRecords(
            OrgId,
            ProjectList,
            true);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);

        _mockQueryBusiness.Verify(
            b => b.GetMultiProjectRecords(
                UserId, OrgId, ProjectList, true, false, false, false),
            Times.Once);
    }

    [Fact]
    public void GetMultiProjectRecords_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(QueryController.GetMultiProjectRecords),
            "organizationId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetRecentlyAddedRecords Tests
    // =========================================================================

    #region GetRecentlyAddedRecords Tests

    [Fact]
    public async Task GetRecentlyAddedRecords_Returns200_WithRecordResponse()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness
            .Setup(b => b.GetRecentlyAddedRecords(
                UserId, OrgId, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.GetRecentlyAddedRecords(
            OrgId,
            ProjectList);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_Returns200_WithEmptyList()
    {

        _mockQueryBusiness
            .Setup(b => b.GetRecentlyAddedRecords(
                UserId, OrgId, ProjectList, false, false, false))
            .ReturnsAsync([]);

        var actionResult = await _QueryController.GetRecentlyAddedRecords(
            OrgId,
            ProjectList);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_Returns500_UnexpectedException()
    {

        _mockQueryBusiness
            .Setup(b => b.GetRecentlyAddedRecords(
                UserId, OrgId, ProjectList, false, false, false))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _QueryController.GetRecentlyAddedRecords(
            OrgId,
            ProjectList);

        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_PassesToBusinessLayer()
    {
        IEnumerable<QueryRecordViewResponseDto> expected =
            new List<QueryRecordViewResponseDto>();

        _mockQueryBusiness
            .Setup(b => b.GetRecentlyAddedRecords(
                UserId, OrgId, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.GetRecentlyAddedRecords(
            OrgId,
            ProjectList);

        var result = actionResult.Result as OkObjectResult;


        _mockQueryBusiness.Verify(
            b => b.GetRecentlyAddedRecords(
                UserId, OrgId, ProjectList, false, false, false),
            Times.Once);
    }

    [Fact]
    public void GetRecentlyAddedRecords_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(QueryController.GetRecentlyAddedRecords),
            "organizationId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetRecordsPaginated Tests
    // =========================================================================

    #region GetRecordsPaginated Tests

    [Fact]
    public async Task GetRecordsPaginated_Returns200_WithPaginatedResponse()
    {
        var expected = new PaginatedResponse<QueryRecordViewResponseDto>();
        var paginatedDto = new PaginatedRequestDto { PageNumber = 1, PageSize = 25 };

        _mockQueryBusiness
            .Setup(b => b.GetRecordsPaginated(
                UserId, OrgId, SortRecordsRequestDto.NameAZ, paginatedDto, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        var actionResult = await _QueryController.GetRecordsPaginated(
            OrgId, ProjectList, SortRecordsRequestDto.NameAZ, paginatedDto);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordsPaginated_Returns200_WithEmptyResponse()
    {
        var paginatedDto = new PaginatedRequestDto { PageNumber = 1, PageSize = 25 };

        _mockQueryBusiness
            .Setup(b => b.GetRecordsPaginated(
                UserId, OrgId, SortRecordsRequestDto.NameAZ, paginatedDto, ProjectList, false, false, false))
            .ReturnsAsync(new PaginatedResponse<QueryRecordViewResponseDto>());

        var actionResult = await _QueryController.GetRecordsPaginated(
            OrgId, ProjectList, SortRecordsRequestDto.NameAZ, paginatedDto);

        var result = actionResult.Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task GetRecordsPaginated_Returns500_UnexpectedException()
    {
        var paginatedDto = new PaginatedRequestDto { PageNumber = 1, PageSize = 25 };

        _mockQueryBusiness
            .Setup(b => b.GetRecordsPaginated(
                UserId, OrgId, SortRecordsRequestDto.NameAZ, paginatedDto, ProjectList, false, false, false))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _QueryController.GetRecordsPaginated(
            OrgId, ProjectList, SortRecordsRequestDto.NameAZ, paginatedDto);

        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetRecordsPaginated_PassesToBusinessLayer()
    {
        var expected = new PaginatedResponse<QueryRecordViewResponseDto>();
        var paginatedDto = new PaginatedRequestDto { PageNumber = 1, PageSize = 25 };

        _mockQueryBusiness
            .Setup(b => b.GetRecordsPaginated(
                UserId, OrgId, SortRecordsRequestDto.NameAZ, paginatedDto, ProjectList, false, false, false))
            .ReturnsAsync(expected);

        await _QueryController.GetRecordsPaginated(
            OrgId, ProjectList, SortRecordsRequestDto.NameAZ, paginatedDto);

        _mockQueryBusiness.Verify(
            b => b.GetRecordsPaginated(
                UserId, OrgId, SortRecordsRequestDto.NameAZ, paginatedDto, ProjectList, false, false, false),
            Times.Once);
    }

    [Fact]
    public void GetRecordsPaginated_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(QueryController.GetRecordsPaginated),
            "organizationId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
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
        return Assert.Single(typeof(QueryController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}