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
///     Unit tests for <see cref="ProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>

public class ProjectControllerTests : IDisposable
{
    private readonly Mock<IProjectBusiness> _mockBusiness;
    private readonly Mock<IInvitationBusiness> _mockInvitationBusiness;
    private readonly Mock<ILogger<ProjectController>> _mockLogger;
    private readonly ProjectController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long OtherUserId = 11L;
    private const long RoleId = 20L;
    private const long GroupId = 30L;

    public ProjectControllerTests()
    {
        _mockBusiness = new Mock<IProjectBusiness>();
        _mockInvitationBusiness = new Mock<IInvitationBusiness>();
        _mockLogger = new Mock<ILogger<ProjectController>>();

        _controller = new ProjectController(
            _mockBusiness.Object,
            _mockInvitationBusiness.Object,
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
    // GetAllProjects Tests
    // =========================================================================

    #region GetAllProjects Tests

    [Fact]
    public async Task GetAllProjects_Returns200_WithList()
    {
        var expected = new List<ProjectResponseDto>
        {
            new(),
            new()
        };

        _mockBusiness.Setup(b => b.GetAllProjects(UserId, OrgId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllProjects(OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllProjects_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllProjects(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllProjects(OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ProjectResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllProjects_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetAllProjects(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetAllProjects(OrgId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllProjects_PassesCurrentUserIdAndHideArchivedToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;

        _mockBusiness.Setup(b => b.GetAllProjects(77L, OrgId, false))
                     .ReturnsAsync([]);

        await _controller.GetAllProjects(OrgId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllProjects(77L, OrgId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetAllProjectsByUser Tests
    // =========================================================================

    #region GetAllProjectsByUser Tests

    [Fact]
    public async Task GetAllProjectsByUser_Returns200_WithList()
    {
        var expected = new List<ProjectResponseDto>
        {
            new(),
            new()
        };

        _mockBusiness.Setup(b => b.GetAllProjects(OtherUserId, OrgId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllProjectsByUser(
            OrgId, OtherUserId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllProjectsByUser_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllProjects(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllProjectsByUser(
            OrgId, OtherUserId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ProjectResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllProjectsByUser_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetAllProjects(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetAllProjectsByUser(
            OrgId, OtherUserId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllProjectsByUser_PassesProvidedUserIdAndHideArchivedToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetAllProjects(OtherUserId, OrgId, false))
                     .ReturnsAsync([]);

        await _controller.GetAllProjectsByUser(
            OrgId, OtherUserId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllProjects(OtherUserId, OrgId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetProject Tests
    // =========================================================================

    #region GetProject Tests

    [Fact]
    public async Task GetProject_Returns200_WithProject()
    {
        var expected = new ProjectResponseDto();

        _mockBusiness.Setup(b => b.GetProject(OrgId, ProjectId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetProject(
            OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetProject(
            OrgId, ProjectId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetProject_PassesHideArchivedToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetProject(OrgId, ProjectId, false))
                     .ReturnsAsync(new ProjectResponseDto());

        await _controller.GetProject(OrgId, ProjectId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetProject(OrgId, ProjectId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateProject Tests
    // =========================================================================

    #region CreateProject Tests

    [Fact]
    public async Task CreateProject_Returns200_WithProject()
    {
        var dto = new CreateProjectRequestDto();
        var expected = new ProjectResponseDto();

        _mockBusiness.Setup(b => b.CreateProject(UserId, OrgId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateProject(
            OrgId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.CreateProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CreateProjectRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.CreateProject(
            OrgId, new CreateProjectRequestDto())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateProject_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;

        var dto = new CreateProjectRequestDto();

        _mockBusiness.Setup(b => b.CreateProject(77L, OrgId, dto))
                     .ReturnsAsync(new ProjectResponseDto());

        await _controller.CreateProject(OrgId, dto);

        _mockBusiness.Verify(b => b.CreateProject(77L, OrgId, dto), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateProject Tests
    // =========================================================================

    #region UpdateProject Tests

    [Fact]
    public async Task UpdateProject_Returns200_WithUpdatedProject()
    {
        var dto = new UpdateProjectRequestDto();
        var expected = new ProjectResponseDto();

        _mockBusiness.Setup(b => b.UpdateProject(UserId, OrgId, ProjectId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateProject(
            OrgId, ProjectId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UpdateProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<UpdateProjectRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.UpdateProject(
            OrgId, ProjectId, new UpdateProjectRequestDto())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;

        var dto = new UpdateProjectRequestDto();

        _mockBusiness.Setup(b => b.UpdateProject(77L, OrgId, ProjectId, dto))
                     .ReturnsAsync(new ProjectResponseDto());

        await _controller.UpdateProject(OrgId, ProjectId, dto);

        _mockBusiness.Verify(b => b.UpdateProject(77L, OrgId, ProjectId, dto), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteProject Tests
    // =========================================================================

    #region DeleteProject Tests

    [Fact]
    public async Task DeleteProject_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.DeleteProject(UserId, OrgId, ProjectId))
                     .Returns(Task.FromResult(true));

        var result = await _controller.DeleteProject(OrgId, ProjectId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.DeleteProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.DeleteProject(OrgId, ProjectId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;

        _mockBusiness.Setup(b => b.DeleteProject(77L, OrgId, ProjectId))
                     .Returns(Task.FromResult(true));

        await _controller.DeleteProject(OrgId, ProjectId);

        _mockBusiness.Verify(b => b.DeleteProject(77L, OrgId, ProjectId), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveProject Tests
    // =========================================================================

    #region ArchiveProject Tests

    [Fact]
    public async Task ArchiveProject_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveProject(UserId, OrgId, ProjectId))
                     .Returns(Task.FromResult(true));

        var result = await _controller.ArchiveProject(
            OrgId, ProjectId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        _mockBusiness.Verify(b => b.ArchiveProject(UserId, OrgId, ProjectId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveProject(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveProject_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveProject(UserId, OrgId, ProjectId))
                     .Returns(Task.FromResult(true));

        var result = await _controller.ArchiveProject(
            OrgId, ProjectId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        _mockBusiness.Verify(b => b.UnarchiveProject(UserId, OrgId, ProjectId), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveProject(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveProject_WhenArchiveTrue_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.ArchiveProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.ArchiveProject(
            OrgId, ProjectId, archive: true) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task ArchiveProject_WhenArchiveFalse_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UnarchiveProject(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.ArchiveProject(
            OrgId, ProjectId, archive: false) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    #endregion

    // =========================================================================
    // ProjectStats Tests
    // =========================================================================

    #region ProjectStats Tests

    [Fact]
    public async Task ProjectStats_Returns200_WithStats()
    {
        var expected = new ProjectStatResponseDto();

        _mockBusiness.Setup(b => b.GetProjectStats(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        var result = (await _controller.ProjectStats(
            OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ProjectStats_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetProjectStats(
                         It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.ProjectStats(
            OrgId, ProjectId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    #endregion

    // =========================================================================
    // GetProjectMembers Tests
    // =========================================================================

    #region GetProjectMembers Tests

    [Fact]
    public async Task GetProjectMembers_Returns200_WithMembers()
    {
        var expected = new List<ProjectMemberResponseDto>
        {
            new()
        };

        _mockBusiness.Setup(b => b.GetProjectMembers(ProjectId))
                    .ReturnsAsync(expected);

        var result = (await _controller.GetProjectMembers(
            OrgId, ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetProjectMembers_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetProjectMembers(It.IsAny<long>()))
                    .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetProjectMembers(
            OrgId, ProjectId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetProjectMembers_PassesProjectIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetProjectMembers(ProjectId))
                    .ReturnsAsync(new List<ProjectMemberResponseDto>());

        await _controller.GetProjectMembers(OrgId, ProjectId);

        _mockBusiness.Verify(b => b.GetProjectMembers(ProjectId), Times.Once);
    }

    #endregion

    // =========================================================================
    // AddMemberToProject Tests
    // =========================================================================

    #region AddMemberToProject Tests

    [Fact]
    public async Task AddMemberToProject_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.AddMemberToProject(ProjectId, RoleId, OtherUserId, null))
                     .Returns(Task.FromResult(true));

        var result = await _controller.AddMemberToProject(
            OrgId, ProjectId, RoleId, OtherUserId, groupId: null) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task AddMemberToProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.AddMemberToProject(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.AddMemberToProject(
            OrgId, ProjectId, RoleId, OtherUserId, groupId: null) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task AddMemberToProject_PassesParametersToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.AddMemberToProject(ProjectId, RoleId, null, GroupId))
                     .Returns(Task.FromResult(true));

        await _controller.AddMemberToProject(
            OrgId, ProjectId, RoleId, userId: null, groupId: GroupId);

        _mockBusiness.Verify(
            b => b.AddMemberToProject(ProjectId, RoleId, null, GroupId),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateProjectMemberRole Tests
    // =========================================================================

    #region UpdateProjectMemberRole Tests

    [Fact]
    public async Task UpdateProjectMemberRole_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.UpdateProjectMemberRole(ProjectId, RoleId, OtherUserId, null))
                     .Returns(Task.FromResult(true));

        var result = await _controller.UpdateProjectMemberRole(
            OrgId, ProjectId, RoleId, OtherUserId, groupId: null) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UpdateProjectMemberRole(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.UpdateProjectMemberRole(
            OrgId, ProjectId, RoleId, OtherUserId, groupId: null) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_PassesParametersToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.UpdateProjectMemberRole(ProjectId, RoleId, null, GroupId))
                     .Returns(Task.FromResult(true));

        await _controller.UpdateProjectMemberRole(
            OrgId, ProjectId, RoleId, userId: null, groupId: GroupId);

        _mockBusiness.Verify(
            b => b.UpdateProjectMemberRole(ProjectId, RoleId, null, GroupId),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // RemoveMemberFromProject Tests
    // =========================================================================

    #region RemoveMemberFromProject Tests

    [Fact]
    public async Task RemoveMemberFromProject_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.RemoveMemberFromProject(ProjectId, OtherUserId, null))
                     .Returns(Task.FromResult(true));

        var result = await _controller.RemoveMemberFromProject(
            OrgId, ProjectId, OtherUserId, groupId: null) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.RemoveMemberFromProject(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.RemoveMemberFromProject(
            OrgId, ProjectId, OtherUserId, groupId: null) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task RemoveMemberFromProject_PassesParametersToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.RemoveMemberFromProject(ProjectId, null, GroupId))
                     .Returns(Task.FromResult(true));

        await _controller.RemoveMemberFromProject(
            OrgId, ProjectId, userId: null, groupId: GroupId);

        _mockBusiness.Verify(
            b => b.RemoveMemberFromProject(ProjectId, null, GroupId),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // InviteUserToProject Tests
    // =========================================================================

    #region InviteUserToProject Tests

    [Fact]
    public async Task InviteUserToProject_Returns200_OnSuccess()
    {
        const string userEmail = "test@example.com";

        _mockInvitationBusiness.Setup(b => b.InviteAndAddUserToHierarchy(
                                   OrgId, ProjectId, GroupId, RoleId, OtherUserId, userEmail))
                               .Returns(Task.FromResult(true));

        var result = await _controller.InviteUserToProject(
            OrgId, ProjectId, userEmail, OtherUserId, GroupId, RoleId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InviteUserToProject_Returns500_OnUnexpectedException()
    {
        _mockInvitationBusiness.Setup(b => b.InviteAndAddUserToHierarchy(
                                   It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                                   It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string?>()))
                               .ThrowsAsync(new Exception("db error"));

        var result = await _controller.InviteUserToProject(
            OrgId, ProjectId, "test@example.com", OtherUserId, GroupId, RoleId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task InviteUserToProject_PassesParametersToInvitationBusinessLayer()
    {
        const string userEmail = "test@example.com";

        _mockInvitationBusiness.Setup(b => b.InviteAndAddUserToHierarchy(
                                   OrgId, ProjectId, GroupId, RoleId, OtherUserId, userEmail))
                               .Returns(Task.FromResult(true));

        await _controller.InviteUserToProject(
            OrgId, ProjectId, userEmail, OtherUserId, GroupId, RoleId);

        _mockInvitationBusiness.Verify(
            b => b.InviteAndAddUserToHierarchy(
                OrgId, ProjectId, GroupId, RoleId, OtherUserId, userEmail),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void ProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(ProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllProjects_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.GetAllProjects),
            "organizationId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void GetAllProjectsByUser_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.GetAllProjectsByUser),
            "organizationId",
            "userId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void GetProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.GetProject),
            "organizationId",
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void CreateProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.CreateProject),
            "organizationId",
            "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void UpdateProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.UpdateProject),
            "organizationId",
            "projectId",
            "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void DeleteProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.DeleteProject),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void ArchiveProject_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.ArchiveProject),
            "organizationId",
            "projectId",
            "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertHasSensitivityEnabledAuthAttribute(method, "update", "project");
    }

    [Fact]
    public void ProjectStats_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.ProjectStats),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void GetProjectMembers_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.GetProjectMembers),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "project");
        AssertHasAuthAttribute(method, "read", "user");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void AddMemberToProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.AddMemberToProject),
            "organizationId",
            "projectId",
            "roleId",
            "userId",
            "groupId",
            "isProjectAdmin");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertHasAuthAttribute(method, "update", "user");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void UpdateProjectMemberRole_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.UpdateProjectMemberRole),
            "organizationId",
            "projectId",
            "roleId",
            "userId",
            "groupId");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertHasAuthAttribute(method, "update", "user");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void SetProjectAdminStatus_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.SetProjectAdminStatus),
            "organizationId",
            "projectId",
            "userId",
            "groupId",
            "isAdmin");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertHasAuthAttribute(method, "update", "user");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void RemoveMemberFromProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.RemoveMemberFromProject),
            "organizationId",
            "projectId",
            "userId",
            "groupId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "update", "project");
        AssertHasAuthAttribute(method, "update", "user");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    [Fact]
    public void InviteUserToProject_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(ProjectController.InviteUserToProject),
            "organizationId",
            "projectId",
            "userEmail",
            "userId",
            "groupId",
            "roleId");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "user");
        AssertHasAuthAttribute(method, "update", "user");
        AssertHasAuthAttribute(method, "update", "project");
        AssertDoesNotHaveSensitivityEnabledAuthAttribute(method);
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(ProjectController).GetMethods()
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

    private static void AssertHasSensitivityEnabledAuthAttribute(
        System.Reflection.MethodInfo method,
        string expectedAction,
        string expectedResource)
    {
        var authAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
            .ToList();

        Assert.Contains(authAttributes, attribute =>
            attribute.ConstructorArguments.Count >= 3 &&
            attribute.ConstructorArguments[0].Value?.ToString() == expectedAction &&
            attribute.ConstructorArguments[1].Value?.ToString() == expectedResource &&
            attribute.ConstructorArguments[2].Value is bool sensitivity &&
            sensitivity);
    }

    private static void AssertDoesNotHaveSensitivityEnabledAuthAttribute(
        System.Reflection.MethodInfo method)
    {
        var sensitivityEnabledAuthAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
            .Where(attribute =>
                attribute.ConstructorArguments.Count >= 3 &&
                attribute.ConstructorArguments[2].Value is bool sensitivity &&
                sensitivity)
            .ToList();

        Assert.Empty(sensitivityEnabledAuthAttributes);
    }
}