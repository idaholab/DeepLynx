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
    private BulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;
    private ClassBusiness _classBusiness = null!;
    private Mock<IDataSourceBusiness> _dataSourceBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private InvitationBusiness _invitationBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<ILogger<OrganizationBusiness>> _mockOrgLogger = null!;
    private Mock<INotificationBusiness> _notificationBusiness = null!;
    private Mock<IObjectStorageBusiness> _objectStorageBusiness = null!;
    private OrganizationBusiness _organizationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    private Mock<IRecordBusiness> _recordBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private Mock<IRoleBusiness> _roleBusiness = null!;
    private UserBusiness _userBusiness = null!;
    public long gid; // group ID

    public long oid; // organization ID
    public long oid2; // organization 2 ID
    public long pid; // project ID
    public long pid2; // project 2 ID
    public long rid; // role ID
    public long uid; // existing user ID
    public long uid2; // existing user 2 ID

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
            Context, _eventBusiness, _roleBusiness.Object, _mockOrgLogger.Object, _objectStorageBusiness.Object);

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

    #region Existing User by Email - Best Effort Email

    [Fact]
    public async Task InviteByEmail_Success_WhenUserExistsAndNotInOrg()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, null, null, userEmail);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
        _notificationBusiness.Verify(n => n.SendEmail(userEmail, "Existing User 2"), Times.Once);
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenEmailIsDifferentCase()
    {
        // Arrange
        var userEmail = "ExistIng.User2@TEST.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, null, null, userEmail);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenUserExistsAndAlreadyInOrg()
    {
        // Arrange
        var userEmail = "existing.user@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, null, null, userEmail);

        // Assert
        Assert.True(result);
        var orgUserCount = await Context.OrganizationUsers
            .CountAsync(ou => ou.UserId == uid && ou.OrganizationId == oid);
        Assert.Equal(1, orgUserCount); // Should still only have one entry
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenUserExistsAndNotInProject()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, null, rid, null, userEmail);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == uid2 && pm.ProjectId == pid));
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenUserExistsAndAlreadyInProject()
    {
        // Arrange
        var userEmail = "existing.user@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

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
            oid, pid, null, rid, null, userEmail);

        // Assert
        Assert.True(result);
        var projectMemberCount = await Context.ProjectMembers
            .CountAsync(pm => pm.UserId == uid && pm.ProjectId == pid);
        Assert.Equal(1, projectMemberCount); // Should still only have one entry
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenUserInGroupAlreadyInProject()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

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
            oid, pid, null, rid, null, userEmail);

        // Assert
        Assert.True(result);
        var directProjectMemberCount = await Context.ProjectMembers
            .CountAsync(pm => pm.UserId == uid2 && pm.ProjectId == pid);
        Assert.Equal(0, directProjectMemberCount); // Should not create duplicate membership
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenEmailSendFails_ExistingUser()
    {
        // Arrange - Email send failure should NOT cause failure for existing users
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, null, null, userEmail);

        // Assert - Should still succeed (best-effort)
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
    }

    #endregion

    #region Existing User by UserId - Best Effort Email

    [Fact]
    public async Task InviteByUserId_Success_WhenUserExistsAndNotInOrg()
    {
        // Arrange
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, null, uid2, null);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
        _notificationBusiness.Verify(n => n.SendEmail("existing.user2@test.com", "Existing User 2"), Times.Once);
    }

    [Fact]
    public async Task InviteByUserId_Success_WhenUserExistsAndNotInProject()
    {
        // Arrange
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, null, rid, uid2, null);

        // Assert
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == uid2 && pm.ProjectId == pid && pm.RoleId == rid));
    }

    [Fact]
    public async Task InviteByUserId_Success_WhenEmailSendFails()
    {
        // Arrange - Email send failure should NOT cause failure for existing users
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, null, uid2, null);

        // Assert - Should still succeed (best-effort)
        Assert.True(result);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
    }

    [Fact]
    public async Task InviteByUserId_Fails_WhenUserDoesNotExist()
    {
        // Arrange
        var nonExistentUserId = 99999L;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, null, nonExistentUserId, null));

        Assert.Contains("not found", exception.Message);
    }

    #endregion

    #region New User by Email - Transaction with Rollback

    [Fact]
    public async Task InviteByEmail_Success_WhenUserDoesNotExist()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, newUserEmail))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, null, null, newUserEmail);

        // Assert
        Assert.True(result);

        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);
        Assert.Equal(newUserEmail, newUser.Name); // Name should be set to email
        Assert.Equal(newUserEmail, newUser.Email);

        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == newUser.Id && ou.OrganizationId == oid));

        _notificationBusiness.Verify(n => n.SendEmail(newUserEmail, newUserEmail), Times.Once);
    }

    [Fact]
    public async Task InviteByEmail_Success_WhenUserDoesNotExistAndAddedToProject()
    {
        // Arrange
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, newUserEmail))
            .ReturnsAsync(true);

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, null, rid, null, newUserEmail);

        // Assert
        Assert.True(result);

        var newUser = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.NotNull(newUser);

        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == newUser.Id && ou.OrganizationId == oid));
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.UserId == newUser.Id && pm.ProjectId == pid && pm.RoleId == rid));

        _notificationBusiness.Verify(n => n.SendEmail(newUserEmail, newUserEmail), Times.Once);
    }

    [Fact]
    public async Task InviteByEmail_RollsBack_WhenEmailSendFailsForNewUser()
    {
        // Arrange - CRITICAL TEST: Email failure should rollback new user creation
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, newUserEmail))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, null, null, newUserEmail));

        Assert.Contains("Failed to send invitation email", exception.Message);
        Assert.Contains(newUserEmail, exception.Message);
        Assert.Contains("User was not created", exception.Message);

        // CRITICAL: Verify user was NOT created (transaction rollback worked)
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.Null(user);

        // CRITICAL: Verify no organization membership was created
        var orgUsers = await Context.OrganizationUsers
            .Where(ou => ou.OrganizationId == oid)
            .ToListAsync();
        Assert.DoesNotContain(orgUsers, ou => 
            Context.Users.Any(u => u.Id == ou.UserId && u.Email == newUserEmail));
    }

    [Fact]
    public async Task InviteByEmail_RollsBack_WhenEmailSendFailsForNewUserWithProject()
    {
        // Arrange - CRITICAL TEST: Email failure should rollback new user and project membership
        var newUserEmail = "newuser@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(newUserEmail, newUserEmail))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, pid, null, rid, null, newUserEmail));

        Assert.Contains("Failed to send invitation email", exception.Message);

        // CRITICAL: Verify user was NOT created
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Email == newUserEmail);
        Assert.Null(user);

        // CRITICAL: Verify no organization membership was created
        var orgUsers = await Context.OrganizationUsers
            .Where(ou => ou.OrganizationId == oid)
            .ToListAsync();
        Assert.DoesNotContain(orgUsers, ou => 
            Context.Users.Any(u => u.Id == ou.UserId && u.Email == newUserEmail));

        // CRITICAL: Verify no project membership was created
        var projectMembers = await Context.ProjectMembers
            .Where(pm => pm.ProjectId == pid)
            .ToListAsync();
        Assert.DoesNotContain(projectMembers, pm => 
            Context.Users.Any(u => u.Id == pm.UserId && u.Email == newUserEmail));
    }

    #endregion

    #region Group Tests

    [Fact]
    public async Task InviteByGroup_Success_WhenGroupExistsAndNotInProject()
    {
        // Arrange
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Add users to group
        var group = await Context.Groups.Include(g => g.Users).FirstOrDefaultAsync(g => g.Id == gid);
        group!.Users.Add(await Context.Users.FindAsync(uid));
        group.Users.Add(await Context.Users.FindAsync(uid2));
        await Context.SaveChangesAsync();

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, gid, rid, null, null);

        // Assert
        Assert.True(result);
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.GroupId == gid && pm.ProjectId == pid && pm.RoleId == rid));

        // Verify emails sent to all group members
        _notificationBusiness.Verify(n => n.SendEmail("existing.user@test.com", "Existing User"), Times.Once);
        _notificationBusiness.Verify(n => n.SendEmail("existing.user2@test.com", "Existing User 2"), Times.Once);
    }

    [Fact]
    public async Task InviteByGroup_Success_WhenGroupAlreadyInProject()
    {
        // Arrange
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Add group to project first
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
            oid, pid, gid, rid, null, null);

        // Assert
        Assert.True(result);
        var projectMemberCount = await Context.ProjectMembers
            .CountAsync(pm => pm.GroupId == gid && pm.ProjectId == pid);
        Assert.Equal(1, projectMemberCount); // Should not duplicate
    }

    [Fact]
    public async Task InviteByGroup_Success_WhenEmailSendFails_BestEffort()
    {
        // Arrange - Email failures should NOT cause group invitation to fail
        _notificationBusiness.Setup(n => n.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Add user to group
        var group = await Context.Groups.Include(g => g.Users).FirstOrDefaultAsync(g => g.Id == gid);
        group!.Users.Add(await Context.Users.FindAsync(uid));
        await Context.SaveChangesAsync();

        // Act
        var result = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, gid, rid, null, null);

        // Assert - Should still succeed (best-effort)
        Assert.True(result);
        Assert.True(await Context.ProjectMembers.AnyAsync(
            pm => pm.GroupId == gid && pm.ProjectId == pid));
    }

    [Fact]
    public async Task InviteByGroup_Fails_WhenGroupDoesNotExist()
    {
        // Arrange
        var nonExistentGroupId = 99999L;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, pid, nonExistentGroupId, rid, null, null));

        Assert.Contains("not found", exception.Message);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Invite_Fails_WhenNoIdentifierProvided()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, null, null, null));

        Assert.Contains("Exactly one of groupId, userId, or userEmail must be supplied", exception.Message);
    }

    [Fact]
    public async Task Invite_Fails_WhenMultipleIdentifiersProvided()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, null, uid, "test@test.com"));

        Assert.Contains("Exactly one of groupId, userId, or userEmail must be supplied", exception.Message);
    }

    [Fact]
    public async Task Invite_Fails_WhenProjectProvidedWithoutRole()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, pid, null, null, uid, null));

        Assert.Contains("roleId is required", exception.Message);
    }

    [Fact]
    public async Task Invite_Fails_WhenRoleProvidedWithoutProject()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, rid, null, "test@test.com"));

        Assert.Contains("Roles do not exist for organization users", exception.Message);
    }

    [Fact]
    public async Task Invite_Fails_WhenUserIdProvidedForOrgInvitation()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, null, null, uid, null));

        Assert.Contains("Only userEmail is allowed for organization invitations", exception.Message);
    }

    [Fact]
    public async Task Invite_Fails_WhenGroupIdProvidedForOrgInvitation()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationBusiness.InviteAndAddUserToHierarchy(
                oid, null, gid, null, null, null));

        Assert.Contains("Only userEmail is allowed for organization invitations", exception.Message);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task InviteByEmail_Success_WithMultipleOrganizations()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act - Add to oid
        var result1 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, null, null, null, null, userEmail);

        // Act - Add to oid2
        var result2 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid2, null, null, null, null, userEmail);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid));
        Assert.True(await Context.OrganizationUsers.AnyAsync(
            ou => ou.UserId == uid2 && ou.OrganizationId == oid2));
    }

    [Fact]
    public async Task InviteByEmail_Success_WithMultipleProjects()
    {
        // Arrange
        var userEmail = "existing.user2@test.com";
        _notificationBusiness.Setup(n => n.SendEmail(userEmail, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act - Add to pid
        var result1 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid, null, rid, null, userEmail);

        // Act - Add to pid2
        var result2 = await _invitationBusiness.InviteAndAddUserToHierarchy(
            oid, pid2, null, rid, null, userEmail);

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