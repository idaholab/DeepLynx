using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class OrganizationBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness = null!;
    private RoleBusiness _roleBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<OrganizationBusiness>> _mockLoggerOrg = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private OrganizationBusiness _organizationBusiness = null!;
    private Mock<IBulkCopyUpsertExecutor> _mockBulkCopyUpsertExecutor = null!;

    public long oid; // organization ID
    public long oid2; // second organization ID
    public long uid; // user IDs
    public long uid2;

    public OrganizationBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // used in multiple contexts
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new Mock<IBulkCopyUpsertExecutor>();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor.Object);
        _roleBusiness = new RoleBusiness(Context, _eventBusiness);

        // org business and dependencies
        _mockLoggerOrg = new Mock<ILogger<OrganizationBusiness>>();
        _organizationBusiness = new OrganizationBusiness(
            Context, _eventBusiness, _roleBusiness, _mockLoggerOrg.Object);
    }

    #region OrganizationResponseDto Tests

    [Fact]
    public void OrganizationResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new OrganizationResponseDto
        {
            Id = 1,
            Name = "Test Organization",
            Description = "Test Description",
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Organization", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        // create test organization
        var testOrg = new Organization
        {
            Name = "Test Organization",
            Description = "Test org for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var archivedOrg = new Organization
        {
            Name = "Archived Organization",
            Description = "Archived org for tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = true
        };
        Context.Organizations.AddRange(testOrg, archivedOrg);
        await Context.SaveChangesAsync();
        oid = testOrg.Id;
        oid2 = archivedOrg.Id;

        // create test users
        var testUser = new User
        {
            Name = "Test User",
            Email = "test@test.com"
        };
        var newUser = new User
        {
            Name = "New User",
            Email = "newuser@test.com"
        };
        Context.Users.AddRange(testUser, newUser);
        await Context.SaveChangesAsync();
        uid = testUser.Id;
        uid2 = newUser.Id;

        // create test organization user
        var testOrgUser = new OrganizationUser
        {
            OrganizationId = oid,
            UserId = uid,
            IsOrgAdmin = false
        };
        Context.OrganizationUsers.Add(testOrgUser);
        await Context.SaveChangesAsync();
    }

    #region GetAllOrganizations Tests

    [Fact]
    public async Task GetAllOrganizations_ExcludesArchived()
    {
        // Act
        var result = await _organizationBusiness.GetAllOrganizations();
        var organizations = result.ToList();

        // Assert
        Assert.All(organizations, o => Assert.False(o.IsArchived));
        Assert.Contains(organizations, o => o.Id == oid);
        Assert.DoesNotContain(organizations, o => o.Id == oid2); // archived organization
    }

    [Fact]
    public async Task GetAllOrganizations_WithHideArchivedFalse_IncludesArchived()
    {
        // Act
        var result = await _organizationBusiness.GetAllOrganizations(false);
        var organizations = result.ToList();

        // Assert
        Assert.Contains(organizations, o => o.IsArchived);
        Assert.Contains(organizations, o => o.Id == oid);
        Assert.Contains(organizations, o => o.Id == oid2); // archived organization
    }

    #endregion

    #region GetOrganization Tests

    [Fact]
    public async Task GetOrganization_Succeeds_WhenExists()
    {
        // Act
        var result = await _organizationBusiness.GetOrganization(oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(oid, result.Id);
        Assert.Equal("Test Organization", result.Name);
        Assert.Equal("Test org for unit tests", result.Description);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetOrganization_Fails_IfNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.GetOrganization(99999));

        Assert.Contains("Organization with id 99999 does not exist", exception.Message);
    }

    [Fact]
    public async Task GetOrganization_Fails_IfArchivedOrg()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.GetOrganization(oid2)); // archived organization

        Assert.Contains($"Organization with id {oid2} is archived", exception.Message);
    }

    #endregion

    #region CreateOrganization Tests

    [Fact]
    public async Task CreateOrganization_Success_ReturnsCorrectValues()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "New Test Organization",
            Description = "New Test Organization Description",
        };
        
        var now =  DateTime.UtcNow;
        
        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.False(result.DefaultOrg);
        Assert.False(result.IsArchived);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // verify org was actually created in database
        var createdOrg = await Context.Organizations.FindAsync(result.Id);
        Assert.NotNull(createdOrg);
        Assert.Equal(dto.Name, createdOrg.Name);
    }
    
    [Fact]
    public async Task CreateOrganization_Success_CreatesDefaultRolesWithCorrectPermissions()
    {
        
        var defaultPermissions = DefaultPermissions.AllDefaultPermissions;
    
        foreach (var defaultPermission in defaultPermissions)
        {
            var permission = new Permission
            {
                Name = defaultPermission.Name,
                Resource = defaultPermission.Resource,
                Action = defaultPermission.Action,
                Description = defaultPermission.Description,
                IsDefault = true
            };
            Context.Permissions.Add(permission);
        }
        
        await Context.SaveChangesAsync();
        
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "New Test Organization",
            Description = "New Test Organization Description",
        };
        
        var now =  DateTime.UtcNow;
        
        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.False(result.DefaultOrg);
        Assert.False(result.IsArchived);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // verify org was actually created in database
        var createdOrg = await Context.Organizations.FindAsync(result.Id);
        Assert.NotNull(createdOrg);
        Assert.Equal(dto.Name, createdOrg.Name);
        
        var defaultRoles = await Context.Roles.Where(r => r.OrganizationId == result.Id).Include(r => r.Permissions).ToListAsync();
        var adminRole = defaultRoles.Single(r => r.Name == "Admin");
        var userRole =  defaultRoles.Single(r => r.Name == "User");
        
        AssertRolePermissions(adminRole, DefaultRolePermissions.Admin.AllowedPermissions);
        AssertRolePermissions(userRole, DefaultRolePermissions.User.AllowedPermissions);
    }

    [Fact]
    public async Task CreateOrganization_Success_CreatesEvent()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "Event Test Organization",
            Description = "A test organization for event logging"
        };

        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Test Organization", result.Name);

        // Ensure that the Organization create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(2, eventList.Count);

        var orgEvent = eventList.Single(e => e.Operation == "create" && e.EntityType == "organization" && e.EntityId == result.Id);

        Assert.NotNull(orgEvent);
    }

    [Fact]
    public async Task CreateOrganization_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Description = "Organization without name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _organizationBusiness.CreateOrganization(uid, dto));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateOrganization_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "",
            Description = "Organization with empty name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _organizationBusiness.CreateOrganization(uid, dto));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateOrganization_Success_OnSetsDefault()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "Default Org A",
            Description = "Should be default"
        };

        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto, true);

        // Assert
        Assert.NotNull(result);

        var created = await Context.Organizations.FindAsync(result.Id);
        Assert.NotNull(created);
        Assert.True(created!.DefaultOrg); // ensure the new org is default
    }

    [Fact]
    public async Task CreateOrganization_Success_NoDefaultOnCreatedOrg()
    {
        // Arrange
        var dto = new CreateOrganizationRequestDto
        {
            Name = "Non-Default Org",
            Description = "Should NOT be default"
        };

        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto);

        // Assert
        Assert.NotNull(result);

        var created = await Context.Organizations.FindAsync(result.Id);
        Assert.NotNull(created);
        Assert.False(created!.DefaultOrg); // ensure the new org is not default
    }

    [Fact]
    public async Task CreateOrganization_Success_ClearsOtherDefaultOrgs()
    {
        // Arrange
        // Ensure there is at least one existing default org
        var existingDefault = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(existingDefault);
        existingDefault!.DefaultOrg = true;
        Context.Organizations.Update(existingDefault);
        await Context.SaveChangesAsync();

        var dto = new CreateOrganizationRequestDto
        {
            Name = "Default Org B",
            Description = "Becomes the new default"
        };

        // Act
        var result = await _organizationBusiness.CreateOrganization(uid, dto, true);

        // Assert
        Assert.NotNull(result);

        var allOrgs = await Context.Organizations.ToListAsync();

        // New org is default
        var created = allOrgs.Single(o => o.Id == result.Id);
        Assert.True(created.DefaultOrg);

        // All *other* orgs are no longer default
        foreach (var org in allOrgs.Where(o => o.Id != result.Id)) Assert.False(org.DefaultOrg);
    }

    #endregion

    #region UpdateOrganization Tests

        [Fact]
        public async Task UpdateOrganization_Success_ReturnsOrganization()
        {
            // Arrange
            var dto = new UpdateOrganizationRequestDto
            {
                Name = "Updated Organization",
                Description = "Updated description"
            };
            
            var now = DateTime.UtcNow;

        // Act
        var result = await _organizationBusiness.UpdateOrganization(uid, oid, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(oid, result.Id);
            Assert.Equal("Updated Organization", result.Name);
            Assert.Equal("Updated description", result.Description);
            Assert.False(result.DefaultOrg);
            Assert.False(result.IsArchived);
            Assert.True(result.LastUpdatedAt >= now);
            Assert.Equal(uid, result.LastUpdatedBy);

        // Verify it was actually saved to DB
        var savedOrg = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(savedOrg);
        Assert.Equal("Updated Organization", savedOrg.Name);
        Assert.Equal("Updated description", savedOrg.Description);

        // Ensure that the Organization update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("organization", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateOrganization_Success_CreatesEvent()
    {
        // Arrange
        var dto = new UpdateOrganizationRequestDto
        {
            Name = "Event Updated Organization"
        };

        // Act
        var result = await _organizationBusiness.UpdateOrganization(uid, oid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Updated Organization", result.Name);

        // Ensure that the Organization update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("organization", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateOrganization_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateOrganizationRequestDto
        {
            Name = "Updated Organization"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.UpdateOrganization(uid, 99999, dto));

        Assert.Contains("Organization with id 99999 does not exist", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateOrganization_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateOrganizationRequestDto
        {
            Name = "Updated Archived Organization"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.UpdateOrganization(uid, oid2, dto)); // archived organization

        Assert.Contains($"Organization with id {oid2} does not exist", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateOrganization_Success_SetsOrgAsDefault()
    {
        // Arrange: ensure current org starts non-default
        var org = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(org);
        org!.DefaultOrg = false;
        Context.Organizations.Update(org);
        await Context.SaveChangesAsync();

        var dto = new UpdateOrganizationRequestDto
        {
            Name = "Becomes Default",
            Description = "Set DefaultOrg = true",
            DefaultOrg = true
        };

        // Act
        var result = await _organizationBusiness.UpdateOrganization(uid, oid, dto);

        // Assert
        Assert.NotNull(result);
        var saved = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(saved);
        Assert.True(saved!.DefaultOrg);
    }

    [Fact]
    public async Task UpdateOrganization_Success_SetsOrgAsNonDefault()
    {
        // Arrange: make target org currently default
        var org = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(org);
        org!.DefaultOrg = true;
        Context.Organizations.Update(org);
        await Context.SaveChangesAsync();

        var dto = new UpdateOrganizationRequestDto
        {
            Name = "No Longer Default",
            Description = "Set DefaultOrg = false",
            DefaultOrg = false
        };

        // Act
        var result = await _organizationBusiness.UpdateOrganization(uid, oid, dto);

        // Assert
        Assert.NotNull(result);
        var saved = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(saved);
        Assert.False(saved!.DefaultOrg);
    }

    [Fact]
    public async Task UpdateOrganization_Success_ClearsOtherDefaultOrgs()
    {
        // Arrange: seed another active org that is currently default
        var otherOrg = new Organization
        {
            Name = "Existing Default Org",
            Description = "Pre-existing default",
            DefaultOrg = true,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(otherOrg);

        // Ensure target org is explicitly non-default before update
        var targetOrg = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(targetOrg);
        targetOrg!.DefaultOrg = false;

        await Context.SaveChangesAsync();

        var dto = new UpdateOrganizationRequestDto
        {
            Name = "Promoted To Default",
            Description = "Will become the sole default",
            DefaultOrg = true
        };

        // Act
        var result = await _organizationBusiness.UpdateOrganization(uid, oid, dto);

        // Assert
        Assert.NotNull(result);

        var all = await Context.Organizations.ToListAsync();

        // Updated org should now be default
        var updated = await Context.Organizations.FindAsync(oid);
        Assert.NotNull(updated);
        Assert.True(updated!.DefaultOrg);

        // All other orgs should be non-default
        foreach (var o in all.Where(o => o.Id != oid)) Assert.False(o.DefaultOrg);
    }

    #endregion

    #region ArchiveOrganization Tests

        [Fact]
        public async Task ArchiveOrganization_Succeeds_IfNotArchived()
        {
            // Arrange
            var now = DateTime.UtcNow;
            
            // Act
            var result = await _organizationBusiness.ArchiveOrganization(uid, oid);

        // Assert
        Assert.True(result);

            // Verify it was actually saved to DB
            var savedOrg = await Context.Organizations.FindAsync(oid);
            Assert.NotNull(savedOrg);
            Assert.True(savedOrg.IsArchived);
            Assert.Equal("Test Organization",  savedOrg.Name);
            Assert.Equal("Test org for unit tests", savedOrg.Description);
            Assert.True(savedOrg.LastUpdatedAt >= now);
            Assert.False(savedOrg.DefaultOrg);
            Assert.Equal(uid, savedOrg.LastUpdatedBy);

        // Ensure that the Organization archive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("organization", actualEvent.EntityType);
        Assert.Equal(oid, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveOrganization_Fails_IfArchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.ArchiveOrganization(uid, oid2)); // already archived

        Assert.Contains($"Organization with id {oid2} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveOrganization_Fails_IfNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.ArchiveOrganization(uid, 99999));

        Assert.Contains("Organization with id 99999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveOrganization Tests

        [Fact]
        public async Task UnarchiveOrganization_Succeeds_IfArchived()
        {
            // Arrange
            var now = DateTime.UtcNow;
            
            // Act
            var result = await _organizationBusiness.UnarchiveOrganization(uid, oid2);

            // Assert
            Assert.True(result);
            
            // Verify it was actually saved to DB
            var savedOrg = await Context.Organizations.FindAsync(oid2);
            Assert.Equal("Archived Organization", savedOrg?.Name);
            Assert.Equal("Archived org for tests", savedOrg?.Description);
            Assert.False(savedOrg?.DefaultOrg);
            Assert.False(savedOrg?.IsArchived);
            Assert.True(savedOrg?.LastUpdatedAt >= now);
            Assert.Equal(uid, savedOrg.LastUpdatedBy);

        // Ensure that the Organization unarchive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("unarchive", actualEvent.Operation);
        Assert.Equal("organization", actualEvent.EntityType);
        Assert.Equal(oid2, actualEvent.EntityId);
    }

    [Fact]
    public async Task UnarchiveOrganization_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.UnarchiveOrganization(uid, oid)); // not archived

        Assert.Contains($"Organization with id {oid} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveOrganization_Fails_IfNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.UnarchiveOrganization(uid, 99999));

        Assert.Contains("Organization with id 99999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteOrganization Tests

    [Fact]
    public async Task DeleteOrganization_Succeeds_WhenExists()
    {
        // Act
        var result = await _organizationBusiness.DeleteOrganization(oid);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedOrg = await Context.Organizations.FindAsync(oid);
        Assert.Null(deletedOrg);
    }

    [Fact]
    public async Task DeleteOrganization_Fails_IfNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.DeleteOrganization(99999));

        Assert.Contains("Organization with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task DeleteOrganization_Fails_IfArchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.DeleteOrganization(oid2)); // archived organization

        Assert.Contains($"Organization with id {oid2} not found", exception.Message);
    }

    #endregion

    #region AddUser Tests

    [Fact]
    public async Task AddUser_Succeeds_IfOrgAndUserExists()
    {
        // Act
        var result = await _organizationBusiness.AddUserToOrganization(oid, uid2);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var orgUser = await Context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == oid && ou.UserId == uid2);
        Assert.NotNull(orgUser);
        Assert.False(orgUser.IsOrgAdmin);
    }

    [Fact]
    public async Task AddUser_Fails_IfOrgUserExists()
    {
        // Act - try to add user that's already in the org
        var result = await _organizationBusiness.AddUserToOrganization(oid, uid);

        // Assert
        Assert.False(result); // should return false when user already exists
    }

    [Fact]
    public async Task AddUser_Fails_IfUserNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.AddUserToOrganization(oid, 99999));

        Assert.Contains("User with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task AddUser_Fails_IfOrgNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.AddUserToOrganization(99999, uid));

        Assert.Contains("Organization with id 99999 not found", exception.Message);
    }

    #endregion

    #region UpdateUserAdmin Tests

    [Fact]
    public async Task UpdateUserAdmin_Succeeds_IfOrgUserExists()
    {
        // Act - set user as admin
        var result = await _organizationBusiness.SetOrganizationAdminStatus(oid, uid, true);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var orgUser = await Context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == oid && ou.UserId == uid);
        Assert.NotNull(orgUser);
        Assert.True(orgUser.IsOrgAdmin);
    }

    [Fact]
    public async Task UpdateUserAdmin_Fails_IfOrgUserNotExists()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.SetOrganizationAdminStatus(oid, uid2, true));

        Assert.Contains($"User with id {uid2} not found in Org with id {oid}", exception.Message);
    }

    #endregion

    #region RemoveUser Tests

    [Fact]
    public async Task RemoveUser_Succeeds_IfOrgUserExists()
    {
        // Act
        var result = await _organizationBusiness.RemoveUserFromOrganization(oid, uid);

        // Assert
        Assert.True(result);

        // Verify it was actually removed from DB
        var orgUser = await Context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == oid && ou.UserId == uid);
        Assert.Null(orgUser);
    }

    [Fact]
    public async Task RemoveUser_Fails_IfOrgUserNotExists()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _organizationBusiness.RemoveUserFromOrganization(oid, uid2));

        Assert.Contains($"User with id {uid2} not found in Org with id {oid}", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateOrganization_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testOrganization = new Organization
        {
            Name = "Test Organization LastUpdatedBy",
            Description = "Test description",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Act
        Context.Organizations.Add(testOrganization);
        await Context.SaveChangesAsync();

        // Assert
        var savedOrganization = await Context.Organizations.FindAsync(testOrganization.Id);
        Assert.NotNull(savedOrganization);
        Assert.Equal(uid, savedOrganization.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateOrganization_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testOrganization = new Organization
        {
            Name = "Test Organization Navigation",
            Description = "Test description 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.Organizations.Add(testOrganization);
        await Context.SaveChangesAsync();

        // Act
        var organizationWithUser = await Context.Organizations
            .Include(o => o.LastUpdatedByUser)
            .FirstAsync(o => o.Id == testOrganization.Id);

        // Assert
        Assert.NotNull(organizationWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", organizationWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test@test.com", organizationWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, organizationWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateOrganization_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testOrganization = new Organization
        {
            Name = "Test Organization Null",
            Description = "Test description 3",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.Organizations.Add(testOrganization);
        await Context.SaveChangesAsync();

        // Assert
        var savedOrganization = await Context.Organizations.FindAsync(testOrganization.Id);
        Assert.NotNull(savedOrganization);
        Assert.Null(savedOrganization.LastUpdatedBy);

        var organizationWithUser = await Context.Organizations
            .Include(o => o.LastUpdatedByUser)
            .FirstAsync(o => o.Id == testOrganization.Id);

        Assert.Null(organizationWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateOrganization_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testOrganization = new Organization
        {
            Name = "Test Organization Update",
            Description = "Test description 4",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Organizations.Add(testOrganization);
        await Context.SaveChangesAsync();

        // Act
        testOrganization.LastUpdatedBy = uid;
        testOrganization.Name = "Updated Organization Name";
        testOrganization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Organizations.Update(testOrganization);
        await Context.SaveChangesAsync();

        // Assert
        var updatedOrganization = await Context.Organizations
            .Include(o => o.LastUpdatedByUser)
            .FirstAsync(o => o.Id == testOrganization.Id);

        Assert.Equal(uid, updatedOrganization.LastUpdatedBy);
        Assert.NotNull(updatedOrganization.LastUpdatedByUser);
        Assert.Equal("Test User", updatedOrganization.LastUpdatedByUser.Name);
        Assert.Equal("Updated Organization Name", updatedOrganization.Name);
    }

    #endregion
    
    private void AssertRolePermissions(
        Role role, 
        Dictionary<string, string[]> expectedPermissions)
    {
        // All permissions should be marked as default
        Assert.True(
            role.Permissions.All(p => p.IsDefault),
            $"Role '{role.Name}' has permissions not marked as default"
        );

        // Verify permission count
        var expectedCount = expectedPermissions.Sum(kvp => kvp.Value.Length);
        Assert.Equal(expectedCount, role.Permissions.Count);

        // Verify each resource type has the correct actions
        foreach (var (resource, expectedActions) in expectedPermissions)
        {
            var actualActions = role.Permissions
                .Where(p => p.Resource == resource)
                .Select(p => p.Action)
                .OrderBy(a => a)
                .ToList();

            var sortedExpectedActions = expectedActions.OrderBy(a => a).ToList();

            Assert.Equal(sortedExpectedActions, actualActions);
        }
    }
}