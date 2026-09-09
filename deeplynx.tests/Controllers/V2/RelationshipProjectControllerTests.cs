using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="RelationshipProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RelationshipProjectControllerTests : IDisposable
{
    private readonly Mock<IRelationshipBusiness> _mockRelationshipBusiness;
    private readonly Mock<ILogger<RelationshipProjectController>> _mockLogger;
    private readonly RelationshipProjectController _relationshipProjectController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long RelationshipId = 22L;

    public RelationshipProjectControllerTests()
    {
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockLogger = new Mock<ILogger<RelationshipProjectController>>();

        _relationshipProjectController = new RelationshipProjectController(
            _mockRelationshipBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;
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
    // GetAllRelationships Tests
    // =========================================================================

    #region GetAllRelationships Tests

    [Fact]
    public async Task GetAllRelationships_Returns200_WithRelationships()
    {
        var expected = new PaginatedResponse<RelationshipResponseDto>
        {
            Items = new List<RelationshipResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
                It.IsAny<PaginatedRequestDto>(), true))
            .ReturnsAsync(expected);

        var result = (await _relationshipProjectController.GetAllRelationships(
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRelationships_Returns200_WithEmptyList()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                It.IsAny<long>(), It.IsAny<long[]?>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>()))
            .ReturnsAsync(new PaginatedResponse<RelationshipResponseDto>
            {
                Items = [],
                PageNumber = 1,
                PageSize = 25,
                TotalCount = 0
            });

        var result = (await _relationshipProjectController.GetAllRelationships(
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<RelationshipResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRelationships_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                It.IsAny<long>(), It.IsAny<long[]?>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.GetAllRelationships(
            ProjectId, true));
    }

    [Fact]
    public async Task GetAllRelationships_RebuildsProjectIdArray_AndPassesOrganizationIdFromContext()
    {
        var expected = new PaginatedResponse<RelationshipResponseDto>
        {
            Items = [],
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
                It.IsAny<PaginatedRequestDto>(), true))
            .ReturnsAsync(expected);

        await _relationshipProjectController.GetAllRelationships(ProjectId, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetAllRelationshipsPaginated(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
                It.IsAny<PaginatedRequestDto>(), true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllRelationships_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
                It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
                true))
            .ReturnsAsync(new PaginatedResponse<RelationshipResponseDto>
            {
                Items = [],
                PageNumber = 1,
                PageSize = 25,
                TotalCount = 0
            });

        await _relationshipProjectController.GetAllRelationships(ProjectId, true);

        _mockRelationshipBusiness.Verify(b => b.GetAllRelationshipsPaginated(
            OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
            true), Times.Once);
    }

    [Fact]
    public async Task GetAllRelationships_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 3, PageSize = 10 };

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
                It.Is<PaginatedRequestDto>(p => p.PageNumber == 3 && p.PageSize == 10),
                true))
            .ReturnsAsync(new PaginatedResponse<RelationshipResponseDto>
            {
                Items = [],
                PageNumber = 3,
                PageSize = 10,
                TotalCount = 0
            });

        await _relationshipProjectController.GetAllRelationships(ProjectId, true, pagination);

        _mockRelationshipBusiness.Verify(b => b.GetAllRelationshipsPaginated(
            OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 3 && p.PageSize == 10),
            true), Times.Once);
    }

    [Fact]
    public void GetAllRelationships_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.GetAllRelationships),
            "projectId", "hideArchived", "paginatedRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "relationship");
    }

    #endregion

    // =========================================================================
    // GetRelationship Tests
    // =========================================================================

    #region GetRelationship Tests

    [Fact]
    public async Task GetRelationship_Returns200_WithRelationship()
    {
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ReturnsAsync(expected);

        var result = (await _relationshipProjectController.GetRelationship(
            ProjectId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRelationship_Returns200_WithNullRelationship()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ReturnsAsync((RelationshipResponseDto)null!);

        var result = (await _relationshipProjectController.GetRelationship(
            ProjectId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.GetRelationship(
            ProjectId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationship_PropagatesKeyNotFoundException_InsteadOfReturning404()
    {
        // v1 mapped a KeyNotFoundException to a 404 via a dedicated catch block. That catch block
        // is removed in v2, so the same exception now propagates raw to the global exception handler
        // instead of producing a 404 directly from this action.
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ThrowsAsync(new KeyNotFoundException("relationship not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _relationshipProjectController.GetRelationship(
            ProjectId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationship_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ReturnsAsync(expected);

        await _relationshipProjectController.GetRelationship(ProjectId, RelationshipId, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true),
            Times.Once);
    }

    [Fact]
    public void GetRelationship_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.GetRelationship),
            "projectId", "relationshipId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "relationship");
    }

    #endregion

    // =========================================================================
    // CreateRelationship Tests
    // =========================================================================

    #region CreateRelationship Tests

    [Fact]
    public async Task CreateRelationship_Returns200_WithRelationship()
    {
        var input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipProjectController.CreateRelationship(
            ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRelationship_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.CreateRelationship(
            ProjectId, input));
    }

    [Fact]
    public async Task CreateRelationship_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        await _relationshipProjectController.CreateRelationship(ProjectId, input);

        _mockRelationshipBusiness.Verify(
            b => b.CreateRelationship(UserId, OrgId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void CreateRelationship_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.CreateRelationship),
            "projectId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // BulkCreateRelationships Tests
    // =========================================================================

    #region BulkCreateRelationships Tests

    [Fact]
    public async Task BulkCreateRelationships_Returns200_WithRelationshipList()
    {
        var expected = new List<RelationshipResponseDto>();
        var input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipProjectController.BulkCreateRelationships(
            ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateRelationships_ThrowsException_WhenBusinessThrows()
    {
        var input = new List<CreateRelationshipRequestDto>();
        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.BulkCreateRelationships(
            ProjectId, input));
    }

    [Fact]
    public async Task BulkCreateRelationships_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var expected = new List<RelationshipResponseDto>();
        var input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        await _relationshipProjectController.BulkCreateRelationships(ProjectId, input);

        _mockRelationshipBusiness.Verify(
            b => b.BulkCreateRelationships(UserId, OrgId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void BulkCreateRelationships_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.BulkCreateRelationships),
            "projectId", "relationships");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // UpdateRelationship Tests
    // =========================================================================

    #region UpdateRelationship Tests

    [Fact]
    public async Task UpdateRelationship_Returns200_WithRelationship()
    {
        var input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, ProjectId, RelationshipId, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipProjectController.UpdateRelationship(
            ProjectId, RelationshipId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRelationship_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, ProjectId, RelationshipId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.UpdateRelationship(
            ProjectId, RelationshipId, input));
    }

    [Fact]
    public async Task UpdateRelationship_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, ProjectId, RelationshipId, input))
            .ReturnsAsync(expected);

        await _relationshipProjectController.UpdateRelationship(ProjectId, RelationshipId, input);

        _mockRelationshipBusiness.Verify(
            b => b.UpdateRelationship(UserId, OrgId, ProjectId, RelationshipId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRelationship_HasHttpPutAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.UpdateRelationship),
            "projectId", "relationshipId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "relationship");
    }

    #endregion

    // =========================================================================
    // DeleteRelationship Tests
    // =========================================================================

    #region DeleteRelationship Tests

    [Fact]
    public async Task DeleteRelationship_Returns200_WithBooleanResult()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipProjectController.DeleteRelationship(
            ProjectId, RelationshipId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipProjectController.DeleteRelationship(
            ProjectId, RelationshipId));
    }

    [Fact]
    public async Task DeleteRelationship_PassesOrganizationIdFromContextToBusinessLayer()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ReturnsAsync(true);

        await _relationshipProjectController.DeleteRelationship(ProjectId, RelationshipId);

        _mockRelationshipBusiness.Verify(
            b => b.DeleteRelationship(UserId, OrgId, ProjectId, RelationshipId),
            Times.Once);
    }

    [Fact]
    public void DeleteRelationship_HasHttpDeleteAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.DeleteRelationship),
            "projectId", "relationshipId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // ArchiveRelationship Tests
    // =========================================================================

    #region ArchiveRelationship Tests

    [Fact]
    public async Task ArchiveRelationship_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipProjectController.ArchiveRelationship(
            ProjectId, RelationshipId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRelationshipBusiness.Verify(
            b => b.ArchiveRelationship(UserId, OrgId, ProjectId, RelationshipId),
            Times.Once);
        _mockRelationshipBusiness.Verify(
            b => b.UnarchiveRelationship(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRelationship_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipProjectController.ArchiveRelationship(
            ProjectId, RelationshipId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRelationshipBusiness.Verify(
            b => b.UnarchiveRelationship(UserId, OrgId, ProjectId, RelationshipId),
            Times.Once);
        _mockRelationshipBusiness.Verify(
            b => b.ArchiveRelationship(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRelationship_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipProjectController.ArchiveRelationship(ProjectId, RelationshipId, archive: true));
    }

    [Fact]
    public async Task ArchiveRelationship_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, ProjectId, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipProjectController.ArchiveRelationship(ProjectId, RelationshipId, archive: false));
    }

    [Fact]
    public void ArchiveRelationship_HasHttpPatchAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipProjectController.ArchiveRelationship),
            "projectId", "relationshipId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "relationship");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void RelationshipProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RelationshipProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
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

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(RelationshipProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}