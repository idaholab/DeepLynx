using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="ClassOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ClassOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<IClassBusiness> _mockClassBusiness;
    private readonly Mock<ILogger<ClassOrganizationController>> _mockLogger;
    private readonly ClassOrganizationController _classOrganizationController;

    private const long OrgId = 1L;
    private const long UserId = 10L;
    private const long ClassIdConst = 20L;
    private static readonly long[] ProjectIdsConst = [13L, 14L];

    public ClassOrganizationControllerTestsV2()
    {
        _mockClassBusiness = new Mock<IClassBusiness>();
        _mockLogger = new Mock<ILogger<ClassOrganizationController>>();

        _classOrganizationController = new ClassOrganizationController(
            _mockClassBusiness.Object,
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
    // GetAllClassesV2 Tests
    // =========================================================================

    #region GetAllClassesV2 Tests

    [Fact]
    public async Task GetAllClassesV2_Returns200_WithList()
    {
        var expected = new List<ClassResponseDto> { new(), new() };

        _mockClassBusiness.Setup(b => b.GetAllClasses(UserId, OrgId, ProjectIdsConst, true))
                     .ReturnsAsync(expected);

        var result = (await _classOrganizationController.GetAllClassesV2(
            OrgId, ProjectIdsConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllClassesV2_Returns200_WithEmptyList()
    {
        _mockClassBusiness.Setup(b => b.GetAllClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _classOrganizationController.GetAllClassesV2(
            OrgId, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ClassResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllClassesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.GetAllClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.GetAllClassesV2(
            OrgId, null, true));
    }

    [Fact]
    public async Task GetAllClassesV2_PassesIdsAndHideArchivedToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.GetAllClasses(UserId, OrgId, ProjectIdsConst, false))
                     .ReturnsAsync([]);

        await _classOrganizationController.GetAllClassesV2(OrgId, ProjectIdsConst, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetAllClasses(UserId, OrgId, ProjectIdsConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetClassV2 Tests
    // =========================================================================

    #region GetClassV2 Tests

    [Fact]
    public async Task GetClassV2_Returns200_WithClass()
    {
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, null, ClassIdConst, true))
                     .ReturnsAsync(expected);

        var result = (await _classOrganizationController.GetClassV2(
            OrgId, ClassIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetClassV2_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.GetClass(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.GetClassV2(
            OrgId, ClassIdConst, true));
    }

    [Fact]
    public async Task GetClassV2_PassesIdsToBusinessLayer()
    {
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, null, ClassIdConst, false))
                     .ReturnsAsync(expected);

        await _classOrganizationController.GetClassV2(OrgId, ClassIdConst, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetClass(OrgId, null, ClassIdConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateClassV2 Tests
    // =========================================================================

    #region CreateClassV2 Tests

    [Fact]
    public async Task CreateClassV2_Returns200_WithCreatedClass()
    {
        var request = new CreateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, null, request))
                     .ReturnsAsync(expected);

        var result = (await _classOrganizationController.CreateClassV2(OrgId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateClassV2_ThrowsException_WhenBusinessThrows()
    {
        var request = new CreateClassRequestDto();

        _mockClassBusiness.Setup(b => b.CreateClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<CreateClassRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.CreateClassV2(OrgId, request));
    }

    [Fact]
    public async Task CreateClassV2_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, null, request))
                     .ReturnsAsync(expected);

        await _classOrganizationController.CreateClassV2(OrgId, request);

        _mockClassBusiness.Verify(b => b.CreateClass(UserId, OrgId, null, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // BulkCreateClassesV2 Tests
    // =========================================================================

    #region BulkCreateClassesV2 Tests

    [Fact]
    public async Task BulkCreateClassesV2_Returns200_WithCreatedClasses()
    {
        var request = new List<CreateClassRequestDto> { new(), new() };
        var expected = new List<ClassResponseDto> { new(), new() };

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, null, request))
                     .ReturnsAsync(expected);

        var result = (await _classOrganizationController.BulkCreateClassesV2(OrgId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateClassesV2_Returns200_WithEmptyList()
    {
        _mockClassBusiness.Setup(b => b.BulkCreateClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<List<CreateClassRequestDto>>()))
                     .ReturnsAsync([]);

        var result = (await _classOrganizationController.BulkCreateClassesV2(OrgId, [])).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<List<ClassResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateClassesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.BulkCreateClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<List<CreateClassRequestDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.BulkCreateClassesV2(OrgId, []));
    }

    [Fact]
    public async Task BulkCreateClassesV2_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new List<CreateClassRequestDto> { new() };
        var expected = new List<ClassResponseDto> { new() };

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, null, request))
                     .ReturnsAsync(expected);

        await _classOrganizationController.BulkCreateClassesV2(OrgId, request);

        _mockClassBusiness.Verify(b => b.BulkCreateClasses(UserId, OrgId, null, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateClassV2 Tests
    // =========================================================================

    #region UpdateClassV2 Tests

    [Fact]
    public async Task UpdateClassV2_Returns200_WithUpdatedClass()
    {
        var dto = new UpdateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, null, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        var result = (await _classOrganizationController.UpdateClassV2(
            OrgId, ClassIdConst, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateClassV2_ThrowsException_WhenBusinessThrows()
    {
        var dto = new UpdateClassRequestDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                         It.IsAny<UpdateClassRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.UpdateClassV2(
            OrgId, ClassIdConst, dto));
    }

    [Fact]
    public async Task UpdateClassV2_PassesIdsAndDtoToBusinessLayer()
    {
        var dto = new UpdateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, null, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        await _classOrganizationController.UpdateClassV2(OrgId, ClassIdConst, dto);

        _mockClassBusiness.Verify(b => b.UpdateClass(UserId, OrgId, null, ClassIdConst, dto), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteClassV2 Tests
    // =========================================================================

    #region DeleteClassV2 Tests

    [Fact]
    public async Task DeleteClassV2_Returns200_OnSuccess()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, null, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classOrganizationController.DeleteClassV2(OrgId, ClassIdConst) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteClassV2_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.DeleteClassV2(OrgId, ClassIdConst));
    }

    [Fact]
    public async Task DeleteClassV2_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, null, ClassIdConst))
                     .ReturnsAsync(true);

        await _classOrganizationController.DeleteClassV2(OrgId, ClassIdConst);

        _mockClassBusiness.Verify(b => b.DeleteClass(UserId, OrgId, null, ClassIdConst), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveClassV2 Tests
    // =========================================================================

    #region ArchiveClassV2 Tests

    [Fact]
    public async Task ArchiveClassV2_Returns200_OnArchive()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, null, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classOrganizationController.ArchiveClassV2(
            OrgId, ClassIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task ArchiveClassV2_Returns200_OnUnarchive()
    {
        _mockClassBusiness.Setup(b => b.UnarchiveClass(UserId, OrgId, null, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classOrganizationController.ArchiveClassV2(
            OrgId, ClassIdConst, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task ArchiveClassV2_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classOrganizationController.ArchiveClassV2(
            OrgId, ClassIdConst, archive: true));
    }

    [Fact]
    public async Task ArchiveClassV2_PassesIdsAndArchiveFlagToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, null, ClassIdConst))
                     .ReturnsAsync(true);

        await _classOrganizationController.ArchiveClassV2(OrgId, ClassIdConst, archive: true);

        _mockClassBusiness.Verify(b => b.ArchiveClass(UserId, OrgId, null, ClassIdConst), Times.Once);
        _mockClassBusiness.Verify(b => b.UnarchiveClass(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void ClassOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(ClassOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllClassesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.GetAllClassesV2),
            "organizationId", "projectIds", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void GetClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.GetClassV2),
            "organizationId", "classId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void CreateClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.CreateClassV2),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void BulkCreateClassesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.BulkCreateClassesV2),
            "organizationId", "classes");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void UpdateClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.UpdateClassV2),
            "organizationId", "classId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "class");
    }

    [Fact]
    public void DeleteClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.DeleteClassV2),
            "organizationId", "classId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void ArchiveClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassOrganizationController.ArchiveClassV2),
            "organizationId", "classId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "class");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(ClassOrganizationController).GetMethods()
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
}