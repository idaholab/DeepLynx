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
///     Unit tests for the V2 actions of <see cref="ObjectStorageProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ObjectStorageProjectControllerTests : IDisposable
{
    private readonly Mock<IObjectStorageBusiness> _mockBusiness;
    private readonly Mock<ILogger<ObjectStorageProjectController>> _mockLogger;
    private readonly Mock<IProjectBusiness> _mockProjectBusiness;
    private readonly ObjectStorageProjectController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long ObjectStorageId = 5L;
    private const long UserId = 10L;

    public ObjectStorageProjectControllerTests()
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
    // GetAllObjectStorages Tests
    // =========================================================================

    #region GetAllObjectStorages Tests

    [Fact]
    public async Task GetAllObjectStorages_Returns200_WithList()
    {
        var expected = new List<ObjectStorageResponseDto> { new(), new() };
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, ProjectId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllObjectStorages(OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllObjectStorages_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllObjectStorages(OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ObjectStorageResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllObjectStorages_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllObjectStorages(OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetAllObjectStorages_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, ProjectId, false))
                     .ReturnsAsync([]);

        await _controller.GetAllObjectStorages(OrgId, ProjectId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllObjectStorages(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetAllObjectStorages_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetAllObjectStorages),
            "organizationId", "projectId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // GetObjectStorage Tests
    // =========================================================================

    #region GetObjectStorage Tests

    [Fact]
    public async Task GetObjectStorage_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetObjectStorage(
            OrgId, ProjectId, ObjectStorageId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetObjectStorage(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetObjectStorage(
            OrgId, ProjectId, ObjectStorageId, true));
    }

    [Fact]
    public async Task GetObjectStorage_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        await _controller.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true);

        _mockBusiness.Verify(b => b.GetObjectStorage(OrgId, ProjectId, ObjectStorageId, true), Times.Once);
    }

    [Fact]
    public void GetObjectStorage_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetObjectStorage),
            "organizationId", "projectId", "objectStorageId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // CreateObjectStorage Tests
    // =========================================================================

    #region CreateObjectStorage Tests

    [Fact]
    public async Task CreateObjectStorage_Returns200_WithObjectStorage()
    {
        var dto = new CreateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();

        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateObjectStorage(OrgId, ProjectId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        var dto = new CreateObjectStorageRequestDto();
        _mockBusiness.Setup(b => b.CreateObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<CreateObjectStorageRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateObjectStorage(OrgId, ProjectId, dto));
    }

    [Fact]
    public async Task CreateObjectStorage_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var dto = new CreateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto))
                     .ReturnsAsync(expected);

        await _controller.CreateObjectStorage(OrgId, ProjectId, dto);

        _mockBusiness.Verify(b => b.CreateObjectStorage(UserId, OrgId, ProjectId, dto), Times.Once);
    }

    [Fact]
    public void CreateObjectStorage_HasHttpPostAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.CreateObjectStorage),
            "organizationId", "projectId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "object_storage");
    }

    #endregion

    // =========================================================================
    // UpdateObjectStorage Tests
    // =========================================================================

    #region UpdateObjectStorage Tests

    [Fact]
    public async Task UpdateObjectStorage_Returns200_WithUpdatedObjectStorage()
    {
        var dto = new UpdateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();

        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateObjectStorage(
            OrgId, ProjectId, ObjectStorageId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.UpdateObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                         It.IsAny<UpdateObjectStorageRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateObjectStorage(
            OrgId, ProjectId, ObjectStorageId, new UpdateObjectStorageRequestDto()));
    }

    [Fact]
    public async Task UpdateObjectStorage_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var dto = new UpdateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        await _controller.UpdateObjectStorage(OrgId, ProjectId, ObjectStorageId, dto);

        _mockBusiness.Verify(b => b.UpdateObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId, dto), Times.Once);
    }

    [Fact]
    public void UpdateObjectStorage_HasHttpPutAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.UpdateObjectStorage),
            "organizationId", "projectId", "objectStorageId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // DeleteObjectStorage Tests
    // =========================================================================

    #region DeleteObjectStorage Tests

    [Fact]
    public async Task DeleteObjectStorage_Returns200_WithBooleanResult()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.DeleteObjectStorage(OrgId, ProjectId, ObjectStorageId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteObjectStorage(
            OrgId, ProjectId, ObjectStorageId));
    }

    [Fact]
    public async Task DeleteObjectStorage_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        await _controller.DeleteObjectStorage(OrgId, ProjectId, ObjectStorageId);

        _mockBusiness.Verify(b => b.DeleteObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void DeleteObjectStorage_HasHttpDeleteAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.DeleteObjectStorage),
            "organizationId", "projectId", "objectStorageId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "object_storage");
    }

    #endregion

    // =========================================================================
    // ArchiveObjectStorage Tests
    // =========================================================================

    #region ArchiveObjectStorage Tests

    [Fact]
    public async Task ArchiveObjectStorage_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorage(
            OrgId, ProjectId, ObjectStorageId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveObjectStorage_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorage(
            OrgId, ProjectId, ObjectStorageId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveObjectStorage(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveObjectStorage_PropagatesException_ForMiddlewareToHandle()
    {
        _mockBusiness.Setup(b => b.ArchiveObjectStorage(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _controller.ArchiveObjectStorage(OrgId, ProjectId, ObjectStorageId, archive: true));
    }

    [Fact]
    public void ArchiveObjectStorage_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.ArchiveObjectStorage),
            "organizationId", "projectId", "objectStorageId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // GetDefaultObjectStorage Tests
    // =========================================================================

    #region GetDefaultObjectStorage Tests

    [Fact]
    public async Task GetDefaultObjectStorage_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultObjectStorage(OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(It.IsAny<long>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultObjectStorage(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetDefaultObjectStorage_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        await _controller.GetDefaultObjectStorage(OrgId, ProjectId);

        _mockBusiness.Verify(b => b.GetDefaultObjectStorage(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetDefaultObjectStorage_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.GetDefaultObjectStorage),
            "organizationId", "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "object_storage");
    }

    #endregion

    // =========================================================================
    // SetDefaultObjectStorage Tests
    // =========================================================================

    #region SetDefaultObjectStorage Tests

    [Fact]
    public async Task SetDefaultObjectStorage_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultObjectStorage(
            OrgId, ProjectId, ObjectStorageId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task SetDefaultObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultObjectStorage(
            OrgId, ProjectId, ObjectStorageId));
    }

    [Fact]
    public async Task SetDefaultObjectStorage_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId))
                     .ReturnsAsync(expected);

        await _controller.SetDefaultObjectStorage(OrgId, ProjectId, ObjectStorageId);

        _mockBusiness.Verify(b => b.SetDefaultObjectStorage(UserId, OrgId, ProjectId, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void SetDefaultObjectStorage_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.SetDefaultObjectStorage),
            "organizationId", "projectId", "objectStorageId");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // CreateProjectContainer Tests
    // =========================================================================

    #region CreateProjectContainer Tests

    [Fact]
    public async Task CreateProjectContainer_Returns200_WithObjectStorage()
    {
        var expected = new ObjectStorageResponseDto();

        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId, "test"))
                            .ReturnsAsync(expected);

        var result = (await _controller.CreateProjectContainer(OrgId, ProjectId, "test", false)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateProjectContainer_ThrowsException_WhenBusinessThrows()
    {
        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(
                                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateProjectContainer(OrgId, ProjectId, "test", false));
    }

    [Fact]
    public async Task CreateProjectContainer_PassesCurrentUserIdAndOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockProjectBusiness.Setup(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId, "test"))
                            .ReturnsAsync(expected);

        await _controller.CreateProjectContainer(OrgId, ProjectId, "test");

        _mockProjectBusiness.Verify(b => b.CreateProjectAzureContainer(UserId, OrgId, ProjectId, "test"), Times.Once);
    }

    [Fact]
    public void CreateProjectContainer_HasHttpPostAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageProjectController.CreateProjectContainer),
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
