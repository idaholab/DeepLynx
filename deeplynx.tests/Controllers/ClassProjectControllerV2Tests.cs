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
///     Unit tests for the V2 actions of <see cref="ClassProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ClassProjectControllerTestsV2 : IDisposable
{
    private readonly Mock<IClassBusiness> _mockClassBusiness;
    private readonly Mock<ILogger<ClassProjectController>> _mockLogger;
    private readonly ClassProjectController _classProjectController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long ClassIdConst = 20L;

    public ClassProjectControllerTestsV2()
    {
        _mockClassBusiness = new Mock<IClassBusiness>();
        _mockLogger = new Mock<ILogger<ClassProjectController>>();

        _classProjectController = new ClassProjectController(
            _mockClassBusiness.Object,
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
    // GetAllClassesV2 Tests
    // =========================================================================

    #region GetAllClassesV2 Tests

    [Fact]
    public async Task GetAllClassesV2_Returns200_WithList()
    {
        var expected = new List<ClassResponseDto> { new(), new() };

        _mockClassBusiness.Setup(b => b.GetAllClasses(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })), true))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.GetAllClassesV2(ProjectId, true)).Result as OkObjectResult;

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

        var result = (await _classProjectController.GetAllClassesV2(ProjectId, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.GetAllClassesV2(ProjectId, true));
    }

    [Fact]
    public async Task GetAllClassesV2_PassesProjectIdAndHideArchivedToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.GetAllClasses(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })), false))
                     .ReturnsAsync([]);

        await _classProjectController.GetAllClassesV2(ProjectId, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetAllClasses(
            UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })), false), Times.Once);
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

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, ProjectId, ClassIdConst, true))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.GetClassV2(
            ProjectId, ClassIdConst, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.GetClassV2(
            ProjectId, ClassIdConst, true));
    }

    [Fact]
    public async Task GetClassV2_PassesIdsToBusinessLayer()
    {
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, ProjectId, ClassIdConst, false))
                     .ReturnsAsync(expected);

        await _classProjectController.GetClassV2(ProjectId, ClassIdConst, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetClass(OrgId, ProjectId, ClassIdConst, false), Times.Once);
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

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.CreateClassV2(ProjectId, request)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.CreateClassV2(ProjectId, request));
    }

    [Fact]
    public async Task CreateClassV2_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        await _classProjectController.CreateClassV2(ProjectId, request);

        _mockClassBusiness.Verify(b => b.CreateClass(UserId, OrgId, ProjectId, request), Times.Once);
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

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.BulkCreateClassesV2(ProjectId, request)).Result as OkObjectResult;

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

        var result = (await _classProjectController.BulkCreateClassesV2(ProjectId, [])).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.BulkCreateClassesV2(ProjectId, []));
    }

    [Fact]
    public async Task BulkCreateClassesV2_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new List<CreateClassRequestDto> { new() };
        var expected = new List<ClassResponseDto> { new() };

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        await _classProjectController.BulkCreateClassesV2(ProjectId, request);

        _mockClassBusiness.Verify(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request), Times.Once);
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

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.UpdateClassV2(
            ProjectId, ClassIdConst, dto)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.UpdateClassV2(
            ProjectId, ClassIdConst, dto));
    }

    [Fact]
    public async Task UpdateClassV2_PassesIdsAndDtoToBusinessLayer()
    {
        var dto = new UpdateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        await _classProjectController.UpdateClassV2(ProjectId, ClassIdConst, dto);

        _mockClassBusiness.Verify(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteClassV2 Tests
    // =========================================================================

    #region DeleteClassV2 Tests

    [Fact]
    public async Task DeleteClassV2_Returns200_OnSuccess()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.DeleteClassV2(ProjectId, ClassIdConst) as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.DeleteClassV2(ProjectId, ClassIdConst));
    }

    [Fact]
    public async Task DeleteClassV2_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        await _classProjectController.DeleteClassV2(ProjectId, ClassIdConst);

        _mockClassBusiness.Verify(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveClassV2 Tests
    // =========================================================================

    #region ArchiveClassV2 Tests

    [Fact]
    public async Task ArchiveClassV2_Returns200_OnArchive()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.ArchiveClassV2(
            ProjectId, ClassIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task ArchiveClassV2_Returns200_OnUnarchive()
    {
        _mockClassBusiness.Setup(b => b.UnarchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.ArchiveClassV2(
            ProjectId, ClassIdConst, archive: false) as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.ArchiveClassV2(
            ProjectId, ClassIdConst, archive: true));
    }

    [Fact]
    public async Task ArchiveClassV2_PassesIdsAndArchiveFlagToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        await _classProjectController.ArchiveClassV2(ProjectId, ClassIdConst, archive: true);

        _mockClassBusiness.Verify(b => b.ArchiveClass(UserId, OrgId, ProjectId, ClassIdConst), Times.Once);
        _mockClassBusiness.Verify(b => b.UnarchiveClass(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()), Times.Never);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void ClassProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(ClassProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllClassesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.GetAllClassesV2),
            "projectId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void GetClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.GetClassV2),
            "projectId", "classId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void CreateClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.CreateClassV2),
            "projectId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void BulkCreateClassesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.BulkCreateClassesV2),
            "projectId", "classes");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void UpdateClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.UpdateClassV2),
            "projectId", "classId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "class");
    }

    [Fact]
    public void DeleteClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.DeleteClassV2),
            "projectId", "classId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void ArchiveClassV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.ArchiveClassV2),
            "projectId", "classId", "archive");

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
        return Assert.Single(typeof(ClassProjectController).GetMethods()
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