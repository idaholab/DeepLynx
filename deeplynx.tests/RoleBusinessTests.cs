using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class RoleBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RoleBusiness _roleBusiness;
    public long mid; // project member ID

    public long oid; // organization ID
    public long permid1; // permission IDs
    public long permid2;
    public long permid3;
    public long permid4;
    public long pid; // project ID
    public long pid2;
    public long rid1; // role IDs
    public long rid2;
    public long rid3;
    public long rid4;
    public long rid5;
    public long uid; // user ID

    public RoleBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _roleBusiness = new RoleBusiness(Context, _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // create user
        var user = new User
        {
            Name = "Test User",
            Email = "test@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        // create test organization
        var organization = new Organization
        {
            Name = "Test",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        oid = organization.Id;

        // create test project
        var project = new Project
        {
            Name = "Test",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organization.Id
        };

        // create test project 2
        var project2 = new Project
        {
            Name = "Test 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organization.Id
        };
        Context.Projects.AddRange(project, project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        // Create roles
        var role1 = new Role { Name = "Role 1", OrganizationId = oid };
        var role2 = new Role { Name = "Role 2", OrganizationId = oid, IsArchived = true }; // Archive role2
        var role3 = new Role { Name = "Role 3", OrganizationId = oid };
        var role4 = new Role { Name = "Role 4", OrganizationId = oid, ProjectId = pid };
        var role5 = new Role { Name = "Role 5", OrganizationId = oid, ProjectId = pid2 };
        Context.Roles.AddRange(role1, role2, role3, role4, role5);
        await Context.SaveChangesAsync();
        rid1 = role1.Id;
        rid2 = role2.Id;
        rid3 = role3.Id;
        rid4 = role4.Id;
        rid5 = role5.Id;

        // Delete role 3
        Context.Roles.Remove(role3);
        await Context.SaveChangesAsync();

        // Add user as project member
        var projectMember = new ProjectMember { ProjectId = pid, UserId = uid, RoleId = rid4 };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();
        mid = projectMember.Id;

        // Create permissions
        var permission1 = new Permission { Name = "Permission 1", Action = "read", Resource = "test", IsDefault = true};
        var permission2 = new Permission { Name = "Permission 2", Action = "write", Resource = "test", IsDefault = true};
        var permission3 = new Permission { Name = "Permission 3", Action = "execute", Resource = "test2", IsDefault = true};
        var permission4 = new Permission { Name = "Permission 4", Action = "glorbulon", Resource = "test", IsDefault = true};
        Context.Permissions.AddRange(permission1, permission2, permission3, permission4);
        await Context.SaveChangesAsync();
        permid1 = permission1.Id;
        permid2 = permission2.Id;
        permid3 = permission3.Id;
        permid4 = permission4.Id;

        // Delete permission 4
        Context.Permissions.Remove(permission4);
        await Context.SaveChangesAsync();

        // Add permissions 1 and 2 to role 1
        var role1perms = await Context.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.Id == rid1);
        role1perms.Permissions.Add(permission1);
        role1perms.Permissions.Add(permission2);
        await Context.SaveChangesAsync();

        // Add permission 3 to role 5
        var role5perms = await Context.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.Id == rid5);
        role5perms.Permissions.Add(permission3);
        await Context.SaveChangesAsync();
    }

    #region CreateRole Tests

    [Fact]
    public async Task CreateRole_Succeeds_WithOrgSupplied()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateRoleRequestDto
        {
            Name = "Org Role"
        };

        // Act
        var result = await _roleBusiness.CreateRole(uid, dto, oid);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Org Role", result.Name);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Null(result.ProjectId);

        // Verify it was actually saved to DB
        var savedRole = await Context.Roles.FindAsync(result.Id);
        Assert.NotNull(savedRole);
        Assert.Equal(dto.Name, savedRole.Name);
        Assert.False(savedRole.IsArchived);
        Assert.True(savedRole.LastUpdatedAt >= now);
        Assert.Equal(uid, savedRole.LastUpdatedBy);
        Assert.Null(savedRole.ProjectId);
        Assert.Equal(oid, savedRole.OrganizationId);
        Assert.Null(savedRole.Description);

        // Ensure that the Role create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(oid, actualEvent.OrganizationId);
        Assert.Null(actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateRole_Succeeds_WithProjectSupplied()
    {
        // Arrange
        var dto = new CreateRoleRequestDto
        {
            Name = "Project Role"
        };

        // Act - we must provide ab organizationId even though this is a project Role
        var result = await _roleBusiness.CreateRole(uid, dto, oid, pid);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Project Role", result.Name);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(oid, result.OrganizationId);

        // Verify it was actually saved to DB
        var savedRole = await Context.Roles.FindAsync(result.Id);
        Assert.NotNull(savedRole);
        Assert.Equal("Project Role", savedRole.Name);

        // Ensure that the Role create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal(oid, actualEvent.OrganizationId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateRole_Success_CreatesEvent()
    {
        // Arrange
        var dto = new CreateRoleRequestDto
        {
            Name = "Event Role"
        };

        // Act
        var result = await _roleBusiness.CreateRole(uid, dto, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Role", result.Name);

        // Ensure that the Role create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(oid, actualEvent.OrganizationId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateRole_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateRoleRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _roleBusiness.CreateRole(uid, dto, oid));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRole_Fails_IfNullDto()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _roleBusiness.CreateRole(uid, null, oid));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRole_Fails_WhenNameConflictsInSameScope()
    {
        // Arrange - "Role 1" already exists as org-level role
        var dto = new CreateRoleRequestDto { Name = "Role 1", Description = "Duplicate" };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => _roleBusiness.CreateRole(uid, dto, oid));

        Assert.Contains("already exists", exception.Message);
    }

    #endregion

    #region BulkCreateRoles Tests

    [Fact]
    public async Task BulkCreateRoles_Success_CreatesMultipleProjectRoles()
    {
        // Arrange
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Project Role 1", Description = "Description 1" },
            new() { Name = "Project Role 2", Description = "Description 2" }
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, pid, bulkDto);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(uid, r.LastUpdatedBy));

        var firstRole = result[0];
        Assert.Equal("Project Role 1", firstRole.Name);
        Assert.Equal("Description 1", firstRole.Description);
        Assert.Equal(oid, firstRole.OrganizationId);
        Assert.Equal(pid, firstRole.ProjectId);
        Assert.NotEqual(0, firstRole.Id);

        var secondRole = result[1];
        Assert.Equal("Project Role 2", secondRole.Name);
        Assert.Equal("Description 2", secondRole.Description);
        Assert.Equal(oid, secondRole.OrganizationId);
        Assert.Equal(pid, secondRole.ProjectId);
        Assert.NotEqual(0, secondRole.Id);
        Assert.NotEqual(firstRole.Id, secondRole.Id);

        // Verify event was logged
        var events = await Context.Events.ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_CreatesMultipleOrgLevelRoles()
    {
        // Arrange
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Org Role 1", Description = "Org Description 1" },
            new() { Name = "Org Role 2", Description = "Org Description 2" }
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, null, bulkDto);

        // Assert
        Assert.Equal(2, result.Count);

        var firstRole = result[0];
        Assert.Equal("Org Role 1", firstRole.Name);
        Assert.Equal("Org Description 1", firstRole.Description);
        Assert.Equal(oid, firstRole.OrganizationId);
        Assert.Null(firstRole.ProjectId);
        Assert.NotEqual(0, firstRole.Id);

        var secondRole = result[1];
        Assert.Equal("Org Role 2", secondRole.Name);
        Assert.Equal("Org Description 2", secondRole.Description);
        Assert.Equal(oid, secondRole.OrganizationId);
        Assert.Null(secondRole.ProjectId);
        Assert.NotEqual(0, secondRole.Id);
        
        var events = await Context.Events.ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_ProjectRole_OnNameCollision_UpdatesDescription()
    {
        // Arrange - Use seeded role4 which is a project role
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new()
            {
                Name = "Role 4", // Matches seeded role4
                Description = "Updated Description"
            }
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, pid, bulkDto);

        // Assert
        Assert.Single(result);
        Assert.Equal(rid4, result.First().Id); // Should be same ID as seeded role4
        Assert.Equal("Role 4", result.First().Name);
        Assert.Equal("Updated Description", result.First().Description);
        Assert.Equal(oid, result.First().OrganizationId);
        Assert.Equal(pid, result.First().ProjectId);

        // Ensure create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
        Assert.Equal("create", eventList[0].Operation);
        Assert.Equal("role", eventList[0].EntityType);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_OrgRole_OnNameCollision_UpdatesDescription()
    {
        // Arrange - Use seeded role1 which is an org-level role
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new()
            {
                Name = "Role 1", // Matches seeded role1
                Description = "Updated Org Description"
            }
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, null, bulkDto);

        // Assert
        Assert.Single(result);
        Assert.Equal(rid1, result.First().Id); // Should be same ID as seeded role1
        Assert.Equal("Role 1", result.First().Name);
        Assert.Equal("Updated Org Description", result.First().Description);
        Assert.Equal(oid, result.First().OrganizationId);
        Assert.Null(result.First().ProjectId);

        // No events for org-level roles
        var events = await Context.Events.ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_AllowsSameNameAcrossOrgAndProjectScopes()
    {
        // Arrange - Create org-level "Admin" role
        var orgRole = new List<CreateRoleRequestDto>
        {
            new() { Name = "Admin", Description = "Org Admin" }
        };
        var orgResult = await _roleBusiness.BulkCreateRoles(uid, oid, null, orgRole);

        // Act - Create project-level "Admin" role with same name
        var projectRole = new List<CreateRoleRequestDto>
        {
            new() { Name = "Admin", Description = "Project Admin" }
        };
        var projectResult = await _roleBusiness.BulkCreateRoles(uid, oid, pid, projectRole);

        // Assert - Both should exist with same name but different scopes
        Assert.Single(orgResult);
        Assert.Single(projectResult);
        Assert.NotEqual(orgResult.First().Id, projectResult.First().Id);

        Assert.Equal("Admin", orgResult.First().Name);
        Assert.Null(orgResult.First().ProjectId);
        Assert.Equal("Org Admin", orgResult.First().Description);

        Assert.Equal("Admin", projectResult.First().Name);
        Assert.Equal(pid, projectResult.First().ProjectId);
        Assert.Equal("Project Admin", projectResult.First().Description);

        // Verify roles exist in database (4 seeded (1 is deleted) + 2 new = 6 total)
        var allRoles = await Context.Roles.ToListAsync();
        Assert.Equal(6, allRoles.Count);
    }

    [Fact]
    public async Task BulkCreateRoles_Fails_IfOrganizationDoesNotExist()
    {
        // Arrange
        var nonExistentOrgId = 99999L;
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Test Role" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.BulkCreateRoles(uid, nonExistentOrgId, pid, bulkDto));
    }

    [Fact]
    public async Task BulkCreateRoles_Fails_IfProjectDoesNotExist()
    {
        // Arrange
        var nonExistentProjectId = 99999L;
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Test Role" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.BulkCreateRoles(uid, oid, nonExistentProjectId, bulkDto));
    }

    [Fact]
    public async Task BulkCreateRoles_Fails_IfProjectDoesNotBelongToOrganization()
    {
        // Arrange - Create a second organization and project
        var otherOrg = new Organization
        {
            Name = "Other Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(otherOrg);
        await Context.SaveChangesAsync();

        var otherProject = new Project
        {
            Name = "Other Project",
            OrganizationId = otherOrg.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Projects.Add(otherProject);
        await Context.SaveChangesAsync();

        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Test Role" }
        };

        // Act & Assert - Try to create role with oid but otherProject.Id
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _roleBusiness.BulkCreateRoles(uid, oid, otherProject.Id, bulkDto));

        Assert.Contains("does not belong to organization", exception.Message);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_OnNameCollision_KeepsOriginalDescriptionIfNewIsNull()
    {
        // Arrange - Use seeded role2, add a description first
        var role2 = await Context.Roles.FindAsync(rid2);
        role2!.Description = "Original Description";
        await Context.SaveChangesAsync();

        var bulkDto = new List<CreateRoleRequestDto>
        {
            new()
            {
                Name = "Role 2", // Matches seeded role3
                Description = null // No new description
            }
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, null, bulkDto);

        // Assert
        Assert.Single(result);
        Assert.Equal(rid2, result.First().Id);
        Assert.Equal("Role 2", result.First().Name);
        Assert.Equal("Original Description", result.First().Description); // Should keep original
    }

    [Fact]
    public async Task BulkCreateRoles_Success_CreatesMixOfNewAndUpdatedRoles()
    {
        // Arrange - Bulk create: update seeded role1 + create new
        var bulkDto = new List<CreateRoleRequestDto>
        {
            new() { Name = "Role 1", Description = "Updated" }, // Updates seeded role1
            new() { Name = "New Role", Description = "Brand New" } // New role
        };

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, null, bulkDto);

        // Assert
        Assert.Equal(2, result.Count);

        var updatedRole = result.First(r => r.Name == "Role 1");
        Assert.Equal(rid1, updatedRole.Id); // Should be seeded role1's ID
        Assert.Equal("Updated", updatedRole.Description);

        var newRole = result.First(r => r.Name == "New Role");
        Assert.NotEqual(rid1, newRole.Id);
        Assert.Equal("Brand New", newRole.Description);

        // Verify total roles in DB (5 seeded + 1 new = 6)
        var allRoles = await Context.Roles.ToListAsync();
        Assert.Equal(5, allRoles.Count);
    }

    [Fact]
    public async Task BulkCreateRoles_Success_HandlesEmptyList()
    {
        // Arrange
        var emptyList = new List<CreateRoleRequestDto>();

        // Act
        var result = await _roleBusiness.BulkCreateRoles(uid, oid, pid, emptyList);

        // Assert
        Assert.Empty(result);

        var events = await Context.Events.ToListAsync();
        Assert.Empty(events);

        // Verify seeded roles are still there (4 total)
        var allRoles = await Context.Roles.ToListAsync();
        Assert.Equal(4, allRoles.Count);
    }

    #endregion

    #region GetAllRole Tests

    [Fact]
    public async Task GetAllRoles_FiltersByProjectAndOrg()
    {
        // Act
        var result = (await _roleBusiness.GetAllRoles(oid, pid)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains(result, r => r.Id == rid4);
    }

    [Fact]
    public async Task GetAllRoles_FiltersByProject()
    {
        // Act - Get project roles without specifying org
        var result = (await _roleBusiness.GetAllRoles(oid, pid)).ToList();

        // Assert
        Assert.Single(result);
        Assert.All(result, r => Assert.Equal(false, r.IsArchived));
        Assert.DoesNotContain(result, r => r.Id == rid1);
        Assert.DoesNotContain(result, r => r.Id == rid2);
        Assert.DoesNotContain(result, r => r.Id == rid3);
        Assert.Contains(result, r => r.Id == rid4);
        Assert.DoesNotContain(result, r => r.Id == rid5);

        // Act - Get same project roles WITH org specified (should be identical)
        var resultWithOrg = (await _roleBusiness.GetAllRoles(oid, pid)).ToList();

        // Assert - Should return same results
        Assert.Single(resultWithOrg);
        Assert.All(resultWithOrg, r => Assert.Equal(false, r.IsArchived));
        Assert.DoesNotContain(resultWithOrg, r => r.Id == rid1);
        Assert.DoesNotContain(resultWithOrg, r => r.Id == rid2);
        Assert.DoesNotContain(resultWithOrg, r => r.Id == rid3);
        Assert.Contains(resultWithOrg, r => r.Id == rid4);
        Assert.DoesNotContain(resultWithOrg, r => r.Id == rid5);

        // Both results should be identical
        Assert.Equal(result.Count, resultWithOrg.Count);
        Assert.Equal(result.First().Id, resultWithOrg.First().Id);
    }

    [Fact]
    public async Task GetAllRoles_FiltersByOrganization()
    {
        // Act
        var result = (await _roleBusiness.GetAllRoles(oid, null)).ToList();

        // Assert
        Assert.All(result, r => Assert.Equal(false, r.IsArchived));
        Assert.Contains(result, r => r.Id == rid1);
    }

    #endregion

    #region GetRole Tests

    [Fact]
    public async Task GetRole_Succeeds_WhenExists()
    {
        // Act
        var result = await _roleBusiness.GetRole(rid1, oid, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid1, result.Id);
        Assert.Equal("Role 1", result.Name);
        Assert.False(result.IsArchived);
    }


    [Fact]
    public async Task GetRole_Succeeds_WhenExistsWithProj()
    {
        // Act
        var result = await _roleBusiness.GetRole(rid4, oid, pid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid4, result.Id);
        Assert.Equal("Role 4", result.Name);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetRole_Succeeds_IfArchived_AndHideArchivedFalse()
    {
        // Act
        var result = await _roleBusiness.GetRole(rid2, oid, null, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Role 2", result.Name);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task GetRole_Fails_IfArchived_AndHideArchivedTrue()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.GetRole(rid2, oid, pid));

        Assert.Contains(
            $"Role with id {rid2} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    [Fact]
    public async Task GetRole_Fails_IfDeletedRole()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.GetRole(rid3, oid, pid));

        Assert.Contains(
            $"Role with id {rid3} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    [Fact]
    public async Task GetRole_Fails_IfNotValidProject()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.GetRole(rid4, oid, pid2));

        Assert.Contains(
            $"Role with id {rid4} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region UpdateRole Tests

    [Fact]
    public async Task UpdateRole_Success_ReturnsRole()
    {
        // Arrange
        var dto = new UpdateRoleRequestDto
        {
            Name = "Updated Role",
            Description = "Now with a description"
        };

        // Act
        var result = await _roleBusiness.UpdateRole(uid, rid1, oid, null, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid1, result.Id);
        Assert.Equal("Updated Role", result.Name);
        Assert.Equal("Now with a description", result.Description);

        // Verify it was actually saved to DB
        var savedRole = await Context.Roles.FindAsync(rid1);
        Assert.NotNull(savedRole);
        Assert.Equal("Updated Role", savedRole.Name);
        Assert.Equal("Now with a description", savedRole.Description);

        // Ensure that the Role update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateRole_Success_CreatesEvent()
    {
        // Arrange
        var dto = new UpdateRoleRequestDto
        {
            Name = "Updated Role",
            Description = "Now with a description"
        };

        // Act
        var result = await _roleBusiness.UpdateRole(uid, rid1, oid, null, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Role", result.Name);

        // Ensure that the Role update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateRole_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateRoleRequestDto
        {
            Name = "Updated Role",
            Description = "Now with a description"
        };

        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.UpdateRole(uid, rid3, oid, pid, dto));

        // Assert
        Assert.Contains($"Role with id {rid3} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateRole_Fails_WhenNewNameConflictsInSameScope()
    {
        // Arrange - Try to rename rid1 to "Role 2" (which already exists)
        var dto = new UpdateRoleRequestDto { Name = "Role 2" };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _roleBusiness.UpdateRole(uid, rid1, oid, null, dto));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task UpdateRole_Fails_WhenWrongProjectSupplied()
    {
        // Arrange - Try to rename rid1 to "Role 2" (which already exists)
        var dto = new UpdateRoleRequestDto { Name = "Role 2" };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.UpdateRole(uid, rid1, oid, pid2, dto));

        Assert.Contains(
            $"Role with id {rid1} not found or does not belong to the specified organization/project context",
            exception.Message);
    }


    [Fact]
    public async Task UpdateRole_Fails_WhenWrongOrgSupplied()
    {
        // Arrange
        var organization = new Organization
        {
            Name = "Test 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();

        var dto = new UpdateRoleRequestDto { Name = "Role 2" };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.UpdateRole(uid, rid1, organization.Id, pid2, dto));

        Assert.Contains(
            $"Role with id {rid1} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    [Fact]
    public async Task UpdateRole_Success_AllowsSameNameInDifferentScope()
    {
        // Arrange - rid1 is org-level, rid4 is project-level
        // Rename rid4 to "Role 1" (same name as rid1, but different scope)
        var dto = new UpdateRoleRequestDto { Name = "Role 1" };

        // Act
        var result = await _roleBusiness.UpdateRole(uid, rid4, oid, pid, dto);

        // Assert
        Assert.Equal("Role 1", result.Name);
        Assert.Equal(pid, result.ProjectId); // Still project-level
    }

    #endregion

    #region ArchiveRole Tests

    [Fact]
    public async Task ArchiveRole_Succeeds_IfNotArchived()
    {
        // Arrange
        var originalRole = await Context.Roles.FindAsync(rid1);

        // Act
        var result = await _roleBusiness.ArchiveRole(uid, rid1, oid, null);

        // Assert
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // Verify it was actually saved to DB
        var savedRole = await Context.Roles.FindAsync(rid1);
        Assert.NotNull(savedRole);
        Assert.True(savedRole.IsArchived);
        Assert.Equal(originalRole.Name, savedRole.Name);
        Assert.True(originalRole.LastUpdatedAt <= savedRole.LastUpdatedAt);
        Assert.Equal(uid, savedRole.LastUpdatedBy);
        Assert.Equal(originalRole.ProjectId, savedRole.ProjectId);
        Assert.Equal(originalRole.OrganizationId, savedRole.OrganizationId);
        Assert.Equal(originalRole.Description, savedRole.Description);


        // Ensure that the Role archive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(rid1, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveRole_RemovesRole_FromProjectMembers()
    {
        // Confirm that user exists as project member with role
        var member = await Context.ProjectMembers.FindAsync(mid);
        Assert.NotNull(member);
        Assert.Equal(pid, member.ProjectId);
        Assert.Equal(uid, member.UserId);
        Assert.Equal(rid4, member.RoleId);

        // Act
        var result = await _roleBusiness.ArchiveRole(uid, rid4, oid, pid);

        // Assert
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // Confirm that user no longer holds role
        var updatedMember = await Context.ProjectMembers.FindAsync(mid);
        Assert.NotNull(updatedMember);
        Assert.Equal(pid, updatedMember.ProjectId);
        Assert.Equal(uid, updatedMember.UserId);
        Assert.NotEqual(rid4, updatedMember.RoleId);
        Assert.Null(updatedMember.RoleId);

        // Ensure that the Role archive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(rid4, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveRole_Fails_IfArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.ArchiveRole(uid, rid2, oid, null));

        Assert.Contains(
            $"Role with id {rid2} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveRole_Fails_IfWrongProjectSupplied()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.ArchiveRole(uid, rid1, oid, pid2));

        Assert.Contains(
            $"Role with id {rid1} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveRole Tests

    [Fact]
    public async Task UnarchiveRole_Succeeds_IfArchived()
    {
        // Arrange
        var originalRole = await Context.Roles.FindAsync(rid2);

        // Act
        var result = await _roleBusiness.UnarchiveRole(uid, rid2, oid, null);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedRole = await Context.Roles.FindAsync(rid2);
        Assert.NotNull(savedRole);
        Assert.False(savedRole.IsArchived);
        Assert.Equal(originalRole.Name, savedRole.Name);
        Assert.True(originalRole.LastUpdatedAt <= savedRole.LastUpdatedAt);
        Assert.Equal(uid, savedRole.LastUpdatedBy);
        Assert.Equal(originalRole.ProjectId, savedRole.ProjectId);
        Assert.Equal(originalRole.OrganizationId, savedRole.OrganizationId);
        Assert.Equal(originalRole.Description, savedRole.Description);

        // Ensure that the Role unarchive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("unarchive", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(rid2, actualEvent.EntityId);
    }

    [Fact]
    public async Task UnarchiveRole_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.UnarchiveRole(uid, rid1, oid, null));

        Assert.Contains(
            $"Role with id {rid1} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveRole_Fails_IfWrongProjectSupplied()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.UnarchiveRole(uid, rid2, oid, pid2));

        Assert.Contains(
            $"Role with id {rid2} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteRole Tests

    [Fact]
    public async Task DeleteRole_Succeeds_WhenExists()
    {
        // Act
        var result = await _roleBusiness.DeleteRole(uid, rid1, oid, null);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedRole = await Context.Roles.FindAsync(rid1);
        Assert.Null(deletedRole);

        // Ensure that the Role delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("delete", actualEvent.Operation);
        Assert.Equal("role", actualEvent.EntityType);
        Assert.Equal(rid1, actualEvent.EntityId);
    }

    [Fact]
    public async Task DeleteRole_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.DeleteRole(uid, rid3, oid, pid));

        Assert.Contains(
            $"Role with id {rid3} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task DeleteRole_Fails_IfWrongProject()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.DeleteRole(uid, rid4, oid, pid2));

        Assert.Contains(
            $"Role with id {rid4} not found or does not belong to the specified organization/project context",
            exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region GetPermissionsByRole Tests

    [Fact]
    public async Task GetPermissionsByRole_Lists_AllPermissionsForRole()
    {
        // Act
        var result = (await _roleBusiness.GetPermissionsByRole(rid1, oid, null)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == permid1);
        Assert.Contains(result, p => p.Id == permid2);
    }

    [Fact]
    public async Task GetPermissionsByRole_DoesNotList_PermissionsNotForRole()
    {
        // Act
        var result = (await _roleBusiness.GetPermissionsByRole(rid1, oid, null)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == permid1);
        Assert.Contains(result, p => p.Id == permid2);
        Assert.DoesNotContain(result, p => p.Id == permid3);
    }

    [Fact]
    public async Task GetPermissionsByRole_Fails_IfRoleNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.GetPermissionsByRole(rid3, oid, pid));

        Assert.Contains($"Role with id {rid3} not found", exception.Message);
    }

    [Fact]
    public async Task GetPermissionsByRole_ReturnsEmpty_IfNoPermissionsForRole()
    {
        // Act
        var result = await _roleBusiness.GetPermissionsByRole(rid4, oid, pid);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPermissionsByRole_Fails_IfWrongProjectSupplied()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _roleBusiness.GetPermissionsByRole(rid4, oid, pid2));

        Assert.Contains(
            $"Role with id {rid4} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region AddPermissionToRole Tests

    [Fact]
    public async Task AddPermissionToRole_AddsPermissionToRole()
    {
        // Act
        var result = await _roleBusiness.AddPermissionToRole(rid1, permid3, oid, null);

        // Assert
        Assert.True(result);

        // Verify permission was added
        var role = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Contains(role.Permissions, p => p.Id == permid3);
    }

    [Fact]
    public async Task AddPermissionToRole_Fails_IfRoleNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.AddPermissionToRole(rid3, permid3, oid, pid));

        Assert.Contains($"Role with id {rid3} not found", exception.Message);
    }

    [Fact]
    public async Task AddPermissionToRole_Fails_IfPermissionNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.AddPermissionToRole(rid1, permid4, oid, null));

        Assert.Contains($"Permission with id {permid4} not found", exception.Message);
    }

    [Fact]
    public async Task AddPermissionToRole_Fails_IfPermissionExistsForRole()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _roleBusiness.AddPermissionToRole(rid1, permid2, oid, null));

        Assert.Contains($"Permission with id {permid2} already exists as part of role {rid1}", exception.Message);
    }

    [Fact]
    public async Task AddPermissionToRole_Fails_IfWrongProjectSupplied()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.AddPermissionToRole(rid4, permid2, oid, pid2));

        Assert.Contains(
            $"Role with id {rid4} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region RemovePermissionFromRole Tests

    [Fact]
    public async Task RemovePermissionFromRole_RemovesPermissionFromRole()
    {
        // Act
        var result = await _roleBusiness.RemovePermissionFromRole(rid1, permid1, oid, null);

        // Assert
        Assert.True(result);

        // Verify permission was removed
        var updatedRole = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.DoesNotContain(updatedRole.Permissions, p => p.Id == permid1);
    }

    [Fact]
    public async Task RemovePermissionFromRole_Fails_IfRoleNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.RemovePermissionFromRole(rid3, permid3, oid, pid));

        // Assert
        Assert.Contains($"Role with id {rid3} not found", exception.Message);
    }

    [Fact]
    public async Task RemovePermissionFromRole_Fails_IfPermissionNotExistsForRole()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.RemovePermissionFromRole(rid1, permid3, oid, null));

        Assert.Contains($"Permission with id {permid3} is not assigned to role {rid1}", exception.Message);
    }

    [Fact]
    public async Task RemovePermissionFromRole_Fails_IfWrongProjectSupplied()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _roleBusiness.RemovePermissionFromRole(rid5, permid3, oid, pid));

        Assert.Contains(
            $"Role with id {rid5} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region SetPermissionsForRole Tests

    [Fact]
    public async Task SetPermissionsForRole_SetsPermissionsForEmptyRole()
    {
        // Arrange
        var permissionIds = new[] { permid1, permid2 };

        // Act
        var result = await _roleBusiness.SetPermissionsForRole(rid4, permissionIds, oid, pid);

        // Assert
        Assert.True(result);

        // Verify permissions were set
        var role = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Equal(2, role.Permissions.Count);
        Assert.Contains(role.Permissions, p => p.Id == permid1);
        Assert.Contains(role.Permissions, p => p.Id == permid2);
    }

    [Fact]
    public async Task SetPermissionsForRole_ResetsPermissionsIfAnyExist()
    {
        // Check existing permissions for role 1 to ensure perms 1 and 2 exist
        var roleBefore = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Equal(2, roleBefore.Permissions.Count);
        Assert.Contains(roleBefore.Permissions, p => p.Id == permid1);
        Assert.Contains(roleBefore.Permissions, p => p.Id == permid2);

        // Arrange
        var permissionIds = new[] { permid1, permid3 };

        // Act
        var result = await _roleBusiness.SetPermissionsForRole(rid1, permissionIds, oid, null);

        // Assert
        Assert.True(result);
        var roleAfter = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Equal(2, roleAfter.Permissions.Count);
        Assert.Contains(roleAfter.Permissions, p => p.Id == permid1);
        Assert.DoesNotContain(roleAfter.Permissions, p => p.Id == permid2);
        Assert.Contains(roleAfter.Permissions, p => p.Id == permid3);
    }

    [Fact]
    public async Task SetPermissionsForRole_SetsPermissionsBlank_IfNoneSupplied()
    {
        // Check existing permissions for role 1 to ensure perms 1 and 2 exist
        var roleBefore = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Equal(2, roleBefore.Permissions.Count);
        Assert.Contains(roleBefore.Permissions, p => p.Id == permid1);
        Assert.Contains(roleBefore.Permissions, p => p.Id == permid2);

        // Arrange
        var emptyPermissionIds = new long[] { };

        // Act
        var result = await _roleBusiness.SetPermissionsForRole(rid1, emptyPermissionIds, oid, null);

        // Assert
        Assert.True(result);
        var roleAfter = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == rid1);
        Assert.Empty(roleAfter.Permissions);
    }

    [Fact]
    public async Task SetPermissionsForRole_Fails_IfRoleNotFound()
    {
        // Arrange
        var permissionIds = new[] { permid1, permid2 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.SetPermissionsForRole(rid3, permissionIds, oid, pid));

        Assert.Contains($"Role with id {rid3} not found", exception.Message);
    }

    [Fact]
    public async Task SetPermissionsForRole_Fails_IfAnyPermissionNotFound()
    {
        // Arrange
        var permissionIds = new[] { permid4 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.SetPermissionsForRole(rid1, permissionIds, oid, null));

        Assert.Contains($"Permissions not found: {string.Join(", ", permissionIds)}", exception.Message);
    }

    [Fact]
    public async Task SetPermissionsForRole_Fails_IfWrongProjectSupplied()
    {
        // Arrange
        var permissionIds = new[] { permid3 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.SetPermissionsForRole(rid5, permissionIds, oid, pid));

        Assert.Contains(
            $"Role with id {rid5} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region SetPermissionsByPattern Tests

    [Fact]
    public async Task SetPermissionsByPattern_Success_SetsPermissions()
    {
        // Arrange
        var permissionPatterns = new Dictionary<string, string[]>
        {
            { "test", new[] { "read", "write" } },
            { "test2", new[] { "execute" } }
        };

        var testRoleId = rid4;

        // Act
        var result = await _roleBusiness.SetPermissionsByPattern(testRoleId, permissionPatterns, oid, pid);

        // Assert
        Assert.True(result);

        // Verify permissions were set correctly
        var role = await Context.Roles.Include(r => r.Permissions).FirstAsync(r => r.Id == testRoleId);

        // Get resources to query
        var resources = permissionPatterns.Keys.ToList();

        // Fetch all permissions for those resources, then filter in memory
        var allPermissionsForResources = await Context.Permissions
            .Where(p => resources.Contains(p.Resource))
            .ToListAsync();

        // Filter in memory to get expected permissions
        var expectedPermissions = allPermissionsForResources
            .Where(p => permissionPatterns.ContainsKey(p.Resource) &&
                        permissionPatterns[p.Resource].Contains(p.Action))
            .ToList();

        Assert.Equal(expectedPermissions.Count, role.Permissions.Count);

        foreach (var expectedPerm in expectedPermissions)
            Assert.Contains(role.Permissions, p => p.Id == expectedPerm.Id);
    }

    [Fact]
    public async Task SetPermissionsByPattern_Fails_IfRoleNotFound()
    {
        // Arrange
        var permissionPatterns = new Dictionary<string, string[]>
        {
            { "test", new[] { "read" } }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.SetPermissionsByPattern(rid3, permissionPatterns, oid, pid));

        Assert.Contains($"Role with id {rid3} not found", exception.Message);
    }

    [Fact]
    public async Task SetPermissionsByPattern_Fails_IfWrongProjectSupplied()
    {
        // Arrange
        var permissionPatterns = new Dictionary<string, string[]>
        {
            { "test", new[] { "read" } }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleBusiness.SetPermissionsByPattern(rid4, permissionPatterns, oid, pid2));

        Assert.Contains(
            $"Role with id {rid4} not found or does not belong to the specified organization/project context",
            exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateRole_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testRole = new Role
        {
            OrganizationId = oid,
            Name = "Test Role LastUpdatedBy",
            Description = "Test description",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Act
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        // Assert
        var savedRole = await Context.Roles.FindAsync(testRole.Id);
        Assert.NotNull(savedRole);
        Assert.Equal(uid, savedRole.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRole_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testRole = new Role
        {
            OrganizationId = oid,
            Name = "Test Role Navigation",
            Description = "Test description 2",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        // Act
        var roleWithUser = await Context.Roles
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRole.Id);

        // Assert
        Assert.NotNull(roleWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", roleWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test@test.com", roleWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, roleWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRole_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testRole = new Role
        {
            OrganizationId = oid,
            Name = "Test Role Null",
            Description = "Test description 3",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        // Assert
        var savedRole = await Context.Roles.FindAsync(testRole.Id);
        Assert.NotNull(savedRole);
        Assert.Null(savedRole.LastUpdatedBy);

        var roleWithUser = await Context.Roles
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRole.Id);

        Assert.Null(roleWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateRole_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testRole = new Role
        {
            OrganizationId = oid,
            Name = "Test Role Update",
            Description = "Test description 4",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        // Act
        testRole.LastUpdatedBy = uid;
        testRole.Name = "Updated Role Name";
        testRole.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Roles.Update(testRole);
        await Context.SaveChangesAsync();

        // Assert
        var updatedRole = await Context.Roles
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRole.Id);

        Assert.Equal(uid, updatedRole.LastUpdatedBy);
        Assert.NotNull(updatedRole.LastUpdatedByUser);
        Assert.Equal("Test User", updatedRole.LastUpdatedByUser.Name);
        Assert.Equal("Updated Role Name", updatedRole.Name);
    }

    #endregion
}