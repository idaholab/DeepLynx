using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Cache;
using deeplynx.models;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class SensitivityLabelGrantBusinessTests : IntegrationTestBase
{
    private SensitivityLabelGrantBusiness _grantBusiness = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;

    public long oid; // organization ID

    public long pid; // project ID (label + membership scope)
    public long pid2; // second project (used for "not a member of this project" cases)

    public long lid; // test label (project-scoped)
    public long lid2; // org-level label (ProjectId == null)

    public long uid; // primary org+project member user
    public long uid2; // second org+project member user
    public long uid3; // user who is a member of the test group (not directly a project member)
    public long uid4; // user who is NOT an org member at all
    public long uid5; // user who IS an org member but NOT a member of pid

    public long gid; // test group - member of the org and of pid
    public long gid2; // "other group" - member of the org but NOT a member of pid

    public long readActionId;
    public long writeActionId;
    public long updateActionId;

    public SensitivityLabelGrantBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _sensitivityLabelService = new SensitivityLabelService(Context); // or whatever its real ctor is
        _grantBusiness = new SensitivityLabelGrantBusiness(Context, _sensitivityLabelService);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // ----- Users -----
        var user = new User { Name = "Test User", Email = "grant_test@example.com", IsArchived = false };
        var user2 = new User { Name = "Second Test User", Email = "grant_test2@example.com", IsArchived = false };
        var groupMemberUser = new User { Name = "Group Member User", Email = "group_member@example.com", IsArchived = false };
        var nonOrgUser = new User { Name = "Non Org User", Email = "non_org_user@example.com", IsArchived = false };
        var orgOnlyUser = new User { Name = "Org Only User", Email = "org_only_user@example.com", IsArchived = false };
        Context.Users.AddRange(user, user2, groupMemberUser, nonOrgUser, orgOnlyUser);
        await Context.SaveChangesAsync();
        uid = user.Id;
        uid2 = user2.Id;
        uid3 = groupMemberUser.Id;
        uid4 = nonOrgUser.Id;
        uid5 = orgOnlyUser.Id;

        // ----- Organization -----
        var testOrg = new Organization
        {
            Name = "Test Organization",
            Description = "Test org for grant business unit tests",
            IsArchived = false
        };
        Context.Organizations.Add(testOrg);
        await Context.SaveChangesAsync();
        oid = testOrg.Id;

        // ----- Projects -----
        var testProject = new Project { Name = "Test Project", OrganizationId = oid, IsArchived = false };
        var testProject2 = new Project { Name = "Test Project 2", OrganizationId = oid, IsArchived = false };
        Context.Projects.AddRange(testProject, testProject2);
        await Context.SaveChangesAsync();
        pid = testProject.Id;
        pid2 = testProject2.Id;

        // ----- Groups -----
        var testGroup = new Group { Name = "Test Group", OrganizationId = oid, IsArchived = false };
        var otherGroup = new Group { Name = "Other Group", OrganizationId = oid, IsArchived = false };
        Context.Groups.AddRange(testGroup, otherGroup);
        await Context.SaveChangesAsync();
        gid = testGroup.Id;
        gid2 = otherGroup.Id;

        testGroup.Users.Add(groupMemberUser);
        await Context.SaveChangesAsync();

        // ----- Organization membership -----
        // uid4 is intentionally left out of the organization entirely.
        Context.OrganizationUsers.AddRange(
            new OrganizationUser { OrganizationId = oid, UserId = uid },
            new OrganizationUser { OrganizationId = oid, UserId = uid2 },
            new OrganizationUser { OrganizationId = oid, UserId = uid3 },
            new OrganizationUser { OrganizationId = oid, UserId = uid5 });
        await Context.SaveChangesAsync();

        // ----- Project membership -----
        // uid5 is an org member but intentionally NOT added to pid.
        // gid2 (otherGroup) is an org group but intentionally NOT added to pid.
        Context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = pid, UserId = uid },
            new ProjectMember { ProjectId = pid, UserId = uid2 },
            new ProjectMember { ProjectId = pid, GroupId = gid });
        await Context.SaveChangesAsync();

        // ----- Labels -----
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label",
            Description = "Test label for grant business unit tests",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var orgLabel = new SensitivityLabel
        {
            Name = "Org Label",
            Description = "Org-level label for grant business unit tests",
            ProjectId = null,
            OrganizationId = oid,
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.SensitivityLabels.AddRange(testLabel, orgLabel);
        await Context.SaveChangesAsync();
        lid = testLabel.Id;
        lid2 = orgLabel.Id;

        // ----- Permission actions (global lookup table, seeded via migration) -----
        readActionId = (await Context.SensitivityLabelPermissionActions.FirstAsync(a => a.Name == "read record")).Id;
        writeActionId = (await Context.SensitivityLabelPermissionActions.FirstAsync(a => a.Name == "write record")).Id;
        updateActionId = (await Context.SensitivityLabelPermissionActions.FirstAsync(a => a.Name == "update record")).Id;
    }

    // --------------------- Helpers ---------------------

    private async Task<SensitivityLabelGrant> AddGrantAsync(long labelId, long? userId, long? groupId, long permissionId, long? grantedBy = null)
    {
        var grant = new SensitivityLabelGrant
        {
            LabelId = labelId,
            UserId = userId,
            GroupId = groupId,
            LabelPermissionId = permissionId,
            GrantedBy = grantedBy ?? uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.SensitivityLabelGrants.Add(grant);
        await Context.SaveChangesAsync();
        return grant;
    }

    #region GetMembersWithLabelAccess Tests

    [Fact]
    public async Task GetMembersWithLabelAccess_ReturnsEmpty_WhenNoGrants()
    {
        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_ReturnsUserGrant_WithPermission()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId, grantedBy: uid2);

        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(uid, member.UserId);
        Assert.Equal("Test User", member.UserName);
        Assert.Equal("grant_test@example.com", member.UserEmail);
        Assert.Null(member.GroupId);

        var permission = Assert.Single(member.Permissions);
        Assert.Equal(readActionId, permission.LabelPermissionId);
        Assert.Equal("read record", permission.LabelPermissionName);
        Assert.Equal(uid2, permission.GrantedBy);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_ReturnsGroupGrant_WithGroupMembers()
    {
        // Arrange
        await AddGrantAsync(lid, null, gid, writeActionId);

        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);
        var member = Assert.Single(result);

        // Assert
        Assert.Null(member.UserId);
        Assert.Equal(gid, member.GroupId);
        Assert.Equal("Test Group", member.GroupName);

        Assert.NotNull(member.GroupMembers);
        var groupMember = Assert.Single(member.GroupMembers!);
        Assert.Equal(uid3, groupMember.UserId);
        Assert.Equal("Group Member User", groupMember.UserName);
        Assert.Equal("group_member@example.com", groupMember.UserEmail);

        var permission = Assert.Single(member.Permissions);
        Assert.Equal(writeActionId, permission.LabelPermissionId);
        Assert.Equal("write record", permission.LabelPermissionName);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_ReturnsGroupGrant_WithPermission()
    {
        // Arrange
        await AddGrantAsync(lid, null, gid, readActionId, grantedBy: uid2);

        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);
        var member = Assert.Single(result);

        // Assert
        Assert.Null(member.UserId);
        Assert.Equal(gid, member.GroupId);
        Assert.Equal("Test Group", member.GroupName);

        var permission = Assert.Single(member.Permissions);
        Assert.Equal(readActionId, permission.LabelPermissionId);
        Assert.Equal("read record", permission.LabelPermissionName);
        Assert.Equal(uid2, permission.GrantedBy);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_RollsUpMultiplePermissions_ForSameUser()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        await AddGrantAsync(lid, uid, null, writeActionId);

        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(2, member.Permissions.Count);
        Assert.Contains(member.Permissions, p => p.LabelPermissionName == "read record");
        Assert.Contains(member.Permissions, p => p.LabelPermissionName == "write record");
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_ReturnsBothUsersAndGroups()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        await AddGrantAsync(lid, null, gid, writeActionId);

        // Act
        var result = await _grantBusiness.GetMembersWithLabelAccess(lid, oid, pid);
        var members = result.ToList();

        // Assert
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.UserId == uid);
        Assert.Contains(members, m => m.GroupId == gid);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_Fails_IfLabelNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.GetMembersWithLabelAccess(99999, oid, pid));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task GetMembersWithLabelAccess_Fails_IfLabelBelongsToDifferentOrganization()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.GetMembersWithLabelAccess(lid, oid + 99999, pid));

        Assert.Contains($"Sensitivity label with id {lid} not found", exception.Message);
    }

    #endregion

    #region GetMemberPermissionsForLabel Tests

    [Fact]
    public async Task GetMemberPermissionsForLabel_ReturnsPermissions_ForUser()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);

        // Act
        var result = await _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid, userId: uid);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(uid, member.UserId);
        var permission = Assert.Single(member.Permissions);
        Assert.Equal("read record", permission.LabelPermissionName);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_ReturnsPermissions_ForGroup()
    {
        // Arrange
        await AddGrantAsync(lid, null, gid, writeActionId);

        // Act
        var result = await _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid, groupId: gid);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(gid, member.GroupId);
        Assert.NotNull(member.GroupMembers);
        Assert.Contains(member.GroupMembers!, gm => gm.UserId == uid3);
        var permission = Assert.Single(member.Permissions);
        Assert.Equal("write record", permission.LabelPermissionName);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_ReturnsEmpty_WhenMemberHasNoGrants()
    {
        // Act
        var result = await _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid, userId: uid);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_FiltersOutOtherMembersGrants()
    {
        // Arrange - grant belongs to uid2, query is for uid
        await AddGrantAsync(lid, uid2, null, readActionId);

        // Act
        var result = await _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid, userId: uid);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_Fails_IfBothUserAndGroupProvided()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid, userId: uid, groupId: gid));

        Assert.Contains("Exactly one of userId or groupId must be provided", exception.Message);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_Fails_IfNeitherUserNorGroupProvided()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _grantBusiness.GetMemberPermissionsForLabel(lid, oid, pid));

        Assert.Contains("Exactly one of userId or groupId must be provided", exception.Message);
    }

    [Fact]
    public async Task GetMemberPermissionsForLabel_Fails_IfLabelNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.GetMemberPermissionsForLabel(99999, oid, pid, userId: uid));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);
    }

    #endregion

    #region SetAccessForLabel Tests

    [Fact]
    public async Task SetAccessForLabel_Success_CreatesGrantForUser()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { readActionId } };

        // Act
        var result = await _grantBusiness.SetAccessForLabel(uid2, lid, oid, pid, dto);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(uid, member.UserId);
        var permission = Assert.Single(member.Permissions);
        Assert.Equal(readActionId, permission.LabelPermissionId);
        Assert.Equal(uid2, permission.GrantedBy);

        var savedGrants = await Context.SensitivityLabelGrants
            .Where(g => g.LabelId == lid && g.UserId == uid)
            .ToListAsync();
        var savedGrant = Assert.Single(savedGrants);
        Assert.Equal(readActionId, savedGrant.LabelPermissionId);
        Assert.Equal(uid2, savedGrant.GrantedBy);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_CreatesGrantForGroup()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { GroupIds = new[] { gid }, LabelPermissionIds = new[] { writeActionId } };

        // Act
        var result = await _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto);
        var member = Assert.Single(result);

        // Assert
        Assert.Equal(gid, member.GroupId);
        var permission = Assert.Single(member.Permissions);
        Assert.Equal(writeActionId, permission.LabelPermissionId);

        var savedGrant = await Context.SensitivityLabelGrants
            .SingleAsync(g => g.LabelId == lid && g.GroupId == gid);
        Assert.Equal(writeActionId, savedGrant.LabelPermissionId);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_CreatesGrantsForMixedUsersAndGroups()
    {
        // Arrange
        var dto = new GrantLabelAccessDto
        {
            UserIds = new[] { uid },
            GroupIds = new[] { gid },
            LabelPermissionIds = new[] { readActionId, writeActionId }
        };

        // Act
        var result = await _grantBusiness.SetAccessForLabel(uid2, lid, oid, pid, dto);
        var members = result.ToList();

        // Assert
        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal(2, m.Permissions.Count));

        var userGrantCount = await Context.SensitivityLabelGrants.CountAsync(g => g.LabelId == lid && g.UserId == uid);
        var groupGrantCount = await Context.SensitivityLabelGrants.CountAsync(g => g.LabelId == lid && g.GroupId == gid);
        Assert.Equal(2, userGrantCount);
        Assert.Equal(2, groupGrantCount);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_CreatesOneGrantPerPermission()
    {
        // Arrange
        var dto = new GrantLabelAccessDto
        {
            UserIds = new[] { uid },
            LabelPermissionIds = new[] { readActionId, writeActionId, updateActionId }
        };

        // Act
        await _grantBusiness.SetAccessForLabel(uid2, lid, oid, pid, dto);

        // Assert
        var grantCount = await Context.SensitivityLabelGrants.CountAsync(g => g.LabelId == lid && g.UserId == uid);
        Assert.Equal(3, grantCount);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_ReplacesExistingGrantsForSpecifiedMember()
    {
        // Arrange - initial grant of read
        await AddGrantAsync(lid, uid, null, readActionId);

        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { writeActionId } };

        // Act - replace with write only
        await _grantBusiness.SetAccessForLabel(uid2, lid, oid, pid, dto);

        // Assert
        var grants = await Context.SensitivityLabelGrants
            .Where(g => g.LabelId == lid && g.UserId == uid)
            .ToListAsync();
        var grant = Assert.Single(grants);
        Assert.Equal(writeActionId, grant.LabelPermissionId);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_LeavesOtherMembersGrantsUntouched()
    {
        // Arrange - uid2 already has a grant on this label
        await AddGrantAsync(lid, uid2, null, readActionId);

        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { writeActionId } };

        // Act
        await _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto);

        // Assert - uid2's original grant is untouched
        var uid2Grant = await Context.SensitivityLabelGrants
            .SingleAsync(g => g.LabelId == lid && g.UserId == uid2);
        Assert.Equal(readActionId, uid2Grant.LabelPermissionId);
    }

    [Fact]
    public async Task SetAccessForLabel_Success_InvalidatesCache_ForGrantedUser()
    {
        // Arrange
        var cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        await CacheService.Instance.SetAsync(cacheKey, new List<long>(), (TimeSpan?)null);

        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { readActionId } };

        // Act
        await _grantBusiness.SetAccessForLabel(uid2, lid, oid, pid, dto);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(cacheKey));
    }

    [Fact]
    public async Task SetAccessForLabel_Success_InvalidatesCache_ForGroupMembers()
    {
        // Arrange - stale cache entry for a user who belongs to the group being granted access
        var cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid3, "read record");
        await CacheService.Instance.SetAsync(cacheKey, new List<long>(), (TimeSpan?)null);

        var dto = new GrantLabelAccessDto { GroupIds = new[] { gid }, LabelPermissionIds = new[] { readActionId } };

        // Act
        await _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(cacheKey));
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfNoUserOrGroupIds()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains("At least one userId or groupId must be provided", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfNoPermissionIds()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = Array.Empty<long>() };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains("At least one labelPermissionId must be provided", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfLabelNotFound()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, 99999, oid, pid, dto));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfUserNotOrganizationMember()
    {
        // Arrange - uid4 was never added to the organization
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid4 }, LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains("not members of organization", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfUserNotProjectMember()
    {
        // Arrange - uid5 is an org member but not a member of pid
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid5 }, LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains($"not members of project {pid}", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfGroupNotOrganizationMember()
    {
        // Arrange - nonexistent group id
        var dto = new GrantLabelAccessDto { GroupIds = new[] { 99999L }, LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains("Groups not found or not members of organization", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfGroupNotProjectMember()
    {
        // Arrange - gid2 is an org group but was never added to pid
        var dto = new GrantLabelAccessDto { GroupIds = new[] { gid2 }, LabelPermissionIds = new[] { readActionId } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains($"Groups not members of project {pid}", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_IfLabelPermissionIdInvalid()
    {
        // Arrange
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { 99999L } };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        Assert.Contains("Invalid label permission actions supplied", exception.Message);
    }

    [Fact]
    public async Task SetAccessForLabel_Fails_DoesNotCreateGrants_WhenPermissionIdInvalid()
    {
        // Arrange
        var initialGrantCount = await Context.SensitivityLabelGrants.CountAsync();
        var dto = new GrantLabelAccessDto { UserIds = new[] { uid }, LabelPermissionIds = new[] { 99999L } };

        // Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.SetAccessForLabel(uid, lid, oid, pid, dto));

        // Assert - no partial grants left behind
        var grantCount = await Context.SensitivityLabelGrants.CountAsync();
        Assert.Equal(initialGrantCount, grantCount);
    }

    #endregion

    #region RevokeAccessForLabel Tests

    [Fact]
    public async Task RevokeAccessForLabel_Success_RevokesUserGrants()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        await AddGrantAsync(lid, uid, null, writeActionId);

        // Act
        var result = await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, new[] { uid }, null);

        // Assert
        Assert.True(result);
        var remaining = await Context.SensitivityLabelGrants.Where(g => g.LabelId == lid && g.UserId == uid).ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Success_RevokesGroupGrants()
    {
        // Arrange
        await AddGrantAsync(lid, null, gid, writeActionId);

        // Act
        var result = await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, null, new[] { gid });

        // Assert
        Assert.True(result);
        var remaining = await Context.SensitivityLabelGrants.Where(g => g.LabelId == lid && g.GroupId == gid).ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Success_RevokesMixedUsersAndGroups()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        await AddGrantAsync(lid, null, gid, writeActionId);

        // Act
        var result = await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, new[] { uid }, new[] { gid });

        // Assert
        Assert.True(result);
        var remaining = await Context.SensitivityLabelGrants.Where(g => g.LabelId == lid).ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Success_LeavesOtherMembersGrantsUntouched()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        await AddGrantAsync(lid, uid2, null, readActionId);

        // Act
        await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, new[] { uid }, null);

        // Assert
        var uid2Grant = await Context.SensitivityLabelGrants
            .SingleOrDefaultAsync(g => g.LabelId == lid && g.UserId == uid2);
        Assert.NotNull(uid2Grant);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Success_InvalidatesCache_ForRevokedUser()
    {
        // Arrange
        await AddGrantAsync(lid, uid, null, readActionId);
        var cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        await CacheService.Instance.SetAsync(cacheKey, new List<long> { lid }, (TimeSpan?)null);

        // Act
        await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, new[] { uid }, null);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(cacheKey));
    }

    [Fact]
    public async Task RevokeAccessForLabel_Success_InvalidatesCache_ForGroupMembers()
    {
        // Arrange
        await AddGrantAsync(lid, null, gid, readActionId);
        var cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid3, "read record");
        await CacheService.Instance.SetAsync(cacheKey, new List<long> { lid }, (TimeSpan?)null);

        // Act
        await _grantBusiness.RevokeAccessForLabel(lid, oid, pid, null, new[] { gid });

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(cacheKey));
    }

    [Fact]
    public async Task RevokeAccessForLabel_Fails_IfNoUserOrGroupIds()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _grantBusiness.RevokeAccessForLabel(lid, oid, pid, null, null));

        Assert.Contains("At least one userId or groupId must be provided", exception.Message);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Fails_IfLabelNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.RevokeAccessForLabel(99999, oid, pid, new[] { uid }, null));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task RevokeAccessForLabel_Fails_IfNoMatchingGrantsExist()
    {
        // Act & Assert - uid has no grants on this label
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _grantBusiness.RevokeAccessForLabel(lid, oid, pid, new[] { uid }, null));

        Assert.Contains("No matching grants found to revoke", exception.Message);
    }

    #endregion
}