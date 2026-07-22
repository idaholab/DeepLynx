using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="RelationshipOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RelationshipOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<IRelationshipBusiness> _mockRelationshipBusiness;
    private readonly Mock<ILogger<RelationshipOrganizationController>> _mockLogger;
    private readonly RelationshipOrganizationController _relationshipOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long RelationshipId = 22L;
    private static readonly long[] ProjectList = { 13L, 14L };

    public RelationshipOrganizationControllerTestsV2()
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
    // GetAllRelationshipsV2 Tests
    // =========================================================================

    #region GetAllRelationshipsV2 Tests

    [Fact]
    public async Task GetAllRelationshipsV2_Returns200_WithRelationships()
    {
        var expected = new List<RelationshipResponseDto>();

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(OrgId, ProjectList, true))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.GetAllRelationshipsV2(
            OrgId, ProjectList, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRelationshipsV2_Returns200_WithEmptyList()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _relationshipOrganizationController.GetAllRelationshipsV2(
            OrgId, ProjectList, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RelationshipResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRelationshipsV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.GetAllRelationshipsV2(
            OrgId, ProjectList, true));
    }

    [Fact]
    public async Task GetAllRelationshipsV2_PassesProjectIdsAndHideArchivedToBusinessLayer()
    {
        var expected = new List<RelationshipResponseDto>();

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(OrgId, ProjectList, true))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.GetAllRelationshipsV2(OrgId, ProjectList, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetAllRelationships(OrgId, ProjectList, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRelationshipsV2_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetAllRelationshipsV2),
            "organizationId", "projectIds", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "relationship");
    }

    #endregion

    // =========================================================================
    // GetRelationshipV2 Tests
    // =========================================================================

    #region GetRelationshipV2 Tests

    [Fact]
    public async Task GetRelationshipV2_Returns200_WithRelationship()
    {
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.GetRelationshipV2(
            OrgId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRelationshipV2_Returns200_WithNullRelationship()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync((RelationshipResponseDto)null!);

        var result = (await _relationshipOrganizationController.GetRelationshipV2(
            OrgId, RelationshipId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRelationshipV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.GetRelationshipV2(
            OrgId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationshipV2_PropagatesKeyNotFoundException_InsteadOfReturning404()
    {
        // v1 mapped a KeyNotFoundException to a 404 via a dedicated catch block. That catch block
        // is removed in v2, so the same exception now propagates raw to the global exception handler
        // instead of producing a 404 directly from this action.
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ThrowsAsync(new KeyNotFoundException("relationship not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _relationshipOrganizationController.GetRelationshipV2(
            OrgId, RelationshipId, true));
    }

    [Fact]
    public async Task GetRelationshipV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.GetRelationshipV2(OrgId, RelationshipId, true);

        _mockRelationshipBusiness.Verify(
            b => b.GetRelationship(OrgId, null, RelationshipId, true),
            Times.Once);
    }

    [Fact]
    public void GetRelationshipV2_HasHttpGetAndReadRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetRelationshipV2),
            "organizationId", "relationshipId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "relationship");
    }

    #endregion

    // =========================================================================
    // CreateRelationshipV2 Tests
    // =========================================================================

    #region CreateRelationshipV2 Tests

    [Fact]
    public async Task CreateRelationshipV2_Returns200_WithRelationship()
    {
        var input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.CreateRelationshipV2(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRelationshipV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.CreateRelationshipV2(
            OrgId, input));
    }

    [Fact]
    public async Task CreateRelationshipV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.CreateRelationshipV2(OrgId, input);

        _mockRelationshipBusiness.Verify(
            b => b.CreateRelationship(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void CreateRelationshipV2_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.CreateRelationshipV2),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // BulkCreateRelationshipsV2 Tests
    // =========================================================================

    #region BulkCreateRelationshipsV2 Tests

    [Fact]
    public async Task BulkCreateRelationshipsV2_Returns200_WithRelationshipList()
    {
        var expected = new List<RelationshipResponseDto>();
        var input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.BulkCreateRelationshipsV2(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateRelationshipsV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new List<CreateRelationshipRequestDto>();
        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.BulkCreateRelationshipsV2(
            OrgId, input));
    }

    [Fact]
    public async Task BulkCreateRelationshipsV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new List<RelationshipResponseDto>();
        var input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.BulkCreateRelationshipsV2(OrgId, input);

        _mockRelationshipBusiness.Verify(
            b => b.BulkCreateRelationships(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void BulkCreateRelationshipsV2_HasHttpPostAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.BulkCreateRelationshipsV2),
            "organizationId", "relationships");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // UpdateRelationshipV2 Tests
    // =========================================================================

    #region UpdateRelationshipV2 Tests

    [Fact]
    public async Task UpdateRelationshipV2_Returns200_WithRelationship()
    {
        var input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        var result = (await _relationshipOrganizationController.UpdateRelationshipV2(
            OrgId, RelationshipId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRelationshipV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.UpdateRelationshipV2(
            OrgId, RelationshipId, input));
    }

    [Fact]
    public async Task UpdateRelationshipV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        await _relationshipOrganizationController.UpdateRelationshipV2(OrgId, RelationshipId, input);

        _mockRelationshipBusiness.Verify(
            b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRelationshipV2_HasHttpPutAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.UpdateRelationshipV2),
            "organizationId", "relationshipId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "relationship");
    }

    #endregion

    // =========================================================================
    // DeleteRelationshipV2 Tests
    // =========================================================================

    #region DeleteRelationshipV2 Tests

    [Fact]
    public async Task DeleteRelationshipV2_Returns200_WithBooleanResult()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var result = await _relationshipOrganizationController.DeleteRelationshipV2(
            OrgId, RelationshipId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRelationshipV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _relationshipOrganizationController.DeleteRelationshipV2(
            OrgId, RelationshipId));
    }

    [Fact]
    public async Task DeleteRelationshipV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        await _relationshipOrganizationController.DeleteRelationshipV2(OrgId, RelationshipId);

        _mockRelationshipBusiness.Verify(
            b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId),
            Times.Once);
    }

    [Fact]
    public void DeleteRelationshipV2_HasHttpDeleteAndWriteRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.DeleteRelationshipV2),
            "organizationId", "relationshipId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "relationship");
    }

    #endregion

    // =========================================================================
    // ArchiveRelationshipV2 Tests
    // =========================================================================

    #region ArchiveRelationshipV2 Tests

    [Fact]
    public async Task ArchiveRelationshipV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var result = await _relationshipOrganizationController.ArchiveRelationshipV2(
            OrgId, RelationshipId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
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
    public async Task ArchiveRelationshipV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        var result = await _relationshipOrganizationController.ArchiveRelationshipV2(
            OrgId, RelationshipId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
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
    public async Task ArchiveRelationshipV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipOrganizationController.ArchiveRelationshipV2(OrgId, RelationshipId, archive: true));
    }

    [Fact]
    public async Task ArchiveRelationshipV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _relationshipOrganizationController.ArchiveRelationshipV2(OrgId, RelationshipId, archive: false));
    }

    [Fact]
    public void ArchiveRelationshipV2_HasHttpPatchAndUpdateRelationshipAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.ArchiveRelationshipV2),
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