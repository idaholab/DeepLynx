using System.Security.Claims;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Middleware;

[Collection("Test Suite Collection")]
public class AuthMiddlewareTests : IntegrationTestBase
{
    private Mock<ILogger<OrgRolePermissionService>> _orgLoggerMock;
    private Mock<IOrgRolePermissionService> _orgRolePermissionServiceMock;
    private Mock<ILogger<ProjectRolePermissionService>> _projectLoggerMock;
    private Mock<IProjectRolePermissionService> _projectRolePermissionServiceMock;
    private Mock<IAdminService> _adminServiceMock;
    private Mock<ILogger<AdminService>> _adminLoggerMock;
    private Mock<IOrganizationService> _organizationServiceMock;

    public long groupId1;
    public long organizationId1;
    public long organizationId2;
    public long permissionId1;
    public long permissionId2;
    public long permissionId3;
    public long projectId1;
    public long projectId2;
    public long roleId1;
    public long roleId2;
    public long roleId3;

    public long userId1;
    public long userId2;
    public long userId3;

    public AuthMiddlewareTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _orgRolePermissionServiceMock = new Mock<IOrgRolePermissionService>();
        _projectRolePermissionServiceMock = new Mock<IProjectRolePermissionService>();
        _adminServiceMock = new Mock<IAdminService>();
        _orgLoggerMock = new Mock<ILogger<OrgRolePermissionService>>();
        _projectLoggerMock = new Mock<ILogger<ProjectRolePermissionService>>();
        _adminLoggerMock = new Mock<ILogger<AdminService>>();
        _organizationServiceMock = new Mock<IOrganizationService>();


        // Reset UserContextStorage before each test
        UserContextStorage.UserId = 0;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsOrgMember = false;
        UserContextStorage.IsProjectAdmin = false;
    }

    // Helper methods

    // Admin flags are pre-populated by UserContextMiddleware in production;
    // tests simulate that by writing directly to UserContextStorage.
    private void SetAuthenticatedUser(HttpContext context, long userId,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false,
        bool isOrgMember = false)
    {
        UserContextStorage.UserId = userId;
        UserContextStorage.IsSysAdmin = isSysAdmin;
        UserContextStorage.IsOrgAdmin = isOrgAdmin;
        UserContextStorage.IsOrgMember = isOrgMember;
        UserContextStorage.IsProjectAdmin = isProjectAdmin;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, $"user{userId}@test.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
    }

    private HttpContext CreateHttpContextWithAuth(string action, string resource)
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthAttribute(action, resource)),
            "Test");
        context.SetEndpoint(endpoint);

        return context;
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create organizations
        var organization1 = new Organization
        {
            Name = $"Test Organization 1 {Guid.NewGuid()}",
            Description = "Test Organization 1 Description"
        };
        Context.Organizations.Add(organization1);

        var organization2 = new Organization
        {
            Name = $"Test Organization 2 {Guid.NewGuid()}",
            Description = "Test Organization 2 Description"
        };
        Context.Organizations.Add(organization2);

        await Context.SaveChangesAsync();
        organizationId1 = organization1.Id;
        organizationId2 = organization2.Id;

        // Create users
        var user1 = new User
        {
            Name = "Test User 1",
            Email = "user1@test.com",
            Username = "user1",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user1);

        // Create users
        var user2 = new User
        {
            Name = "Test User 2",
            Email = "user2@test.com",
            Username = "user2",
            IsActive = true,
            IsArchived = false,
            IsSysAdmin = true
        };
        Context.Users.Add(user2);

        var user3 = new User
        {
            Name = "Test User 3",
            Email = "user3@test.com",
            Username = "user3",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user3);

        await Context.SaveChangesAsync();
        userId1 = user1.Id;
        userId2 = user2.Id;
        userId3 = user3.Id;

        // Create projects
        var project1 = new Project
        {
            Name = "Test Project 1",
            Description = "Test Description 1",
            OrganizationId = organizationId1,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Projects.Add(project1);

        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test Description 2",
            OrganizationId = organizationId1,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Projects.Add(project2);

        await Context.SaveChangesAsync();
        projectId1 = project1.Id;
        projectId2 = project2.Id;

        // Add user3 to organization1
        var orgUser = new OrganizationUser
        {
            UserId = userId3,
            OrganizationId = organizationId1,
            IsOrgAdmin = true
        };
        Context.Set<OrganizationUser>().Add(orgUser);
        await Context.SaveChangesAsync();
    }

    #region Middleware Tests - No Auth Attributes

    [Fact]
    public async Task InvokeAsync_ContinuesPipeline_WhenNoEndpoint()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ContinuesPipeline_WhenNoAuthAttributes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "Test");
        context.SetEndpoint(endpoint);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - Unauthorized

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserNotAuthenticated()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        // Don't set any authentication - UserContextStorage.UserId remains 0

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserIdIsZero()
    {
        // Arrange
        UserContextStorage.UserId = 0;

        var context = CreateHttpContextWithAuth("read", "organization");
        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserIdIsNegative()
    {
        // Arrange
        UserContextStorage.UserId = -1;

        var context = CreateHttpContextWithAuth("read", "organization");
        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Bad Request (Missing IDs)

    [Fact]
    public async Task InvokeAsync_Returns400_WhenBothIdsAreMissing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthAttribute("read", "organization")),
            "Test");
        context.SetEndpoint(endpoint);
        // No route values or query parameters

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenOrgIdIsInvalid()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "not-a-number";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Admins

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserIsSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User is sysadmin
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId2, organizationId1, "delete", "data"))
            .ReturnsAsync(true);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId2, projectId1, "delete", "data"))
            .ReturnsAsync(false);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserHasOrgPermission()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId3);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        // User has org permission as an org admin 
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId3, organizationId1, "delete", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - Organization Only

    [Fact]
    public async Task InvokeAsync_ExtractsOrgIdFromRoute_Successfully()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"),
            Times.Once);
    }


    [Fact]
    public async Task InvokeAsync_Returns403_WhenUserLacksOrgPermission()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("write", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "write", "organization"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Project Only

    [Fact]
    public async Task InvokeAsync_ExtractsProjectIdFromRoute_Successfully()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "project"),
            Times.Once);
    }


    [Fact]
    public async Task InvokeAsync_Returns403_WhenUserLacksProjectPermission()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("write", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "write", "project"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserIsProjectAdmin_EvenWithoutRolePermission()
    {
        // Arrange - project admin lacking the role-based permission should still pass
        var context = CreateHttpContextWithAuth("write", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // Resolve the org for the project so the project-admin bypass can run.
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId1, false))
            .ReturnsAsync(organizationId1);

        // Project-admin status is now computed fresh via AdminService.
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, It.IsAny<long>(), It.Is<List<long>>(l => l.Contains(projectId1))))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "write", "project"))
            .ReturnsAsync(false);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert - bypass granted access without consulting the role permission result
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "write", "project"),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Passes_WhenProjectAdmin_OnOrgLessProjectRoute()
    {
        // Arrange - org-less project route (no {organizationId} segment). The middleware
        // must derive the org from the project and still honor the project-admin bypass.
        var context = CreateHttpContextWithAuth("read", "data_source");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // No organizationId in the route, so AuthMiddleware derives it from the project.
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, null, false))
            .ReturnsAsync(organizationId1);

        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, It.IsAny<long>(), It.Is<List<long>>(l => l.Contains(projectId1))))
            .ReturnsAsync(true);

        // User lacks the role-based permission; the project-admin bypass must still grant access.
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "data_source"))
            .ReturnsAsync(false);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert - bypass works even though the route carries no organizationId
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "data_source"),
            Times.Never);
    }

    #endregion

    #region Middleware Tests - Both Org and Project (OR Logic)

    [Fact]
    public async Task InvokeAsync_FailsWithOrgPermissionAndNotProjectPermission_WhenBothIdsPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User has org permission but NOT project permission
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "data"))
            .ReturnsAsync(true);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "data"))
            .ReturnsAsync(false);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PassesWithProjectPermission_WhenBothIdsPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("write", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User has project permission but NOT org permission
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "write", "data"))
            .ReturnsAsync(false);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "write", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenLacksBothPermissions()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User lacks BOTH org and project permission
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "delete", "data"))
            .ReturnsAsync(false);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "delete", "data"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_PassesWithBothPermissions_WhenBothIdsPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User has BOTH permissions
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "project"))
            .ReturnsAsync(true);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ChecksBothScopes_WithBothIds()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "data"))
            .ReturnsAsync(false);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify project wins over organization permissions
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "data"),
            Times.Once);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "data"),
            Times.Never);
    }

    #endregion

    #region Middleware Tests - Multiple Attributes

    [Fact]
    public async Task InvokeAsync_ChecksAllAttributes_WhenMultiplePresent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(
                new AuthAttribute("read", "organization"),
                new AuthAttribute("write", "data")),
            "Test");
        context.SetEndpoint(endpoint);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "write", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"),
            Times.Once);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "write", "data"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenAnyAttributeCheckFails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(
                new AuthAttribute("read", "organization"),
                new AuthAttribute("delete", "data")), // User doesn't have this
            "Test");
        context.SetEndpoint(endpoint);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "delete", "data"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_StopsAtFirstFailure_WithMultipleAttributes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1);

        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(
                new AuthAttribute("delete", "organization"), // Should fail here
                new AuthAttribute("write", "data")),
            "Test");
        context.SetEndpoint(endpoint);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "delete", "organization"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        // Second permission check should not be called
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "write", "data"),
            Times.Never);
    }

    #endregion

    #region Middleware Tests - Preference Priority

    [Fact]
    public async Task InvokeAsync_PrefersRouteOverQuery_ForOrgId()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?organizationId={organizationId2}");

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Should use route value (organizationId1), not query parameter (organizationId2)
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_PrefersRouteOverQuery_ForProjectId()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.QueryString = new QueryString($"?projectId={projectId2}");

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "project"),
            Times.Once);
    }

    #endregion

    #region Middleware Tests - SysAdmin Check Mock Setup

    [Fact]
    public async Task InvokeAsync_SkipsPermissionChecks_WhenUserIsSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // SysAdminCheck now lives in UserContextMiddleware, so AuthMiddleware should never invoke it
        _adminServiceMock.Verify(x => x.SysAdminCheck(It.IsAny<long>()), Times.Never);
        // Verify permission checks were NOT called
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Middleware Tests - OrganizationService.CheckExistence

    [Fact]
    public async Task InvokeAsync_CallsCheckExistence_WithBothIds()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, organizationId1, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsCheckExistence_WithOnlyOrgId()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(
            x => x.CheckExistence(null, organizationId1, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsCheckExistence_WithOnlyProjectId()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, null, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsCheckExistence_EvenForSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(
            x => x.CheckExistence(null, organizationId1, false),
            Times.Once);
    }

    #endregion

    #region Middleware Tests - Project/Organization Mismatch

    [Fact]
    public async Task InvokeAsync_ThrowsException_WhenProjectDoesNotBelongToOrganization()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "data");
        SetAuthenticatedUser(context, userId1);
        // Project1 belongs to Organization1, but we're checking with Organization2
        context.Request.RouteValues["organizationId"] = organizationId2.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // CheckExistence should throw when project doesn't belong to org
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId2, false))
            .ThrowsAsync(new InvalidOperationException("Project does not belong to the specified organization"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Equal("Project does not belong to the specified organization", exception.Message);

        // Verify CheckExistence was called before permission checks
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, organizationId2, false),
            Times.Once);

        // Verify permission checks were never called due to exception
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CallsCheckExistence_BeforePermissionChecks()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        var callOrder = new List<string>();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId1, false))
            .Callback(() => callOrder.Add("CheckExistence"))
            .ReturnsAsync(organizationId1);

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "data"))
            .Callback(() => callOrder.Add("OrgPermission"))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "data"))
            .Callback(() => callOrder.Add("ProjectPermission"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(2, callOrder.Count); // organization is checked last and if passing in project, org is skipped
        Assert.Equal("CheckExistence", callOrder[0]);
        // Permission checks should come after CheckExistence
        Assert.Contains("ProjectPermission", callOrder);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCallPermissionChecks_WhenCheckExistenceThrows()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("write", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId2.ToString();
        context.Request.RouteValues["projectId"] = projectId2.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId2, organizationId2, false))
            .ThrowsAsync(new KeyNotFoundException("Project not found"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        // Verify permission checks were never attempted
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_VerifiesProjectBelongsToOrg_WhenBothProvided()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId1);
        // Providing both IDs - should verify they match
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // Setup successful existence check
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "delete", "data"))
            .ReturnsAsync(false);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "delete", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify CheckExistence was called with both IDs to validate relationship
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, organizationId1, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_HandlesNonExistentProject_InCheckExistence()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = "99999"; // Non-existent project

        _organizationServiceMock
            .Setup(x => x.CheckExistence(99999, null, false))
            .ThrowsAsync(new KeyNotFoundException("Project with ID 99999 not found"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Contains("99999", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_HandlesNonExistentOrganization_InCheckExistence()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "88888"; // Non-existent organization

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, 88888, false))
            .ThrowsAsync(new KeyNotFoundException("Organization with ID 88888 not found"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Contains("88888", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_AllowsValidProjectOrgCombination()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "data");
        SetAuthenticatedUser(context, userId1);
        // Project1 correctly belongs to Organization1
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // CheckExistence succeeds (no exception thrown)
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "data"))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, organizationId1, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminFailsCheckExistence_ForMismatchedProjectOrg()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "data");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        // Intentionally mismatched: Project1 belongs to Org1, but we're providing Org2
        context.Request.RouteValues["organizationId"] = organizationId2.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // CheckExistence should throw when project doesn't belong to org, even for sysadmin
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId2, false))
            .ThrowsAsync(new InvalidOperationException($"Project {projectId1} does not belong to organization {organizationId2}"));

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Contains($"Project {projectId1} does not belong to organization {organizationId2}", exception.Message);
        Assert.False(nextCalled);

        // Verify CheckExistence was called even for sysadmin
        _organizationServiceMock.Verify(
            x => x.CheckExistence(projectId1, organizationId2, false),
            Times.Once);
    }

    #endregion

    #region Middleware Tests - Negative Verification (Methods NOT Called)

    [Fact]
    public async Task InvokeAsync_DoesNotCallOrgPermission_WhenOnlyProjectIdPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCallProjectPermission_WhenOnlyOrgIdPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Middleware Tests - Exception Handling

    [Fact]
    public async Task InvokeAsync_ThrowsException_WhenCheckExistenceFails()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(It.IsAny<long?>(), It.IsAny<long?>(), false))
            .ThrowsAsync(new Exception("Organization not found"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));
    }

    [Fact]
    public async Task InvokeAsync_ThrowsException_WhenOrgPermissionCheckFails()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "organization"))
            .ThrowsAsync(new Exception("Permission check failed"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));
    }

    [Fact]
    public async Task InvokeAsync_ThrowsException_WhenProjectPermissionCheckFails()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "project"))
            .ThrowsAsync(new Exception("Permission check failed"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));
    }

    #endregion

    #region Middleware Tests - Empty/Null String Route Values

    [Fact]
    public async Task InvokeAsync_Returns400_WhenOrgIdIsEmptyString()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenProjectIdIsEmptyString()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = "";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenOrgIdIsWhitespace()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "   ";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenProjectIdIsWhitespace()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = "   ";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenQueryOrgIdIsEmptyString()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.QueryString = new QueryString("?organizationId=");

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenQueryProjectIdIsEmptyString()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.QueryString = new QueryString("?projectId=");

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenOrgIdIsNonNumeric()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "abc123";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenProjectIdIsNonNumeric()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = "xyz789";

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Zero/Negative ID Values

    [Fact]
    public async Task InvokeAsync_HandlesZeroOrgId_AsValidButChecksFail()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = "0";

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, 0, "read", "organization"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        // Should attempt to check permissions with 0, which will fail
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HandlesNegativeProjectId_AsValidButChecksFail()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = "-1";

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, -1, "read", "project"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - List of project Id Values

    [Fact]
    public async Task InvokeAsync_WithSingleProjectIdInQuery_ChecksPermissionForThatProject()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", projectId1.ToString() }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence(It.IsAny<int?>(), It.IsAny<int?>(), false))
            .ReturnsAsync(organizationId1);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "class"),
            Times.Once);
        // Organization permission should not be checked when project IDs are present
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleProjectIdsCommaSeparated_ChecksPermissionForAllProjects()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},{projectId2}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
            { "projectIds", $"{projectId1},{projectId2}" }
            });

        // Set up specific mocks for each project ID with explicit casts
        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId1, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId2, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId1, "read", "class"))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId2, "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled, $"Next was not called. Response status: {context.Response.StatusCode}");
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (int)projectId1, "read", "class"),
            Times.Once);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (int)projectId2, "read", "class"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleProjectIds_FailsIfMissingPermissionInOneProject()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("write", "data_source");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},{projectId2}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},{projectId2}" }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence(It.IsAny<int?>(), It.IsAny<int?>(), false))
            .ReturnsAsync(organizationId1);

        // User has permission in project1 but NOT in project2
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "write", "data_source"))
            .ReturnsAsync(true);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId2, "write", "data_source"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithNullableProjectIds_ChecksOnlyOrganizationPermission()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        // No projectIds in query

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "class"),
            Times.Once);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyProjectIdsArray_ChecksOnlyOrganizationPermission()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "relationship");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString("?projectIds=");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", "" }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "read", "relationship"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "read", "relationship"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithProjectIds_SysAdmin_SkipsPermissionChecks()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("delete", "record");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},{projectId2}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},{projectId2}" }
            });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        // CheckExistence should not be called for sysadmin
        _organizationServiceMock.Verify(
            x => x.CheckExistence(It.IsAny<int?>(), It.IsAny<int?>(), false),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithProjectIds_ChecksExistenceForAllProjects()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "record");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},{projectId2}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},{projectId2}" }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence(It.IsAny<long?>(), It.IsAny<long?>(), false))
            .ReturnsAsync(organizationId1);

        // Change It.IsAny<int> to It.IsAny<long> to match what middleware passes
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled, $"Next was not called. Response status: {context.Response.StatusCode}");
        _organizationServiceMock.Verify(
            x => x.CheckExistence((long)projectId1, (long)organizationId1, false),
            Times.Once);
        _organizationServiceMock.Verify(
            x => x.CheckExistence((long)projectId2, (long)organizationId1, false),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleProjectIdsRepeatedFormat_ParsesCorrectly()
    {
        // Arrange - Testing ?projectIds=1&projectIds=2 format
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        // Simulate multiple query parameters with same key
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", new Microsoft.Extensions.Primitives.StringValues(new[] { projectId1.ToString(), projectId2.ToString() }) }
            });

        // Set up specific mocks for each project ID
        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId1, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId2, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId1, "read", "class"))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId2, "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled, $"Next was not called. Response status: {context.Response.StatusCode}");
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (int)projectId1, "read", "class"),
            Times.Once);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (int)projectId2, "read", "class"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithProjectIds_ThrowsException_WhenProjectDoesNotBelongToOrganization()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "record");
        SetAuthenticatedUser(context, userId1);
        // Project1 belongs to Organization1, but we're checking with Organization2
        context.Request.RouteValues["organizationId"] = organizationId2.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", projectId1.ToString() }
            });

        // CheckExistence should throw when project doesn't belong to org
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId2, false))
            .ThrowsAsync(new InvalidOperationException("Project does not belong to the specified organization"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Equal("Project does not belong to the specified organization", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_WithDuplicateProjectIds_ChecksPermissionOnlyOnce()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        // Duplicate project ID in comma-separated list
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},{projectId1}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},{projectId1}" }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence(It.IsAny<int?>(), It.IsAny<int?>(), false))
            .ReturnsAsync(organizationId1);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Should only check once due to duplicate removal in middleware logic
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "read", "class"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidProjectIdInArray_IgnoresInvalidId()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.QueryString = new QueryString($"?projectIds={projectId1},invalid,{projectId2}");
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},invalid,{projectId2}" }
            });

        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId1, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _organizationServiceMock
            .Setup(x => x.CheckExistence((long)projectId2, (long)organizationId1, false))
            .ReturnsAsync(organizationId1);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId1, "read", "class"))
            .ReturnsAsync(true);

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, (int)projectId2, "read", "class"))
            .ReturnsAsync(true);

        var responseStatusCode = 0;
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Debug: Check the response status code
        responseStatusCode = context.Response.StatusCode;

        // Assert
        Assert.True(nextCalled, $"Next was not called. Response status: {responseStatusCode}");
    }

    [Fact]
    public async Task InvokeAsync_WithProjectIdInRouteAndProjectIdsInQuery_PrioritizesQueryArray()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("read", "class");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString(); // Single project in route
        context.Request.QueryString = new QueryString($"?projectIds={projectId2}"); // Array in query
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", projectId2.ToString() }
            });

        // Set up mock for EACH specific project ID
        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId1, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _organizationServiceMock
            .Setup(x => x.CheckExistence(projectId2, organizationId1, false))
            .ReturnsAsync(organizationId1);

        // Change It.IsAny<int> to It.IsAny<long> to match the actual parameter type
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, It.IsAny<long>(), "read", "class"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled, $"Next was not called. Response status: {context.Response.StatusCode}");
        // Should check BOTH project1 (from route) AND project2 (from query)
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (long)projectId1, "read", "class"),
            Times.Once);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, (long)projectId2, "read", "class"),
            Times.Once);
    }
    #endregion

    #region Middleware Tests - Update Action

    [Fact]
    public async Task InvokeAsync_PassesWithUpdatePermission_OnOrganization()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("update", "organization");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "update", "organization"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(userId1, organizationId1, "update", "organization"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenUserLacksUpdatePermissionOnProject()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("update", "project");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "update", "project"))
            .ReturnsAsync(false);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_PassesWithUpdatePermission_OnDataResource()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("update", "data");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "update", "data"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "update", "data"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminBypassesUpdatePermissionChecks()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("update", "data");
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify update permission checks were NOT called
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), "update", It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), "update", It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_PassesWithUpdatePermission_WhenBothIdsPresent()
    {
        // Arrange
        var context = CreateHttpContextWithAuth("update", "record");
        SetAuthenticatedUser(context, userId1);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        // User has project update permission but NOT org permission
        _orgRolePermissionServiceMock
            .Setup(x => x.PermissionInOrg(userId1, organizationId1, "update", "record"))
            .ReturnsAsync(false);
        _projectRolePermissionServiceMock
            .Setup(x => x.PermissionInProject(userId1, projectId1, "update", "record"))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(userId1, projectId1, "update", "record"),
            Times.Once);
    }

    #endregion

    #region Middleware Tests - SysAdmin Attribute

    private HttpContext CreateHttpContextWithSysAdmin()
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new SysAdminAttribute()),
            "Test");
        context.SetEndpoint(endpoint);

        return context;
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_PassesForSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // SysAdminCheck now lives in UserContextMiddleware; AuthMiddleware reads UserContextStorage.IsSysAdmin
        _adminServiceMock.Verify(x => x.SysAdminCheck(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_Returns403ForNonSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        SetAuthenticatedUser(context, userId1); // non-sysadmin

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_Returns401ForUnauthenticatedUser()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        // Don't set authentication - UserContextStorage.UserId remains 0

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_DoesNotRequireOrganizationId()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        // No organizationId or projectId in route

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_SkipsPermissionChecks()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify no permission checks were called
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_SysAdminAttribute_WithOrgAdmin_StillRequiresSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSysAdmin();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - OrgAdmin Attribute

    private HttpContext CreateHttpContextWithOrgAdmin(bool includeArchived = false)
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new OrgAdminAttribute(includeArchived)),
            "Test");
        context.SetEndpoint(endpoint);

        return context;
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_PassesForOrgAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // OrgAdminCheck now happens in UserContextMiddleware; AuthMiddleware reads UserContextStorage.IsOrgAdmin
        _adminServiceMock.Verify(x => x.OrgAdminCheck(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_PassesForSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // OrgAdminCheck now lives in UserContextMiddleware; AuthMiddleware must not invoke it
        _adminServiceMock.Verify(x => x.OrgAdminCheck(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_Returns403ForNonOrgAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId1); // regular user
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_Returns401ForUnauthenticatedUser()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        // Don't set authentication - UserContextStorage.UserId remains 0

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_Returns400WhenOrganizationIdMissing()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3);
        // No organizationId in route — UserContextMiddleware leaves IsOrgAdmin = false

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_CallsCheckExistence()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(x => x.CheckExistence(null, organizationId1, false), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_WithIncludeArchived_PassesParameterToCheckExistence()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin(includeArchived: true);
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, true))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _organizationServiceMock.Verify(x => x.CheckExistence(null, organizationId1, true), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_ThrowsException_WhenOrganizationNotFound()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3);
        context.Request.RouteValues["organizationId"] = "99999";

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, 99999, false))
            .ThrowsAsync(new KeyNotFoundException("Organization with ID 99999 not found"));

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
                _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object));

        Assert.Contains("99999", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_SkipsRegularPermissionChecks()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify regular permission checks were NOT called
        _orgRolePermissionServiceMock.Verify(
            x => x.PermissionInOrg(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _projectRolePermissionServiceMock.Verify(
            x => x.PermissionInProject(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_SetsOrganizationIdInContext()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            // Verify the context was set
            Assert.Equal(organizationId1, UserContextStorage.OrganizationId);
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_OrgAdminAttribute_OrgAdminForDifferentOrg_Returns403()
    {
        // Arrange
        var context = CreateHttpContextWithOrgAdmin();
        // userId3 is org admin for org1; UserContextMiddleware checks against the route's
        // organizationId (here org2), so IsOrgAdmin stays false for this request.
        SetAuthenticatedUser(context, userId3);
        context.Request.RouteValues["organizationId"] = organizationId2.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId2, false))
            .ReturnsAsync(organizationId2);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - OrgMember Attribute

    private HttpContext CreateHttpContextWithOrgMember(bool includeArchived = false)
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new OrgMemberAttribute(includeArchived)),
            "Test");
        context.SetEndpoint(endpoint);

        return context;
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_PassesForOrgMember()
    {
        // Arrange
        var context = CreateHttpContextWithOrgMember();
        SetAuthenticatedUser(context, userId1, isOrgMember: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Membership is pre-populated by UserContextMiddleware; AuthMiddleware must not re-check it
        _adminServiceMock.Verify(x => x.OrgMemberCheck(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_PassesForOrgAdmin()
    {
        // Arrange: an org admin is implicitly a member
        var context = CreateHttpContextWithOrgMember();
        SetAuthenticatedUser(context, userId3, isOrgAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_PassesForSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithOrgMember();
        SetAuthenticatedUser(context, userId2, isSysAdmin: true);
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_Returns403ForNonMember()
    {
        // Arrange
        var context = CreateHttpContextWithOrgMember();
        SetAuthenticatedUser(context, userId1); // not a member, not an admin
        context.Request.RouteValues["organizationId"] = organizationId1.ToString();

        _organizationServiceMock
            .Setup(x => x.CheckExistence(null, organizationId1, false))
            .ReturnsAsync(organizationId1);

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_Returns401ForUnauthenticatedUser()
    {
        // Arrange
        var context = CreateHttpContextWithOrgMember();
        // Don't set authentication - UserContextStorage.UserId remains 0

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OrgMemberAttribute_Returns400WhenOrganizationIdMissing()
    {
        // Arrange
        var context = CreateHttpContextWithOrgMember();
        SetAuthenticatedUser(context, userId1, isOrgMember: true);
        // No organizationId route value

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new AuthMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _orgRolePermissionServiceMock.Object,
            _projectRolePermissionServiceMock.Object, _adminServiceMock.Object, _organizationServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    #endregion
}