using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class InvitationBusinessTests : IntegrationTestBase
{
    private InvitationBusiness _invitationBusiness = null!;
    private ClassBusiness _classBusiness = null!;
    private Mock<IDataSourceBusiness> _dataSourceBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<OrganizationBusiness>> _mockOrgLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<INotificationBusiness> _notificationBusiness = null!;
    private Mock<IObjectStorageBusiness> _objectStorageBusiness = null!;
    private UserBusiness _userBusiness = null!;
    private OrganizationBusiness _organizationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    private Mock<IRecordBusiness> _recordBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private Mock<IRoleBusiness> _roleBusiness = null!;
    private BulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;

    public long oid; // organization ID
    public long oid2; // organization 2 ID
    public long pid; // project ID
    public long pid2; // project 2 ID
    public long uid; // existing user ID
    public long uid2; // existing user 2 ID
    public long rid; // role ID
    public long gid; // group ID

    public InvitationBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _recordBusiness = new Mock<IRecordBusiness>();
        _relationshipBusiness = new Mock<IRelationshipBusiness>();
        _dataSourceBusiness = new Mock<IDataSourceBusiness>();
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _userBusiness = new UserBusiness(Context);
        _notificationBusiness = new Mock<INotificationBusiness>();
        _mockOrgLogger = new Mock<ILogger<OrganizationBusiness>>();
        _bulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness.Object, _bulkCopyUpsertExecutor);
        _objectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _roleBusiness = new Mock<IRoleBusiness>();
        _organizationBusiness = new OrganizationBusiness(
            Context, _eventBusiness, _roleBusiness.Object, _mockOrgLogger.Object);

        _classBusiness = new ClassBusiness(
            Context, _recordBusiness.Object,
            _relationshipBusiness.Object, _eventBusiness);

        _projectBusiness = new ProjectBusiness(
            Context, _mockLogger.Object,
            _classBusiness, _roleBusiness.Object, _dataSourceBusiness.Object,
            _objectStorageBusiness.Object, _eventBusiness, _organizationBusiness);

        _invitationBusiness = new InvitationBusiness(
            Context,
            _notificationBusiness.Object,
            _projectBusiness,
            _organizationBusiness,
            _userBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create users
        var user1 = new User
        {
            Name = "Existing User",
            Email = "existing.user@test.com",
            Password = "test_password",
            IsArchived = false
        };
        var user2 = new User
        {
            Name = "Existing User 2",
            Email = "existing.user2@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.AddRange(user1, user2);
        await Context.SaveChangesAsync();
        uid = user1.Id;
        uid2 = user2.Id;

        // Create organizations
        var org1 = new Organization
        {
            Name = "Test Org 1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var org2 = new Organization
        {
            Name = "Test Org 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.AddRange(org1, org2);
        await Context.SaveChangesAsync();
        oid = org1.Id;
        oid2 = org2.Id;

        // Create projects
        var project1 = new Project
        {
            Name = "Project 1",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var project2 = new Project
        {
            Name = "Project 2",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Projects.AddRange(project1, project2);
        await Context.SaveChangesAsync();
        pid = project1.Id;
        pid2 = project2.Id;

        // Create role
        var role = new Role
        {
            Name = "Test Role",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Roles.Add(role);
        await Context.SaveChangesAsync();
        rid = role.Id;

        // Create group
        var group = new Group
        {
            Name = "Test Group",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();
        gid = group.Id;

        // Add user1 to org1
        var orgUser = new OrganizationUser
        {
            OrganizationId = oid,
            UserId = uid
        };
        Context.OrganizationUsers.Add(orgUser);
        await Context.SaveChangesAsync();
    }

    #region InviteAndAddUserToHierarchy - Existing User Tests

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserExistsAndNotInOrg()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, userEmail, null);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserExistsAndAlreadyInOrg()
    {
        // Arrange
        var userEmail = "existing.user@test.com";

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, userEmail, null);

        // Assert
        Assert.True(result);
        var orgUserCount = await Context.OrganizationUsers
            .CountAsync(ou => ou.UserId == uid && ou.OrganizationId == oid);
        Assert.Equal(1, orgUserCount); // Should still only have one entry
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserExistsAndNotInProject()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, rid, userEmail, null);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == uid2 && pm.ProjectId == pid));
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserExistsAndAlreadyInProject()
    {
        // Arrange
        var userEmail = "existing.user@test.com";
        
        // Add user to project first
        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid,
            RoleId = rid
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, rid, userEmail, null);

        // Assert
        Assert.True(result);
        var projectMemberCount = await Context.ProjectMembers
            .CountAsync(pm => pm.UserId == uid && pm.ProjectId == pid);
        Assert.Equal(1, projectMemberCount); // Should still only have one entry
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserInGroupAlreadyInProject()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
        Assert.NotNull(user);
        
        var group = await Context.Groups.FirstOrDefaultAsync(g => g.Id == gid);
        Assert.NotNull(group);
        
        group.Users.Add(user);

        // Add group to project
        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            GroupId = gid,
            RoleId = rid
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, rid, userEmail, null);

        // Assert
        Assert.True(result);
        var directProjectMemberCount = await Context.ProjectMembers
            .CountAsync(pm => pm.UserId == uid2 && pm.ProjectId == pid);
        Assert.Equal(0, directProjectMemberCount); // Should not create duplicate membership
    }

    #endregion

    #region InviteAndAddUserToHierarchy - New User Tests

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserDoesNotExist()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        var userName = "New User";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, userName))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, newUserEmail, userName);

        // Assert
        Assert.True(result);
        
        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);
        Assert.Equal(newUserEmail, newUser.Name); // Name should be set to email
        Assert.Equal(newUserEmail, newUser.Email);
        
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == newUser.Id && ou.OrganizationId == oid));
        
        _notificationBusiness.Verify(n => n.SendEmail(newUserEmail, userName), Times.Once);
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WhenUserDoesNotExistAndAddedToProject()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, null))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, rid, newUserEmail, null);

        // Assert
        Assert.True(result);
        
        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);
        
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == newUser.Id && ou.OrganizationId == oid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == newUser.Id && pm.ProjectId == pid && pm.RoleId == rid));
        
        _notificationBusiness.Verify(n => n.SendEmail(newUserEmail, null), Times.Once);
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Fails_WhenEmailSendFails()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, null))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _invitationBusiness.InviteAndAddUserToHierarchy(oid, null, null, newUserEmail, null));
        
        Assert.Contains("Email not sent", exception.Message);
        Assert.Contains(newUserEmail, exception.Message);
        
        // Verify user was not created
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.Null(user);
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WithNullProjectId()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, null))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, newUserEmail, null);

        // Assert
        Assert.True(result);
        
        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == newUser.Id && ou.OrganizationId == oid));
        
        // Should not be in any project
        Assert.False(await Context.ProjectMembers.AnyAsync(pm => pm.UserId == newUser.Id));
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WithNullRoleId()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, null))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, null, newUserEmail, null);

        // Assert
        Assert.True(result);
        
        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);
        
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.UserId == newUser.Id && pm.ProjectId == pid);
        Assert.NotNull(projectMember);
        Assert.Null(projectMember.RoleId);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WithMultipleOrganizations()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";

        // Act - Add to oid
        var result1 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, userEmail, null);
        
        // Act - Add to oid2
        var result2 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, userEmail, null);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
    }

    [Fact]
    public async Task InviteAndAddUserToHierarchy_Success_WithMultipleProjects()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";

        // Act - Add to pid
        var result1 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, rid, userEmail, null);
        
        // Act - Add to pid2
        var result2 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid2, rid, userEmail, null);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == uid2 && pm.ProjectId == pid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == uid2 && pm.ProjectId == pid2));
    }

    #endregion
}