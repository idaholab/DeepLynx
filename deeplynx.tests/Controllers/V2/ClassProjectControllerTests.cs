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
///     Unit tests for the V2 actions of <see cref="ClassProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class ClassProjectControllerTests : IDisposable
{
    private readonly Mock<IClassBusiness> _mockClassBusiness;
    private readonly Mock<ILogger<ClassProjectController>> _mockLogger;
    private readonly ClassProjectController _classProjectController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long ClassIdConst = 20L;

    public ClassProjectControllerTests()
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
    // GetAllClasses Tests
    // =========================================================================

    #region GetAllClasses Tests

    [Fact]
    public async Task GetAllClasses_Returns200_WithList()
    {
        var expected = new PaginatedResponse<ClassResponseDto>
        {
            Items = new List<ClassResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
                         It.IsAny<PaginatedRequestDto>(), true, false, false))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.GetAllClasses(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllClasses_Returns200_WithEmptyList()
    {
        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]?>(),
                         It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync(new PaginatedResponse<ClassResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        var result = (await _classProjectController.GetAllClasses(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<ClassResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllClasses_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]?>(),
                         It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.GetAllClasses(ProjectId, true));
    }

    [Fact]
    public async Task GetAllClasses_PassesProjectIdAndHideArchivedToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
                         It.IsAny<PaginatedRequestDto>(), false, false, false))
                     .ReturnsAsync(new PaginatedResponse<ClassResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _classProjectController.GetAllClasses(ProjectId, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetAllClassesPaginated(
            UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
            It.IsAny<PaginatedRequestDto>(), false, false, false), Times.Once);
    }

    [Fact]
    public async Task GetAllClasses_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
                         true, false, false))
                     .ReturnsAsync(new PaginatedResponse<ClassResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _classProjectController.GetAllClasses(ProjectId, true);

        _mockClassBusiness.Verify(b => b.GetAllClassesPaginated(
            UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
            true, false, false), Times.Once);
    }

    [Fact]
    public async Task GetAllClasses_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 3, PageSize = 10 };

        _mockClassBusiness.Setup(b => b.GetAllClassesPaginated(
                         UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 3 && p.PageSize == 10),
                         true, false, false))
                     .ReturnsAsync(new PaginatedResponse<ClassResponseDto>
                     {
                         Items = [],
                         PageNumber = 3,
                         PageSize = 10,
                         TotalCount = 0
                     });

        await _classProjectController.GetAllClasses(ProjectId, true, pagination);

        _mockClassBusiness.Verify(b => b.GetAllClassesPaginated(
            UserId, OrgId, It.Is<long[]>(ids => ids.SequenceEqual(new[] { ProjectId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 3 && p.PageSize == 10),
            true, false, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetClass Tests
    // =========================================================================

    #region GetClass Tests

    [Fact]
    public async Task GetClass_Returns200_WithClass()
    {
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, ProjectId, ClassIdConst, true))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.GetClass(
            ProjectId, ClassIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetClass_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.GetClass(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.GetClass(
            ProjectId, ClassIdConst, true));
    }

    [Fact]
    public async Task GetClass_PassesIdsToBusinessLayer()
    {
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.GetClass(OrgId, ProjectId, ClassIdConst, false))
                     .ReturnsAsync(expected);

        await _classProjectController.GetClass(ProjectId, ClassIdConst, hideArchived: false);

        _mockClassBusiness.Verify(b => b.GetClass(OrgId, ProjectId, ClassIdConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateClass Tests
    // =========================================================================

    #region CreateClass Tests

    [Fact]
    public async Task CreateClass_Returns200_WithCreatedClass()
    {
        var request = new CreateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.CreateClass(ProjectId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateClass_ThrowsException_WhenBusinessThrows()
    {
        var request = new CreateClassRequestDto();

        _mockClassBusiness.Setup(b => b.CreateClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<CreateClassRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.CreateClass(ProjectId, request));
    }

    [Fact]
    public async Task CreateClass_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.CreateClass(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        await _classProjectController.CreateClass(ProjectId, request);

        _mockClassBusiness.Verify(b => b.CreateClass(UserId, OrgId, ProjectId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // BulkCreateClasses Tests
    // =========================================================================

    #region BulkCreateClasses Tests

    [Fact]
    public async Task BulkCreateClasses_Returns200_WithCreatedClasses()
    {
        var request = new List<CreateClassRequestDto> { new(), new() };
        var expected = new List<ClassResponseDto> { new(), new() };

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.BulkCreateClasses(ProjectId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateClasses_Returns200_WithEmptyList()
    {
        _mockClassBusiness.Setup(b => b.BulkCreateClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<List<CreateClassRequestDto>>()))
                     .ReturnsAsync([]);

        var result = (await _classProjectController.BulkCreateClasses(ProjectId, [])).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<List<ClassResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateClasses_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.BulkCreateClasses(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<List<CreateClassRequestDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.BulkCreateClasses(ProjectId, []));
    }

    [Fact]
    public async Task BulkCreateClasses_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new List<CreateClassRequestDto> { new() };
        var expected = new List<ClassResponseDto> { new() };

        _mockClassBusiness.Setup(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request))
                     .ReturnsAsync(expected);

        await _classProjectController.BulkCreateClasses(ProjectId, request);

        _mockClassBusiness.Verify(b => b.BulkCreateClasses(UserId, OrgId, ProjectId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateClass Tests
    // =========================================================================

    #region UpdateClass Tests

    [Fact]
    public async Task UpdateClass_Returns200_WithUpdatedClass()
    {
        var dto = new UpdateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        var result = (await _classProjectController.UpdateClass(
            ProjectId, ClassIdConst, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateClass_ThrowsException_WhenBusinessThrows()
    {
        var dto = new UpdateClassRequestDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                         It.IsAny<UpdateClassRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.UpdateClass(
            ProjectId, ClassIdConst, dto));
    }

    [Fact]
    public async Task UpdateClass_PassesIdsAndDtoToBusinessLayer()
    {
        var dto = new UpdateClassRequestDto();
        var expected = new ClassResponseDto();

        _mockClassBusiness.Setup(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto))
                     .ReturnsAsync(expected);

        await _classProjectController.UpdateClass(ProjectId, ClassIdConst, dto);

        _mockClassBusiness.Verify(b => b.UpdateClass(UserId, OrgId, ProjectId, ClassIdConst, dto), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteClass Tests
    // =========================================================================

    #region DeleteClass Tests

    [Fact]
    public async Task DeleteClass_Returns200_OnSuccess()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.DeleteClass(ProjectId, ClassIdConst) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteClass_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.DeleteClass(ProjectId, ClassIdConst));
    }

    [Fact]
    public async Task DeleteClass_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        await _classProjectController.DeleteClass(ProjectId, ClassIdConst);

        _mockClassBusiness.Verify(b => b.DeleteClass(UserId, OrgId, ProjectId, ClassIdConst), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveClass Tests
    // =========================================================================

    #region ArchiveClass Tests

    [Fact]
    public async Task ArchiveClass_Returns200_OnArchive()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.ArchiveClass(
            ProjectId, ClassIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task ArchiveClass_Returns200_OnUnarchive()
    {
        _mockClassBusiness.Setup(b => b.UnarchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        var result = await _classProjectController.ArchiveClass(
            ProjectId, ClassIdConst, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task ArchiveClass_ThrowsException_WhenBusinessThrows()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _classProjectController.ArchiveClass(
            ProjectId, ClassIdConst, archive: true));
    }

    [Fact]
    public async Task ArchiveClass_PassesIdsAndArchiveFlagToBusinessLayer()
    {
        _mockClassBusiness.Setup(b => b.ArchiveClass(UserId, OrgId, ProjectId, ClassIdConst))
                     .ReturnsAsync(true);

        await _classProjectController.ArchiveClass(ProjectId, ClassIdConst, archive: true);

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
    public void GetAllClasses_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.GetAllClasses),
            "projectId", "hideArchived", "paginatedRequestDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void GetClass_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.GetClass),
            "projectId", "classId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "class");
    }

    [Fact]
    public void CreateClass_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.CreateClass),
            "projectId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void BulkCreateClasses_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.BulkCreateClasses),
            "projectId", "classes");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void UpdateClass_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.UpdateClass),
            "projectId", "classId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "class");
    }

    [Fact]
    public void DeleteClass_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.DeleteClass),
            "projectId", "classId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "class");
    }

    [Fact]
    public void ArchiveClass_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ClassProjectController.ArchiveClass),
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