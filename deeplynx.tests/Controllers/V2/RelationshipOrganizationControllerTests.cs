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
///     Unit tests for the V2 actions of <see cref="RelationshipOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RelationshipOrganizationControllerTests : IDisposable
{
    private readonly Mock<IRelationshipBusiness> _mockRelationshipBusiness;
    private readonly Mock<ILogger<RelationshipOrganizationController>> _mockLogger;
    private readonly RelationshipOrganizationController _relationshipOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long RelationshipId = 22L;
    private static readonly long[] ProjectList = { 13L, 14L };

    public RelationshipOrganizationControllerTests()
    {
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockLogger = new Mock<ILogger<RelationshipOrganizationController>>();

        _relationshipOrganizationController = new RelationshipOrganizationController(
            _mockRelationshipBusiness.Object,
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
            .Setup(b => b.GetAllRelationshipsPaginated(OrgId, ProjectList, It.IsAny<PaginatedRequestDto>(), true))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.GetAllRelationships(
            OrgId, ProjectList, true)).Result as OkObjectResult;

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

        var result = (await _relationshipOrganizationController.GetAllRelationships(
            OrgId, ProjectList, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.GetAllRelationships(
            OrgId, ProjectList, true));
    }

    [Fact]
    public async Task GetAllRelationships_PassesProjectIdsAndHideArchivedToBusinessLayer()
    {
        var expected = new PaginatedResponse<RelationshipResponseDto>
        {
            Items = [],
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(OrgId, ProjectList, It.IsAny<PaginatedRequestDto>(), true))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.GetAllRelationships(OrgId, ProjectList, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetAllRelationshipsPaginated(OrgId, ProjectList, It.IsAny<PaginatedRequestDto>(), true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllRelationships_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, ProjectList,
                It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
                true))
            .ReturnsAsync(new PaginatedResponse<RelationshipResponseDto>
            {
                Items = [],
                PageNumber = 1,
                PageSize = 25,
                TotalCount = 0
            });

        await _relationshipOrganizationController.GetAllRelationships(OrgId, ProjectList, true);

        _mockRelationshipBusiness.Verify(b => b.GetAllRelationshipsPaginated(
            OrgId, ProjectList,
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
            true), Times.Once);
    }

    [Fact]
    public async Task GetAllRelationships_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 4, PageSize = 50 };

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationshipsPaginated(
                OrgId, ProjectList,
                It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
                true))
            .ReturnsAsync(new PaginatedResponse<RelationshipResponseDto>
            {
                Items = [],
                PageNumber = 4,
                PageSize = 50,
                TotalCount = 0
            });

        await _relationshipOrganizationController.GetAllRelationships(OrgId, ProjectList, true, pagination);

        _mockRelationshipBusiness.Verify(b => b.GetAllRelationshipsPaginated(
            OrgId, ProjectList,
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
            true), Times.Once);
    }

    [Fact]
    public void GetAllRelationships_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetAllRelationships),
            "organizationId", "projectIds", "hideArchived", "paginatedRequestDto");

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
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.GetRelationship(
            OrgId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRelationship_Returns200_WithNullRelationship()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync((RelationshipResponseDto)null!);

        var result = (await _relationshipOrganizationController.GetRelationship(
            OrgId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.GetRelationship(
            OrgId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationship_PropagatesKeyNotFoundException_InsteadOfReturning404()
    {
        // v1 mapped a KeyNotFoundException to a 404 via a dedicated catch block. That catch block
        // is removed in v2, so the same exception now propagates raw to the global exception handler
        // instead of producing a 404 directly from this action.
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ThrowsAsync(new KeyNotFoundException("relationship not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _relationshipOrganizationController.GetRelationship(
            OrgId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationship_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.GetRelationship(OrgId, RelationshipId, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetRelationship(OrgId, null, RelationshipId, true),
            Times.Once);
    }

    [Fact]
    public void GetRelationship_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetRelationship),
            "organizationId", "relationshipId", "hideArchived");

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
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.CreateRelationship(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRelationship_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.CreateRelationship(
            OrgId, input));
    }

    [Fact]
    public async Task CreateRelationship_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.CreateRelationship(OrgId, input);

        _mockRelationshipBusiness.Verify(
            b => b.CreateRelationship(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void CreateRelationship_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.CreateRelationship),
            "organizationId", "dto");

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
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.BulkCreateRelationships(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateRelationships_ThrowsException_WhenBusinessThrows()
    {
        var input = new List<CreateRelationshipRequestDto>();
        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.BulkCreateRelationships(
            OrgId, input));
    }

    [Fact]
    public async Task BulkCreateRelationships_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new List<RelationshipResponseDto>();
        var input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.BulkCreateRelationships(OrgId, input);

        _mockRelationshipBusiness.Verify(
            b => b.BulkCreateRelationships(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void BulkCreateRelationships_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.BulkCreateRelationships),
            "organizationId", "relationships");

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
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.UpdateRelationship(
            OrgId, RelationshipId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRelationship_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.UpdateRelationship(
            OrgId, RelationshipId, input));
    }

    [Fact]
    public async Task UpdateRelationship_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.UpdateRelationship(OrgId, RelationshipId, input);

        _mockRelationshipBusiness.Verify(
            b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRelationship_HasHttpPutAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.UpdateRelationship),
            "organizationId", "relationshipId", "dto");

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
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipOrganizationController.DeleteRelationship(
            OrgId, RelationshipId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.DeleteRelationship(
            OrgId, RelationshipId));
    }

    [Fact]
    public async Task DeleteRelationship_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        await _relationshipOrganizationController.DeleteRelationship(OrgId, RelationshipId);

        _mockRelationshipBusiness.Verify(
            b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId),
            Times.Once);
    }

    [Fact]
    public void DeleteRelationship_HasHttpDeleteAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.DeleteRelationship),
            "organizationId", "relationshipId");

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
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId, RelationshipId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRelationshipBusiness.Verify(
            b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId),
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
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId, RelationshipId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRelationshipBusiness.Verify(
            b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId),
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
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipOrganizationController.ArchiveRelationship(OrgId, RelationshipId, archive: true));
    }

    [Fact]
    public async Task ArchiveRelationship_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipOrganizationController.ArchiveRelationship(OrgId, RelationshipId, archive: false));
    }

    [Fact]
    public void ArchiveRelationship_HasHttpPatchAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.ArchiveRelationship),
            "organizationId", "relationshipId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "relationship");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void RelationshipOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RelationshipOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void RelationshipOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(RelationshipOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
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
        return Assert.Single(typeof(RelationshipOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}