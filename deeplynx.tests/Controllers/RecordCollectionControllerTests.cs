using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="RecordCollectionController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RecordCollectionControllerTests : IDisposable
{
    private readonly Mock<IRecordCollectionBusiness> _mockRecordCollectionBusiness;
    private readonly Mock<ILogger<RecordCollectionController>> _mockLogger;
    private readonly RecordCollectionController _recordCollectionController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private const long UserId = 10L;
    private const long CollectionId = 7L;
    private const long RecordCollectionId = 8L;
    private const long RecordIdConst = 20L;

    public RecordCollectionControllerTests()
    {
        _mockRecordCollectionBusiness = new Mock<IRecordCollectionBusiness>();
        _mockLogger = new Mock<ILogger<RecordCollectionController>>();

        _recordCollectionController = new RecordCollectionController(
            _mockRecordCollectionBusiness.Object,
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
    // GetAllRecordCollections Tests
    // =========================================================================

    #region GetAllRecordCollections Tests

    [Fact]
    public async Task GetAllRecordCollections_Returns200_WithList()
    {
        var expected = new List<RecordCollectionResponseDto>
        {
            new(),
            new()
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollections(
                         UserId, OrgId, ProjectId, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRecordCollections_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollections(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RecordCollectionResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRecordCollections_Returns500_OnUnexpectedException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollections(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while listing all record collections", result.Value.ToString());
    }

    [Fact]
    public async Task GetAllRecordCollections_PassesIdsHideArchivedAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollections(
                         UserId, OrgId, ProjectId, false, true, true, true))
                     .ReturnsAsync([]);

        await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId,
            hideArchived: false);

        _mockRecordCollectionBusiness.Verify(b => b.GetAllRecordCollections(
            UserId, OrgId, ProjectId, false, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetRecordsInRecordCollection Tests
    // =========================================================================

    #region GetRecordsInRecordCollection Tests

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns200_WithList()
    {
        var expected = new List<RecordResponseDto>
        {
            new() { Id = RecordIdConst, Name = "Record 1" },
            new() { Id = RecordIdConst + 1, Name = "Record 2" }
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollection(
                         UserId, OrgId, ProjectId, CollectionId, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RecordResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns404_OnKeyNotFoundException()
    {
        const string expected = "record collection not found";

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException(expected));

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns500_OnUnexpectedException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while listing records in collection {CollectionId}", result.Value.ToString());
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_PassesIdsHideArchivedAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollection(
                         UserId, OrgId, ProjectId, CollectionId, false, true, true, true))
                     .ReturnsAsync([]);

        await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            hideArchived: false);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordsInRecordCollection(
            UserId, OrgId, ProjectId, CollectionId, false, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // AddRecordsToRecordCollection Tests
    // =========================================================================

    #region AddRecordsToRecordCollection Tests

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns200_OnSuccess()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         false,
                         false,
                         false))
                     .ReturnsAsync(true);

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Successfully added records", result.Value.ToString());
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns400_OnArgumentException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new ArgumentException("invalid request"));

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("invalid request", result.Value);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException("record not found"));

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("record not found", result.Value);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns403_OnUnauthorizedAccessException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new UnauthorizedAccessException("not allowed"));

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("not allowed", result.Value);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns500_OnUnexpectedException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while updating record collection records", result.Value.ToString());
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_PassesIdsRecordIdsAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         true,
                         true,
                         true))
                     .ReturnsAsync(true);

        await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList);

        _mockRecordCollectionBusiness.Verify(b => b.AddRecordsToRecordCollection(
            UserId,
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList,
            true,
            true,
            true), Times.Once);
    }

    #endregion

    // =========================================================================
    // RemoveRecordsFromRecordCollection Tests
    // =========================================================================

    #region RemoveRecordsFromRecordCollection Tests

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns200_OnSuccess()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         false,
                         false,
                         false))
                     .ReturnsAsync(true);

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Successfully removed records", result.Value.ToString());
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns400_OnArgumentException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new ArgumentException("invalid request"));

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("invalid request", result.Value);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException("record not found"));

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("record not found", result.Value);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns403_OnUnauthorizedAccessException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new UnauthorizedAccessException("not allowed"));

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("not allowed", result.Value);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns500_OnUnexpectedException()
    {
        var request = new UpdateRecordCollectionRequestDto
        {
            RecordIds = new List<long> { RecordIdConst }
        };

        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while updating record collection records", result.Value.ToString());
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_PassesIdsRecordIdsAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var recordIds = new List<long> { RecordIdConst };
        var request = new UpdateRecordCollectionRequestDto
        {
            RecordIds = recordIds
        };

        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         true,
                         true,
                         true))
                     .ReturnsAsync(true);

        await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList);

        _mockRecordCollectionBusiness.Verify(b => b.RemoveRecordsFromRecordCollection(
            UserId,
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList,
            true,
            true,
            true), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateRecordCollection Tests
    // =========================================================================

    #region CreateRecordCollection Tests

    [Fact]
    public async Task CreateRecordCollection_Returns200_WithRecordCollection()
    {
        var request = new CreateRecordCollectionRequestDto();
        var expected = new RecordCollectionResponseDto();

        _mockRecordCollectionBusiness.Setup(b => b.CreateRecordCollection(
                         UserId, OrgId, ProjectId, null, request))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            null,
            request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRecordCollection_Returns500_OnUnexpectedException()
    {
        var request = new CreateRecordCollectionRequestDto();

        _mockRecordCollectionBusiness.Setup(b => b.CreateRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<long>>(),
                         It.IsAny<CreateRecordCollectionRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            It.IsAny<List<long>>(),
            request)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while creating record collection", result.Value.ToString());
    }

    [Fact]
    public async Task CreateRecordCollection_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateRecordCollectionRequestDto();
        var expected = new RecordCollectionResponseDto();

        _mockRecordCollectionBusiness.Setup(b => b.CreateRecordCollection(
                         UserId, OrgId, ProjectId, null, request))
                     .ReturnsAsync(expected);

        await _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            null,
            request);

        _mockRecordCollectionBusiness.Verify(b => b.CreateRecordCollection(
            UserId, OrgId, ProjectId, null, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteRecordCollection Tests
    // =========================================================================

    #region DeleteRecordCollection Tests

    [Fact]
    public async Task DeleteRecordCollection_Returns200_WithMessage()
    {
        _mockRecordCollectionBusiness.Setup(b => b.DeleteRecordCollection(
                         UserId, OrgId, ProjectId, RecordCollectionId))
                     .Returns(Task.FromResult(true));

        var result = await _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteRecordCollection_Returns500_OnUnexpectedException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.DeleteRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while deleting record collection", result.Value.ToString());
    }

    [Fact]
    public async Task DeleteRecordCollection_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockRecordCollectionBusiness.Setup(b => b.DeleteRecordCollection(
                         UserId, OrgId, ProjectId, RecordCollectionId))
                     .Returns(Task.FromResult(true));

        await _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId);

        _mockRecordCollectionBusiness.Verify(b => b.DeleteRecordCollection(
            UserId, OrgId, ProjectId, RecordCollectionId), Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void RecordCollectionController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RecordCollectionController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllRecordCollections_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetAllRecordCollections),
            "organizationId",
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void GetRecordsInRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetRecordsInRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void AddRecordsToRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.AddRecordsToRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "recordIds");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void RemoveRecordsFromRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.RemoveRecordsFromRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "recordIds");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void CreateRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.CreateRecordCollection),
            "organizationId",
            "projectId",
            "sensitivityLabelIds",
            "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void DeleteRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.DeleteRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(RecordCollectionController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => method.GetParameters()
                .Select(parameter => parameter.Name ?? string.Empty)
                .SequenceEqual(parameterNames)));
    }

    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static void AssertHasAuthAttribute(
        System.Reflection.MethodInfo method,
        string expectedAction,
        string expectedResource)
    {
        var authAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
            .ToList();

        Assert.Contains(authAttributes, attribute =>
            attribute.ConstructorArguments.Count >= 2 &&
            attribute.ConstructorArguments[0].Value?.ToString() == expectedAction &&
            attribute.ConstructorArguments[1].Value?.ToString() == expectedResource);
    }

    private static void AssertHasSensitivityAttribute(
        System.Reflection.MethodInfo method,
        string expectedSensitivity)
    {
        var sensitivityAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "SensitivityAttribute")
            .ToList();

        Assert.Contains(sensitivityAttributes, attribute =>
            attribute.ConstructorArguments.Any(argument =>
                argument.Value?.ToString() == expectedSensitivity));
    }
}