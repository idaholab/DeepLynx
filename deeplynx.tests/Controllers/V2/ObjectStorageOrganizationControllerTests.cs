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
///     Unit tests for the V2 actions of <see cref="ObjectStorageOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ObjectStorageOrganizationControllerTests : IDisposable
{
    private readonly Mock<IObjectStorageBusiness> _mockBusiness;
    private readonly Mock<ILogger<ObjectStorageProjectController>> _mockLogger;
    private readonly ObjectStorageOrganizationController _controller;

    private const long OrgId = 1L;
    private const long ObjectStorageId = 5L;
    private const long UserId = 10L;

    public ObjectStorageOrganizationControllerTests()
    {
        _mockBusiness = new Mock<IObjectStorageBusiness>();
        _mockLogger = new Mock<ILogger<ObjectStorageProjectController>>();

        _controller = new ObjectStorageOrganizationController(
            _mockBusiness.Object,
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
    // GetAllObjectStorages Tests
    // =========================================================================

    #region GetAllObjectStorages Tests

    [Fact]
    public async Task GetAllObjectStorages_Returns200_WithList()
    {
        var expected = new List<ObjectStorageResponseDto> { new(), new() };
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, null, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllObjectStorages(OrgId, true)).Result as OkObjectResult;

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

        var result = (await _controller.GetAllObjectStorages(OrgId, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllObjectStorages(OrgId, true));
    }

    [Fact]
    public async Task GetAllObjectStorages_PassesNullProjectIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetAllObjectStorages(OrgId, null, false))
                     .ReturnsAsync([]);

        await _controller.GetAllObjectStorages(OrgId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllObjectStorages(OrgId, null, false), Times.Once);
    }

    [Fact]
    public void GetAllObjectStorages_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.GetAllObjectStorages),
            "organizationId", "hideArchived");

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
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, null, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetObjectStorage(OrgId, ObjectStorageId, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.GetObjectStorage(OrgId, ObjectStorageId, true));
    }

    [Fact]
    public async Task GetObjectStorage_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetObjectStorage(OrgId, null, ObjectStorageId, true))
                     .ReturnsAsync(expected);

        await _controller.GetObjectStorage(OrgId, ObjectStorageId, true);

        _mockBusiness.Verify(b => b.GetObjectStorage(OrgId, null, ObjectStorageId, true), Times.Once);
    }

    [Fact]
    public void GetObjectStorage_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.GetObjectStorage),
            "organizationId", "objectStorageId", "hideArchived");

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

        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, null, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateObjectStorage(OrgId, dto)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateObjectStorage(OrgId, dto));
    }

    [Fact]
    public async Task CreateObjectStorage_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new CreateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.CreateObjectStorage(UserId, OrgId, null, dto))
                     .ReturnsAsync(expected);

        await _controller.CreateObjectStorage(OrgId, dto);

        _mockBusiness.Verify(b => b.CreateObjectStorage(UserId, OrgId, null, dto), Times.Once);
    }

    [Fact]
    public void CreateObjectStorage_HasHttpPostAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.CreateObjectStorage),
            "organizationId", "dto");

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

        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, null, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateObjectStorage(OrgId, ObjectStorageId, dto)).Result as OkObjectResult;

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
            OrgId, ObjectStorageId, new UpdateObjectStorageRequestDto()));
    }

    [Fact]
    public async Task UpdateObjectStorage_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new UpdateObjectStorageRequestDto();
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.UpdateObjectStorage(UserId, OrgId, null, ObjectStorageId, dto))
                     .ReturnsAsync(expected);

        await _controller.UpdateObjectStorage(OrgId, ObjectStorageId, dto);

        _mockBusiness.Verify(b => b.UpdateObjectStorage(UserId, OrgId, null, ObjectStorageId, dto), Times.Once);
    }

    [Fact]
    public void UpdateObjectStorage_HasHttpPutAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.UpdateObjectStorage),
            "organizationId", "objectStorageId", "dto");

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
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.DeleteObjectStorage(OrgId, ObjectStorageId) as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteObjectStorage(OrgId, ObjectStorageId));
    }

    [Fact]
    public async Task DeleteObjectStorage_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(true);

        await _controller.DeleteObjectStorage(OrgId, ObjectStorageId);

        _mockBusiness.Verify(b => b.DeleteObjectStorage(UserId, OrgId, null, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void DeleteObjectStorage_HasHttpDeleteAndWriteObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.DeleteObjectStorage),
            "organizationId", "objectStorageId");

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
        _mockBusiness.Setup(b => b.ArchiveObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorage(
            OrgId, ObjectStorageId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveObjectStorage(UserId, OrgId, null, ObjectStorageId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveObjectStorage_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveObjectStorage(
            OrgId, ObjectStorageId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveObjectStorage(UserId, OrgId, null, ObjectStorageId), Times.Once);
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
            _controller.ArchiveObjectStorage(OrgId, ObjectStorageId, archive: true));
    }

    [Fact]
    public void ArchiveObjectStorage_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.ArchiveObjectStorage),
            "organizationId", "objectStorageId", "archive");

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
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, null))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultObjectStorage(OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultObjectStorage_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(It.IsAny<long>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultObjectStorage(OrgId));
    }

    [Fact]
    public async Task GetDefaultObjectStorage_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultObjectStorage(OrgId, null))
                     .ReturnsAsync(expected);

        await _controller.GetDefaultObjectStorage(OrgId);

        _mockBusiness.Verify(b => b.GetDefaultObjectStorage(OrgId, null), Times.Once);
    }

    [Fact]
    public void GetDefaultObjectStorage_HasHttpGetAndReadObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.GetDefaultObjectStorage),
            "organizationId");

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
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultObjectStorage(OrgId, ObjectStorageId)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultObjectStorage(OrgId, ObjectStorageId));
    }

    [Fact]
    public async Task SetDefaultObjectStorage_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new ObjectStorageResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultObjectStorage(UserId, OrgId, null, ObjectStorageId))
                     .ReturnsAsync(expected);

        await _controller.SetDefaultObjectStorage(OrgId, ObjectStorageId);

        _mockBusiness.Verify(b => b.SetDefaultObjectStorage(UserId, OrgId, null, ObjectStorageId), Times.Once);
    }

    [Fact]
    public void SetDefaultObjectStorage_HasHttpPatchAndUpdateObjectStorageAuthorization()
    {
        var method = GetControllerMethod(
            nameof(ObjectStorageOrganizationController.SetDefaultObjectStorage),
            "organizationId", "objectStorageId");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "object_storage");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void ObjectStorageOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(ObjectStorageOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void ObjectStorageOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(ObjectStorageOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(ObjectStorageOrganizationController).GetMethods()
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