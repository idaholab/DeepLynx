using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class UserControllerTests : IDisposable
{
    private readonly Mock<IUserBusiness> _mockUserBusiness;
    private readonly Mock<ILogger<UserController>> _mockLogger;
    private readonly UserController _userController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long PermissionId = 15L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private static readonly long[] PermissionList = { 13L, 14L };
    private const long RelationshipId = 22L;
    private const long TagId = 67L;


    public UserControllerTests()
    {
        _mockUserBusiness = new Mock<IUserBusiness>();
        _mockLogger = new Mock<ILogger<UserController>>();

        _userController = new UserController(
            _mockUserBusiness.Object,
            _mockLogger.Object
        );

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    // =========================================================================
    // GetAllUsers Tests
    // =========================================================================

    #region GetAllUsers Tests

    [Fact]
    public async Task GetAllUsers_Returns200_WithList()
    {
        var expected = new List<UserResponseDto>
        {
            new() { Id = 1, Name = "User 1", AccountType = "Dev", Email = "123@inl.gov", Username = "Testy", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now},
            new() { Id = 1, Name = "User 2", AccountType = "Dev", Email = "456@inl.gov", Username = "Tester", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now}
        };
        _mockUserBusiness.Setup(b => b.GetAllUsers(ProjectId, OrgId, false))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetAllUsers(ProjectId, OrgId, false)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllUsers_Returns200_WithEmptyList()
    {
        _mockUserBusiness.Setup(b => b.GetAllUsers(ProjectId, OrgId, false))
                     .ReturnsAsync([]);

        var result = (await _userController.GetAllUsers(ProjectId, OrgId, false)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllUsers_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetAllUsers(ProjectId, OrgId, false))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetAllUsers(ProjectId, OrgId, false)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_PassesToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        _mockUserBusiness.Setup(b => b.GetAllUsers(ProjectId, OrgId, false))
                     .ReturnsAsync([]);

        await _userController.GetAllUsers(ProjectId, OrgId, false);

        _mockUserBusiness.Verify(b => b.GetAllUsers(ProjectId, OrgId, false), Times.Once);
    }

    [Fact]
    public void GetAllUsers_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetAllUsers),
            "projectId",
            "organizationId",
            "includeArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // GetUser Tests
    // =========================================================================

    #region GetUser Tests

    [Fact]
    public async Task GetUser_Returns200_WithUser()
    {
        var expected = new UserResponseDto { Id = 1, Name = "User 1", AccountType = "Dev", Email = "123@inl.gov", Username = "Testy", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now };
        _mockUserBusiness.Setup(b => b.GetUser(UserId))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetUser(UserId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetUser_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetUser(UserId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetUser(UserId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetUser_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetUser),
            "userId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // GetLocalDevUser Tests
    // =========================================================================

    #region GetLocalDevUser Tests

    [Fact]
    public async Task GetLocalDevUser_Returns200_WithUser()
    {
        var expected = new UserResponseDto { Id = 1, Name = "User 1", AccountType = "Dev", Email = "123@inl.gov", Username = "Testy", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now };
        _mockUserBusiness.Setup(b => b.GetLocalDevUser())
                     .ReturnsAsync(expected);

        var result = (await _userController.GetLocalDevUser()).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetLocalDevUser_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetLocalDevUser())
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetLocalDevUser()).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetLocalDevUser_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetLocalDevUser));

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // CreateUser Tests
    // =========================================================================

    #region CreateUser Tests

    [Fact]
    public async Task CreateUser_Returns200_WithUser()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        var dto = new CreateUserRequestDto
        {
            Name = "Testing",
            Email = "789@inl.gov",
            Username = "og-117",
            IsArchived = false,
            IsActive = false
        };

        var expected = new UserResponseDto { Id = 1, Name = "User 1", AccountType = "Dev", Email = "123@inl.gov", Username = "Testy", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now };

        _mockUserBusiness.Setup(b => b.CreateUser(dto, true, false, false))
                    .ReturnsAsync(expected);

        var result = (await _userController.CreateUser(dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateUser_Returns500_OnUnexpectedException()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        var dto = new CreateUserRequestDto
        {
            Name = "Testing",
            Email = "789@inl.gov",
            Username = "og-117",
            IsArchived = false,
            IsActive = false
        };

        _mockUserBusiness.Setup(b => b.CreateUser(dto, true, false, false))
                    .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.CreateUser(dto)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateUser_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        var dto = new CreateUserRequestDto
        {
            Name = "Testing",
            Email = "789@inl.gov",
            Username = "og-117",
            IsArchived = false,
            IsActive = false
        };

        var expected = new UserResponseDto { Id = 1, Name = "User 1", AccountType = "Dev", Email = "123@inl.gov", Username = "Testy", IsActive = true, IsArchived = false, IsOrgAdmin = false, IsSysAdmin = true, LastLogin = System.DateTime.Now };

        _mockUserBusiness.Setup(b => b.CreateUser(dto, true, false, false))
                    .ReturnsAsync(expected);

        await _userController.CreateUser(dto);

        _mockUserBusiness.Verify(
            b => b.CreateUser(dto, true, false, false),
            Times.Once);
    }

    [Fact]
    public void CreateUser_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(UserController.CreateUser),
            "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");

    }

    #endregion

    // =========================================================================
    // UpdateUser Tests
    // =========================================================================

    #region UpdateUser Tests

    [Fact]
    public async Task UpdateUser_Returns200_WithUpdatedUser()
    {
        var dto = new UpdateUserRequestDto { Name = "Updated" };
        var expected = new UserResponseDto { Id = UserId, Name = "Updated" };

        _mockUserBusiness.Setup(b => b.UpdateUser(UserId, dto))
                     .ReturnsAsync(expected);

        var result = (await _userController.UpdateUser(UserId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateUser_Returns500_OnUnexpectedException()
    {
        var dto = new UpdateUserRequestDto { Name = "Updated" };

        _mockUserBusiness.Setup(b => b.UpdateUser(UserId, dto))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.UpdateUser(UserId, dto)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_PassesToBusinessLayer()
    {
        var dto = new UpdateUserRequestDto { Name = "Updated" };
        var expected = new UserResponseDto { Id = UserId, Name = "Updated" };

        _mockUserBusiness.Setup(b => b.UpdateUser(UserId, dto))
                     .ReturnsAsync(expected);

        await _userController.UpdateUser(UserId, dto);

        _mockUserBusiness.Verify(
            b => b.UpdateUser(UserId, dto),
            Times.Once);
    }

    [Fact]
    public void UpdateUser_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(UserController.UpdateUser),
            "userId",
            "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");

    }

    #endregion

    // =========================================================================
    // DeleteUser Tests
    // =========================================================================

    #region DeleteUser Tests

    [Fact]
    public async Task DeleteUser_Returns200_OnSuccess()
    {
        _mockUserBusiness.Setup(b => b.DeleteUser(UserId))
                     .ReturnsAsync(true);

        var result = await _userController.DeleteUser(UserId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteUser_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.DeleteUser(UserId))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _userController.DeleteUser(UserId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_PassesToBusinessLayer()
    {
        _mockUserBusiness.Setup(b => b.DeleteUser(UserId))
                     .ReturnsAsync(true);

        await _userController.DeleteUser(UserId);

        _mockUserBusiness.Verify(
            b => b.DeleteUser(UserId),
            Times.Once);
    }

    [Fact]
    public void DeleteUser_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(UserController.DeleteUser),
            "userId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");

    }

    #endregion

    // =========================================================================
    // ArchiveUser Tests
    // =========================================================================

    #region ArchiveUser Tests

    [Fact]
    public async Task ArchiveUser_Returns200_WhenArchiving()
    {
        _mockUserBusiness.Setup(b => b.ArchiveUser(UserId))
                     .ReturnsAsync(true);

        var result = await _userController.ArchiveUser(
            UserId,
            true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task ArchiveUser_Returns200_WhenUnarchiving()
    {
        _mockUserBusiness.Setup(b => b.UnarchiveUser(UserId))
                     .ReturnsAsync(true);

        var result = await _userController.ArchiveUser(
            UserId,
            false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task ArchiveUser_Returns500_OnUnexpectedException_WhenArchiving()
    {
        _mockUserBusiness.Setup(b => b.ArchiveUser(UserId))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _userController.ArchiveUser(
            UserId,
            true) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task ArchiveUser_Returns500_OnUnexpectedException_WhenUnarchiving()
    {
        _mockUserBusiness.Setup(b => b.UnarchiveUser(UserId))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _userController.ArchiveUser(
            UserId,
            false) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task ArchiveUser_PassesToBusinessLayer_WhenArchiving()
    {
        _mockUserBusiness.Setup(b => b.ArchiveUser(UserId))
                     .ReturnsAsync(true);

        await _userController.ArchiveUser(
            UserId,
            true);

        _mockUserBusiness.Verify(
            b => b.ArchiveUser(UserId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveUser_PassesToBusinessLayer_WhenUnarchiving()
    {
        _mockUserBusiness.Setup(b => b.UnarchiveUser(UserId))
                     .ReturnsAsync(true);

        await _userController.ArchiveUser(
            UserId,
            false);

        _mockUserBusiness.Verify(
            b => b.UnarchiveUser(UserId),
            Times.Once);
    }

    [Fact]
    public void ArchiveUser_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(UserController.ArchiveUser),
            "userId",
            "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");

    }

    #endregion

    // =========================================================================
    // SetSysAdmin Tests
    // =========================================================================

    #region SetSysAdmin Tests

    [Fact]
    public async Task SetSysAdmin_Returns200_OnSuccess()
    {
        var isAdmin = true;

        _mockUserBusiness.Setup(b => b.SetSysAdmin(UserId, UserId, isAdmin))
                     .ReturnsAsync(true);

        var result = (await _userController.SetSysAdmin(
            UserId,
            isAdmin)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task SetSysAdmin_Returns500_OnUnexpectedException()
    {
        var isAdmin = true;

        _mockUserBusiness.Setup(b => b.SetSysAdmin(UserId, UserId, isAdmin))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.SetSysAdmin(
            UserId,
            isAdmin)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SetSysAdmin_PassesToBusinessLayer()
    {
        var isAdmin = true;

        _mockUserBusiness.Setup(b => b.SetSysAdmin(UserId, UserId, isAdmin))
                     .ReturnsAsync(true);

        await _userController.SetSysAdmin(
            UserId,
            isAdmin);

        _mockUserBusiness.Verify(
            b => b.SetSysAdmin(UserId, UserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public void SetSysAdmin_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(UserController.SetSysAdmin),
            "userId",
            "isAdmin");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");

    }

    #endregion

    // =========================================================================
    // GetDataOverview Tests
    // =========================================================================

    #region GetDataOverview Tests

    [Fact]
    public async Task GetDataOverview_Returns200_WithOverview()
    {
        var expected = new DataOverviewDto();

        _mockUserBusiness.Setup(b => b.GetUserOverview(UserId))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetDataOverview(UserId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDataOverview_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetUserOverview(UserId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetDataOverview(UserId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetDataOverview_PassesToBusinessLayer()
    {
        var expected = new DataOverviewDto();

        _mockUserBusiness.Setup(b => b.GetUserOverview(UserId))
                     .ReturnsAsync(expected);

        await _userController.GetDataOverview(UserId);

        _mockUserBusiness.Verify(
            b => b.GetUserOverview(UserId),
            Times.Once);
    }

    [Fact]
    public void GetDataOverview_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetDataOverview),
            "userId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // GetCurrentUser Tests
    // =========================================================================

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_Returns200_WithUser()
    {
        var expected = new UserAdminInfoDto();

        _mockUserBusiness.Setup(b => b.GetUserAdminInfo(UserId, OrgId, ProjectId))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetCurrentUser(
            OrgId,
            ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetCurrentUser_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetUserAdminInfo(UserId, OrgId, ProjectId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetCurrentUser(
            OrgId,
            ProjectId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_PassesToBusinessLayer()
    {
        var expected = new UserAdminInfoDto();

        _mockUserBusiness.Setup(b => b.GetUserAdminInfo(UserId, OrgId, ProjectId))
                     .ReturnsAsync(expected);

        await _userController.GetCurrentUser(
            OrgId,
            ProjectId);

        _mockUserBusiness.Verify(
            b => b.GetUserAdminInfo(UserId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void GetCurrentUser_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetCurrentUser),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // GetActiveUserCounts Tests
    // =========================================================================

    #region GetActiveUserCounts Tests

    [Fact]
    public async Task GetActiveUserCounts_Returns200_WithCounts()
    {
        var expected = new UserActivityCountsDto();

        _mockUserBusiness.Setup(b => b.GetActiveUserCounts(ProjectId, OrgId))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetActiveUserCounts(
            ProjectId,
            OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetActiveUserCounts_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetActiveUserCounts(ProjectId, OrgId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetActiveUserCounts(
            ProjectId,
            OrgId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetActiveUserCounts_PassesToBusinessLayer()
    {
        var expected = new UserActivityCountsDto();

        _mockUserBusiness.Setup(b => b.GetActiveUserCounts(ProjectId, OrgId))
                     .ReturnsAsync(expected);

        await _userController.GetActiveUserCounts(
            ProjectId,
            OrgId);

        _mockUserBusiness.Verify(
            b => b.GetActiveUserCounts(ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void GetActiveUserCounts_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetActiveUserCounts),
            "projectId",
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

    }

    #endregion

    // =========================================================================
    // GetActiveUsers Tests
    // =========================================================================

    #region GetActiveUsers Tests

    [Fact]
    public async Task GetActiveUsers_Returns200_WithUsers()
    {
        var expected = new UserActivityUsersDto();

        _mockUserBusiness.Setup(b => b.GetActiveUsers(ProjectId, OrgId))
                     .ReturnsAsync(expected);

        var result = (await _userController.GetActiveUsers(
            ProjectId,
            OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetActiveUsers_Returns500_OnUnexpectedException()
    {
        _mockUserBusiness.Setup(b => b.GetActiveUsers(ProjectId, OrgId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userController.GetActiveUsers(
            ProjectId,
            OrgId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetActiveUsers_PassesToBusinessLayer()
    {
        var expected = new UserActivityUsersDto();

        _mockUserBusiness.Setup(b => b.GetActiveUsers(ProjectId, OrgId))
                     .ReturnsAsync(expected);

        await _userController.GetActiveUsers(
            ProjectId,
            OrgId);

        _mockUserBusiness.Verify(
            b => b.GetActiveUsers(ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void GetActiveUsers_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserController.GetActiveUsers),
            "projectId",
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");

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

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(UserController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}