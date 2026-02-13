using System.Security.Claims;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Middleware;

[Collection("Test Suite Collection")]
public class SensitivityMiddlewareTests : IntegrationTestBase
{
    private Mock<ISensitivityLabelService> _sensitivityLabelServiceMock;
    private Mock<IAdminService> _adminServiceMock;

    public long organizationId1;
    public long organizationId2;
    public long projectId1;
    public long projectId2;
    
    public long userId1; // Full permissions for both labels
    public long userId2; // Permissions for label1 only
    public long userId3; // Permissions for label2 only
    public long userId4; // No permissions
    public long userId5; // SysAdmin
    public long userId6; // OrgAdmin
    public long userId7; // ProjectAdmin
    
    public long labelId1;
    public long labelId2;
    
    public long recordId1; // Has labelId1
    public long recordId2; // Has labelId2
    public long recordId3; // Has both labels
    public long recordId4; // No labels

    public SensitivityMiddlewareTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _sensitivityLabelServiceMock = new Mock<ISensitivityLabelService>();
        _adminServiceMock = new Mock<IAdminService>();

        // Reset UserContextStorage before each test
        UserContextStorage.UserId = 0;
        UserContextStorage.OrganizationId = 0;
    }

    private void SetAuthenticatedUser(HttpContext context, long userId, long organizationId)
    {
        UserContextStorage.UserId = userId;
        UserContextStorage.OrganizationId = organizationId;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, $"user{userId}@test.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
    }

    private HttpContext CreateHttpContextWithSensitivity(string action)
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            ctx => Task.CompletedTask,
            new EndpointMetadataCollection(new SensitivityAttribute(action)),
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
            Name = "Test User 1 - Full Permissions",
            Email = "user1@test.com",
            Username = "user1",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user1);
        
        var user2 = new User
        {
            Name = "Test User 2 - Label1 Only",
            Email = "user2@test.com",
            Username = "user2",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user2);
        
        var user3 = new User
        {
            Name = "Test User 3 - Label2 Only",
            Email = "user3@test.com",
            Username = "user3",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user3);
        
        var user4 = new User
        {
            Name = "Test User 4 - No Permissions",
            Email = "user4@test.com",
            Username = "user4",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user4);
        
        var user5 = new User
        {
            Name = "Test User 5 - SysAdmin",
            Email = "user5@test.com",
            Username = "user5",
            IsActive = true,
            IsArchived = false,
            IsSysAdmin = true
        };
        Context.Users.Add(user5);
        
        var user6 = new User
        {
            Name = "Test User 6 - OrgAdmin",
            Email = "user6@test.com",
            Username = "user6",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user6);
        
        var user7 = new User
        {
            Name = "Test User 7 - ProjectAdmin",
            Email = "user7@test.com",
            Username = "user7",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user7);

        await Context.SaveChangesAsync();
        userId1 = user1.Id;
        userId2 = user2.Id;
        userId3 = user3.Id;
        userId4 = user4.Id;
        userId5 = user5.Id;
        userId6 = user6.Id;
        userId7 = user7.Id;

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
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RequireSensitivityLabel = true
        };
        Context.Projects.Add(project2);

        await Context.SaveChangesAsync();
        projectId1 = project1.Id;
        projectId2 = project2.Id;
        
        // Add user6 as org admin
        var orgUser = new OrganizationUser
        {
            UserId = userId6,
            OrganizationId = organizationId1, 
            IsOrgAdmin = true
        };
        Context.Set<OrganizationUser>().Add(orgUser);
        await Context.SaveChangesAsync();
        
        // Setup label IDs (these would be created by sensitivity label service)
        labelId1 = 1;
        labelId2 = 2;
        
        // Setup record IDs (these would be created elsewhere)
        recordId1 = 1;
        recordId2 = 2;
        recordId3 = 3;
        recordId4 = 4;
    }

    #region Middleware Tests - No Sensitivity Attributes

    [Fact]
    public async Task InvokeAsync_ContinuesPipeline_WhenNoEndpoint()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1, organizationId1);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ContinuesPipeline_WhenNoSensitivityAttributes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, userId1, organizationId1);

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

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - Unauthorized

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserNotAuthenticated()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        // Don't set any authentication - UserContextStorage.UserId remains 0

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserIdIsZero()
    {
        // Arrange
        UserContextStorage.UserId = 0;

        var context = CreateHttpContextWithSensitivity("write record");
        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenUserIdIsNegative()
    {
        // Arrange
        UserContextStorage.UserId = -1;

        var context = CreateHttpContextWithSensitivity("delete record");
        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Admin Bypass

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserIsSysAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId5, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId5))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        // Verify no permission checks were called
        _sensitivityLabelServiceMock.Verify(
            x => x.GetAuthorizedSensitivityLabels(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserIsOrgAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId6, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId6))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId6, organizationId1))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Passes_WhenUserIsProjectAdmin()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("delete record");
        SetAuthenticatedUser(context, userId7, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId7))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId7, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId7, organizationId1, new List<long> { projectId1 }))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - CREATE Actions (write record, upload file)

    [Fact]
    public async Task InvokeAsync_WriteRecord_Passes_WhenLabelNotRequiredAndNoneProvided()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WriteRecord_Returns403_WhenLabelRequiredAndNoneProvided()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(true);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WriteRecord_Passes_WhenUserHasPermissionForProvidedLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId1.ToString() }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WriteRecord_Returns403_WhenUserLacksPermissionForProvidedLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId2.ToString() } // User doesn't have label2
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UploadFile_Passes_WithMultipleAuthorizedLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("upload file");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelIds", $"{labelId1},{labelId2}" }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "upload file"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UploadFile_Returns403_WhenUserLacksOneOfMultipleLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("upload file");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelIds", $"{labelId1},{labelId2}" } // User only has label1
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "upload file"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - READ/DELETE Actions (read record, delete record, download file, delete file)

    [Fact]
    public async Task InvokeAsync_ReadRecord_Passes_WhenUserHasPermissionForRecordLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "read record"))
            .ReturnsAsync(new List<long> { labelId1 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ReadRecord_Returns403_WhenUserLacksPermissionForRecordLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId2.ToString(); // Has labelId2

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId2))
            .ReturnsAsync(new List<long> { labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "read record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ReadRecord_Passes_WhenRecordHasNoLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId4, organizationId1); // User with no permissions
        context.Request.RouteValues["recordId"] = recordId4.ToString(); // Record with no labels

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId4))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId4, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId4, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId4))
            .ReturnsAsync(new List<long>()); // No labels
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId4, organizationId1, It.IsAny<long[]>(), "read record"))
            .ReturnsAsync(new List<long>()); // No authorized labels

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ReadRecord_Returns403_WhenRecordHasMultipleLabelsAndUserLacksOne()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId3.ToString(); // Has both labels

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId3))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "read record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DeleteRecord_Passes_WhenUserHasAllRecordLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("delete record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["recordId"] = recordId3.ToString(); // Has both labels

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId3))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "delete record"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DownloadFile_Returns403_WhenUserHasNoPermissions()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("download file");
        SetAuthenticatedUser(context, userId4, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId4))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId4, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId4, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId4, organizationId1, It.IsAny<long[]>(), "download file"))
            .ReturnsAsync(new List<long>());

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DeleteFile_Passes_WhenUserHasPermission()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("delete file");
        SetAuthenticatedUser(context, userId3, organizationId1);
        context.Request.RouteValues["recordId"] = recordId2.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId3))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId3, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId3, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId2))
            .ReturnsAsync(new List<long> { labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId3, organizationId1, It.IsAny<long[]>(), "delete file"))
            .ReturnsAsync(new List<long> { labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - UPDATE Actions (update record, update file)

    [Fact]
    public async Task InvokeAsync_UpdateRecord_Passes_WhenUserHasPermissionForExistingLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "update record"))
            .ReturnsAsync(new List<long> { labelId1 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UpdateRecord_Returns403_WhenUserLacksPermissionForExistingLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId2.ToString(); // Has labelId2

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId2))
            .ReturnsAsync(new List<long> { labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "update record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UpdateRecord_Passes_WhenUserHasPermissionForBothExistingAndNewLabels()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString(); // Has labelId1
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId2.ToString() } // Adding labelId2
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "update record"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UpdateRecord_Returns403_WhenUserHasExistingButNotNewLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update record");
        SetAuthenticatedUser(context, userId2, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString(); // Has labelId1
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId2.ToString() } // Trying to add labelId2
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId2))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId2, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId2, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId2, organizationId1, It.IsAny<long[]>(), "update record"))
            .ReturnsAsync(new List<long> { labelId1 }); // User only has label1

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UpdateFile_Passes_WhenNoNewLabelsProvided()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update file");
        SetAuthenticatedUser(context, userId3, organizationId1);
        context.Request.RouteValues["recordId"] = recordId2.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId3))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId3, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId3, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId2))
            .ReturnsAsync(new List<long> { labelId2 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId3, organizationId1, It.IsAny<long[]>(), "update file"))
            .ReturnsAsync(new List<long> { labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UpdateFile_Returns403_WhenUserHasNewButNotExistingLabel()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("update file");
        SetAuthenticatedUser(context, userId3, organizationId1);
        context.Request.RouteValues["recordId"] = recordId1.ToString(); // Has labelId1
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId2.ToString() }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId3))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId3, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId3, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId3, organizationId1, It.IsAny<long[]>(), "update file"))
            .ReturnsAsync(new List<long> { labelId2 }); // User only has label2

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    #endregion

    #region Middleware Tests - Project ID Parsing

    [Fact]
    public async Task InvokeAsync_ParsesProjectIdFromRoute()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.Is<List<long>>(list => list.Contains(projectId1))))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(
                userId1, 
                organizationId1, 
                It.Is<long[]>(arr => arr.Contains(projectId1)),
                "write record"))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
        _sensitivityLabelServiceMock.Verify(
            x => x.GetAuthorizedSensitivityLabels(
                userId1, 
                organizationId1, 
                It.Is<long[]>(arr => arr.Contains(projectId1)),
                "write record"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ParsesMultipleProjectIdsFromQuery()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("read record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "projectIds", $"{projectId1},{projectId2}" }
            });
        context.Request.RouteValues["recordId"] = recordId1.ToString();

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(
                userId1, 
                organizationId1, 
                It.Is<List<long>>(list => list.Contains(projectId1) && list.Contains(projectId2))))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetRecordSensitivityLabels(recordId1))
            .ReturnsAsync(new List<long> { labelId1 });
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(
                userId1, 
                organizationId1, 
                It.Is<long[]>(arr => arr.Contains(projectId1) && arr.Contains(projectId2)),
                "read record"))
            .ReturnsAsync(new List<long> { labelId1 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion

    #region Middleware Tests - Label ID Parsing

    [Fact]
    public async Task InvokeAsync_ParsesSingleLabelIdFromQuery()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelId", labelId1.ToString() }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ParsesMultipleLabelIdsFromQuery()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("upload file");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelIds", $"{labelId1},{labelId2}" }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "upload file"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresInvalidLabelIds()
    {
        // Arrange
        var context = CreateHttpContextWithSensitivity("write record");
        SetAuthenticatedUser(context, userId1, organizationId1);
        context.Request.RouteValues["projectId"] = projectId1.ToString();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "sensitivityLabelIds", $"{labelId1},invalid,{labelId2}" }
            });

        _adminServiceMock
            .Setup(x => x.SysAdminCheck(userId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.OrgAdminCheck(userId1, organizationId1))
            .ReturnsAsync(false);
        _adminServiceMock
            .Setup(x => x.ProjectAdminCheck(userId1, organizationId1, It.IsAny<List<long>>()))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.IsSensitivityLabelRequired(organizationId1, projectId1))
            .ReturnsAsync(false);
        
        _sensitivityLabelServiceMock
            .Setup(x => x.GetAuthorizedSensitivityLabels(userId1, organizationId1, It.IsAny<long[]>(), "write record"))
            .ReturnsAsync(new List<long> { labelId1, labelId2 });

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SensitivityMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _sensitivityLabelServiceMock.Object, _adminServiceMock.Object);

        // Assert
        Assert.True(nextCalled);
    }

    #endregion
    
    #region Sensitivity Label Service Method Tests

    [Fact]
    public async Task SensitivityLabelService_GetAuthorizedLabels_ReturnsCorrectLabels()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.GetAuthorizedSensitivityLabels(
            userId1,
            organizationId1,
            new[] { projectId1 },
            "write record");
    
        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SensitivityLabelService_IsSensitivityLabelRequired_ChecksOrgLevel()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.IsSensitivityLabelRequired(organizationId1, null);
    
        // Assert
        Assert.False(result); // org1 doesn't require labels
    }

    [Fact]
    public async Task SensitivityLabelService_IsSensitivityLabelRequired_ChecksProjectLevel()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.IsSensitivityLabelRequired(organizationId1, projectId2);
    
        // Assert
        Assert.True(result); // project2 requires labels
    }

    [Fact]
    public async Task SensitivityLabelService_GetRecordSensitivityLabels_ReturnsLabels()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.GetRecordSensitivityLabels(recordId1);
    
        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SensitivityLabelService_GetAuthorizedLabels_ThrowsOnInvalidAction()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetAuthorizedSensitivityLabels(
                userId1,
                organizationId1,
                new[] { projectId1 },
                "invalid action"));
    }
    
    [Fact]
    public async Task SensitivityLabelService_GetAuthorizedLabels_ReturnsEmptyForNullProjects()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.GetAuthorizedSensitivityLabels(
            userId1,
            organizationId1,
            null,
            "write record");
    
        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SensitivityLabelService_GetAuthorizedLabels_ReturnsEmptyForEmptyProjectArray()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.GetAuthorizedSensitivityLabels(
            userId1,
            organizationId1,
            new long[] { },
            "write record");
    
        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SensitivityLabelService_GetAuthorizedLabels_WorksWithMultipleProjects()
    {
        // Arrange
        var service = new SensitivityLabelService(Context);
    
        // Act
        var result = await service.GetAuthorizedSensitivityLabels(
            userId1,
            organizationId1,
            new[] { projectId1, projectId2 },
            "write record");
    
        // Assert
        Assert.NotNull(result);
    }

    #endregion
}