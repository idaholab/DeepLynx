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
///     Unit tests for the V2 actions of <see cref="ObjectStorageProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ObjectStorageProjectControllerTestsV2 : IDisposable
{
    private readonly Mock<IObjectStorageBusiness> _mockBusiness;
    private readonly Mock<ILogger<ObjectStorageProjectController>> _mockLogger;
    private readonly Mock<IProjectBusiness> _mockProjectBusiness;
    private readonly ObjectStorageProjectController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long ObjectStorageId = 5L;
    private const long UserId = 10L;

    public ObjectStorageProjectControllerTestsV2()
    {
        _mockBusiness = new Mock<IObjectStorageBusiness>();
        _mockLogger = new Mock<ILogger<ObjectStorageProjectController>>();
        _mockProjectBusiness = new Mock<IProjectBusiness>();

        _controller = new ObjectStorageProjectController(
            _mockBusiness.Object,
            _mockLogger.Object,
            _mockProjectBusiness.Object);

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
    // GetAllObjectStoragesV2 Tests
    // =========================================================================

    #region GetAllObjectStoragesV2 Tests

    [Fact]
    public async Task GetAllObjectStoragesV2_Returns200_WithList()
    {
        var expected = new List<ObjectStorageResponseDto> { new(), new() };
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, ProjectId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllObjectStoragesV2(OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllObjectStoragesV2_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllObjectStoragesV2(OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ObjectStorageResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllObjectStoragesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllObjectStoragesV2(OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetAllObjectStoragesV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, ProjectId, false))
                     .ReturnsAsync([]);

        await _controller.GetAllObjectStoragesV2(OrgId, ProjectId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllObjectStorages(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetAllObjectStoragesV2_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetAllObjectStoragesV2),
            "organizationId", "projectId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // GetObjectStorageV2 Tests
    // =========================================================================

    #region GetObjectStorageV2 Tests

    [Fact]
    public async Task GetObjectStorageV2_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetObjectStorage(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, true));
    }

    [Fact]
    public async Task GetObjectStorageV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        await _controller.GetObjectStorageV2(OrgId, ProjectId, ObjectStorageId, true);

        _mockBusiness.Verify(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true), Times.Once);
    }

    [Fact]
    public void GetObjectStorageV2_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetObjectStorageV2),
            "organizationId", "projectId", "objectStorageId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // CreateObjectStorageV2 Tests
    // =========================================================================

    #region CreateObjectStorageV2 Tests

    [Fact]
    public async Task CreateObjectStorageV2_Returns200_WithObjectStorage()
    {
        var dto = new CreateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();

        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateObjectStorageV2(OrgId, ProjectId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        var dto = new CreateObjectStorageRequestDto();
        _mockBusiness.Setup(b => b.CreateObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<CreateObjectStorageRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateObjectStorageV2(OrgId, ProjectId, dto));
    }

    [Fact]
    public async Task CreateObjectStorageV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var dto = new CreateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto))
                     .ReturnsAsync(expected);

        await _controller.CreateObjectStorageV2(OrgId, ProjectId, dto);

        _mockBusiness.Verify(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto), Times.Once);
    }

    [Fact]
    public void CreateObjectStorageV2_HasHttpPostAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.CreateObjectStorageV2),
            "organizationId", "projectId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "object_storage");
    }

    #endregion

    // =========================================================================
    // UpdateObjectStorageV2 Tests
    // =========================================================================

    #region UpdateObjectStorageV2 Tests

    [Fact]
    public async Task UpdateObjectStorageV2_Returns200_WithUpdatedObjectStorage()
    {
        var dto = new UpdateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();

        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.UpdateObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                         It.IsAny<UpdateObjectStorageRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, new UpdateObjectStorageRequestDto()));
    }

    [Fact]
    public async Task UpdateObjectStorageV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var dto = new UpdateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        await _controller.UpdateObjectStorageV2(OrgId, ProjectId, ObjectStorageId, dto);

        _mockBusiness.Verify(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto), Times.Once);
    }

    [Fact]
    public void UpdateObjectStorageV2_HasHttpPutAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.UpdateObjectStorageV2),
            "organizationId", "projectId", "objectStorageId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // DeleteObjectStorageV2 Tests
    // =========================================================================

    #region DeleteObjectStorageV2 Tests

    [Fact]
    public async Task DeleteObjectStorageV2_Returns200_WithBooleanResult()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.DeleteObjectStorageV2(OrgId, ProjectId, ObjectStorageId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId));
    }

    [Fact]
    public async Task DeleteObjectStorageV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        await _controller.DeleteObjectStorageV2(OrgId, ProjectId, ObjectStorageId);

        _mockBusiness.Verify(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void DeleteObjectStorageV2_HasHttpDeleteAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.DeleteObjectStorageV2),
            "organizationId", "projectId", "objectStorageId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "object_storage");
    }

    #endregion

    // =========================================================================
    // ArchiveObjectStorageV2 Tests
    // =========================================================================

    #region ArchiveObjectStorageV2 Tests

    [Fact]
    public async Task ArchiveObjectStorageV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveObjectStorageV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveObjectStorage(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveObjectStorageV2_PropagatesException_ForMiddlewareToHandle()
    {
        _mockBusiness.Setup(b => b.ArchiveObjectStorage(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _controller.ArchiveObjectStorageV2(OrgId, ProjectId, ObjectStorageId, archive: true));
    }

    [Fact]
    public void ArchiveObjectStorageV2_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.ArchiveObjectStorageV2),
            "organizationId", "projectId", "objectStorageId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // GetDefaultObjectStorageV2 Tests
    // =========================================================================

    #region GetDefaultObjectStorageV2 Tests

    [Fact]
    public async Task GetDefaultObjectStorageV2_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultObjectStorageV2(OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(It.IsAny<long>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultObjectStorageV2(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetDefaultObjectStorageV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        await _controller.GetDefaultObjectStorageV2(OrgId, ProjectId);

        _mockBusiness.Verify(b => b.GetDefaultObjectStorage(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetDefaultObjectStorageV2_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetDefaultObjectStorageV2),
            "organizationId", "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // SetDefaultObjectStorageV2 Tests
    // =========================================================================

    #region SetDefaultObjectStorageV2 Tests

    [Fact]
    public async Task SetDefaultObjectStorageV2_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task SetDefaultObjectStorageV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultObjectStorageV2(
            OrgId, ProjectId, ObjectStorageId));
    }

    [Fact]
    public async Task SetDefaultObjectStorageV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(expected);

        await _controller.SetDefaultObjectStorageV2(OrgId, ProjectId, ObjectStorageId);

        _mockBusiness.Verify(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void SetDefaultObjectStorageV2_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.SetDefaultObjectStorageV2),
            "organizationId", "projectId", "objectStorageId");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // CreateProjectContainerV2 Tests
    // =========================================================================

    #region CreateProjectContainerV2 Tests

    [Fact]
    public async Task CreateProjectContainerV2_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();

        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId))
                            .ReturnsAsync(expected);

        var result = (await _controller.CreateProjectContainerV2(OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateProjectContainerV2_ThrowsException_WhenBusinessThrows()
    {
        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(
                                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateProjectContainerV2(OrgId, ProjectId));
    }

    [Fact]
    public async Task CreateProjectContainerV2_PassesCurrentUserIdAndOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId))
                            .ReturnsAsync(expected);

        await _controller.CreateProjectContainerV2(OrgId, ProjectId);

        _mockProjectBusiness.Verify(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void CreateProjectContainerV2_HasHttpPostAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.CreateProjectContainerV2),
            "organizationId", "projectId");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "object_storage");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void ObjectStorageProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(ObjectStorageProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(ObjectStorageProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
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
}