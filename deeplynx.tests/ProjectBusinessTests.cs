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
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class ProjectBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private Mock<IEdgeBusiness> _mockEdgeBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<IRecordBusiness> _mockRecordBusiness = null!;
    private Mock<IRelationshipBusiness> _mockRelationshipBusiness = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private Mock<IObjectStorageBusiness> _objectStorageBusiness = null!;
    private Mock<IOrganizationBusiness> _organizationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    private RoleBusiness _roleBusiness = null!;
    private long cid; // class ID
    private long did; // datasource ID
    private long gid; // group ID
    private long gid2;
    private long oid; // org IDs
    private long oid2;

    private long pid; // project ID
    private long pid2;
    private long pid3;
    private long pid4;
    private long pid5;
    private long rid; // role ID
    private long rid2;
    private long uid; // user ID
    private long uid2;
    private long uid3;

    public ProjectBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Environment.SetEnvironmentVariable("FILE_STORAGE_METHOD", "filesystem");
        Environment.SetEnvironmentVariable("STORAGE_DIRECTORY", "./storage/");
        Environment.SetEnvironmentVariable("AZURE_OBJECT_CONNECTION_STRING", "azure-example-connection-string");
        Environment.SetEnvironmentVariable("AWS_S3_CONNECTION_STRING", "aws-example-connection-string");
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _objectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _mockRecordBusiness = new Mock<IRecordBusiness>();
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockEdgeBusiness = new Mock<IEdgeBusiness>();
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _organizationBusiness = new Mock<IOrganizationBusiness>();

        _roleBusiness = new RoleBusiness(Context, _eventBusiness);
        _dataSourceBusiness = new DataSourceBusiness(
            Context, _mockEdgeBusiness.Object,
            _mockRecordBusiness.Object, _eventBusiness);
        _classBusiness = new ClassBusiness(
            Context, _mockRecordBusiness.Object,
            _mockRelationshipBusiness.Object, _eventBusiness);
        _projectBusiness = new ProjectBusiness(
            Context, _mockLogger.Object,
            _classBusiness, _roleBusiness, _dataSourceBusiness,
            _objectStorageBusiness.Object, _eventBusiness, _organizationBusiness.Object);
    }

    #region GetProjectStats Tests

    [Fact]
    public async Task GetProjectStats_Success_ReturnsCorrectCounts()
    {
        // Act
        var result = await _projectBusiness.GetProjectStats(oid, pid);

        // Assert
        Assert.Equal(1, result.classes);
        Assert.Equal(1, result.records);
        Assert.Equal(1, result.datasources);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Add org
        var testOrg = new Organization { Name = "Test Org" };
        var deletedOrg = new Organization { Name = "Delete Me" };
        Context.Organizations.AddRange(testOrg, deletedOrg);
        await Context.SaveChangesAsync();
        oid = testOrg.Id;
        oid2 = deletedOrg.Id;

        // delete org 2
        Context.Organizations.Remove(deletedOrg);
        await Context.SaveChangesAsync();

        // Add projects
        var testProj = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        var testProj2 = new Project
        {
            Name = "Other Project",
            Description = "Secondary project for unit tests",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        var loneProj = new Project
        {
            Name = "Lone Project",
            Description = "Project with just the lonely user",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        var arcProj = new Project
        {
            Name = "Archived Project",
            Description = "Archived project for unit tests",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = true,
            OrganizationId = oid
        };
        var groupProj = new Project
        {
            Name = "Other Project 2",
            Description = "Another secondary project for unit tests",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        Context.Projects.AddRange(testProj, testProj2, loneProj, arcProj, groupProj);
        await Context.SaveChangesAsync();
        pid = testProj.Id;
        pid2 = testProj2.Id;
        pid3 = loneProj.Id;
        pid4 = arcProj.Id;
        pid5 = groupProj.Id;

        // Add classes
        var testClass = new Class
        {
            Name = "Test Class",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        // Add datasource
        var testDataSource = new DataSource
        {
            Name = "Test DataSource",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(testDataSource);
        await Context.SaveChangesAsync();
        did = testDataSource.Id;

        // Add record
        var testRecord = new Record
        {
            Name = "Test Record",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = did,
            ClassId = cid,
            Properties = "{}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OriginalId = "test-original-1",
            Description = "Test record for unit tests",
            IsArchived = false
        };
        Context.Records.Add(testRecord);
        await Context.SaveChangesAsync();

        // Add users
        var testUser = new User
        {
            Email = "test@example.com",
            Name = "Test User",
            IsSysAdmin = false
        };
        var missingUser = new User
        {
            Email = "ope@example.com",
            Name = "Missing User",
            IsSysAdmin = false
        };
        var lonelyUser = new User
        {
            Email = "lonely@example.com",
            Name = "Lonely User",
            IsSysAdmin = false
        };
        Context.Users.AddRange(testUser, missingUser, lonelyUser);
        await Context.SaveChangesAsync();
        uid = testUser.Id;
        uid2 = missingUser.Id;
        uid3 = lonelyUser.Id;
        Context.Users.Remove(missingUser);
        await Context.SaveChangesAsync();

        // Add test roles
        var testRole = new Role { Name = "Test Role", OrganizationId = oid };
        var missingRole = new Role { Name = "Missing Role", OrganizationId = oid };
        Context.Roles.AddRange(testRole, missingRole);
        await Context.SaveChangesAsync();
        rid = testRole.Id;
        rid2 = missingRole.Id;
        Context.Roles.Remove(missingRole);
        await Context.SaveChangesAsync();

        // Add groups
        var testGroup = new Group { Name = "Test Group", OrganizationId = oid };
        var missingGroup = new Group { Name = "Missing Group", OrganizationId = oid };
        Context.Groups.AddRange(testGroup, missingGroup);
        await Context.SaveChangesAsync();
        gid = testGroup.Id;
        gid2 = missingGroup.Id;
        Context.Groups.Remove(missingGroup);
        await Context.SaveChangesAsync();

        // Add project members
        var projectMembers = new List<ProjectMember>
        {
            new()
            {
                ProjectId = pid,
                UserId = uid,
                RoleId = rid
            },
            new()
            {
                ProjectId = pid2,
                UserId = uid,
                RoleId = rid
            },
            new()
            {
                ProjectId = pid3,
                UserId = uid3
            },
            new()
            {
                ProjectId = pid4,
                UserId = uid
            },
            new()
            {
                ProjectId = pid5,
                GroupId = gid,
                RoleId = rid
            },
            new()
            {
                ProjectId = pid2,
                GroupId = gid,
                RoleId = rid
            }
        };
        Context.ProjectMembers.AddRange(projectMembers);
        await Context.SaveChangesAsync();

        var permissions = new List<Permission>
        {
            new() { Resource = "project", Action = "read", Name = "Read Project", IsDefault = true },
            new() { Resource = "project", Action = "write", Name = "Write Project", IsDefault = true },
            new() { Resource = "object_storage", Action = "read", Name = "Read Object Storage", IsDefault = true },
            new() { Resource = "object_storage", Action = "write", Name = "Write Object Source", IsDefault = true },
            new() { Resource = "data_source", Action = "read", Name = "Read Data Source", IsDefault = true },
            new() { Resource = "data_source", Action = "write", Name = "Write Data Source", IsDefault = true },
            new() { Resource = "record", Action = "read", Name = "Read Record", IsDefault = true },
            new() { Resource = "record", Action = "write", Name = "Write Record", IsDefault = true },
            new() { Resource = "edge", Action = "read", Name = "Read Edge", IsDefault = true },
            new() { Resource = "edge", Action = "write", Name = "Write Edge", IsDefault = true },
            new() { Resource = "file", Action = "read", Name = "Read File", IsDefault = true },
            new() { Resource = "file", Action = "write", Name = "Write File", IsDefault = true },
            new() { Resource = "tag", Action = "read", Name = "Read Tag", IsDefault = true },
            new() { Resource = "tag", Action = "write", Name = "Write Tag", IsDefault = true },
            new() { Resource = "class", Action = "read", Name = "Read Class", IsDefault = true },
            new() { Resource = "class", Action = "write", Name = "Write Class", IsDefault = true },
            new() { Resource = "relationship", Action = "read", Name = "Read Relationship", IsDefault = true },
            new() { Resource = "relationship", Action = "write", Name = "Write Relationship", IsDefault = true },
            new() { Resource = "user", Action = "read", Name = "Read User", IsDefault = true },
            new() { Resource = "user", Action = "write", Name = "Write User", IsDefault = true },
            new() { Resource = "group", Action = "read", Name = "Read Group", IsDefault = true },
            new() { Resource = "group", Action = "write", Name = "Write Group", IsDefault = true },
            new() { Resource = "organization", Action = "read", Name = "Read Organization", IsDefault = true },
            new() { Resource = "organization", Action = "write", Name = "Write Organization", IsDefault = true },
            new() { Resource = "role", Action = "read", Name = "Read Role", IsDefault = true },
            new() { Resource = "role", Action = "write", Name = "Write Role", IsDefault = true },
            new() { Resource = "permission", Action = "read", Name = "Read Permission", IsDefault = true },
            new() { Resource = "permission", Action = "write", Name = "Write Permission", IsDefault = true },
            new()
            {
                Resource = "sensitivity_label", Action = "read", Name = "Read Sensitivity Label", IsDefault = true
            },
            new()
            {
                Resource = "sensitivity_label", Action = "write", Name = "Write Sensitivity Label", IsDefault = true
            }
        };

        Context.Permissions.AddRange(permissions);
        await Context.SaveChangesAsync();
    }

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

    #region CreateProject Tests

    [Fact]
    public async Task CreateProject_Success_ReturnsCorrectFields()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateProjectRequestDto
        {
            Name = $"Test Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Act
        var result = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(dto.Abbreviation, result.Abbreviation);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Ensure that the project create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(4, eventList.Count);
        Assert.Single(eventList, e =>
            e.ProjectId == result.Id &&
            e.Operation == "create" &&
            e.EntityType == "project" &&
            e.EntityId == result.Id
        );
    }

    [Fact]
    public async Task CreateProject_Success_CreatesDefaultRolesWithCorrectPermissions()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateProjectRequestDto
        {
            Name = $"Test Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Act
        var result = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(dto.Abbreviation, result.Abbreviation);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(uid, result.LastUpdatedBy);

        var defaultRoles = await Context.Roles.Where(r => r.ProjectId == result.Id).Include(r => r.Permissions)
            .ToListAsync();
        var adminRole = defaultRoles.Single(r => r.Name == "Admin");
        var userRole = defaultRoles.Single(r => r.Name == "User");

        AssertRolePermissions(adminRole, DefaultRolePermissions.Admin.AllowedPermissions);
        AssertRolePermissions(userRole, DefaultRolePermissions.User.AllowedPermissions);

        // Ensure that the project create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(4, eventList.Count);
        Assert.Single(eventList, e =>
            e.ProjectId == result.Id &&
            e.Operation == "create" &&
            e.EntityType == "project" &&
            e.EntityId == result.Id
        );
    }

    [Fact]
    public async Task CreateProject_Creates_DefaultClasses()
    {
        // Arrange
        var dto = new CreateProjectRequestDto
        {
            Name = $"Test Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Act
        var project = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.Equal(dto.Name, project.Name);
        var classResult = await _classBusiness.GetAllClasses(
            oid, [project.Id], true);

        Assert.Equal(3, classResult.Count);

        var names = classResult.Select(c => c.Name).ToList();

        Assert.Contains("File", names);
        Assert.Contains("Report", names);
        Assert.Contains("Timeseries", names);

        // Ensure that the project create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(4, eventList.Count);
        Assert.Single(eventList, e =>
            e.ProjectId == project.Id &&
            e.Operation == "create" &&
            e.EntityType == "project" &&
            e.EntityId == project.Id
        );
    }

    [Fact]
    public async Task CreateProject_Creates_DefaultDataSource()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateProjectRequestDto
        {
            Name = $"Test Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Act
        var project = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.Equal(dto.Name, project.Name);
        var dataSourceResult = await _dataSourceBusiness.GetAllDataSources(oid, new[] { project.Id });
        Assert.Single(dataSourceResult);
        Assert.Equal("Default Data Source", dataSourceResult[0].Name);
        Assert.Equal("This data source was created alongside the project for ease of use.",
            dataSourceResult[0].Description);

        // Ensure that the project create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(4, eventList.Count);
        Assert.Single(eventList, e =>
            e.ProjectId == project.Id &&
            e.Operation == "create" &&
            e.EntityType == "project" &&
            e.EntityId == project.Id
        );
    }

    [Fact]
    public async Task CreateProject_Creates_DefaultRoles_AndPermissions()
    {
        // Arrange
        var dto = new CreateProjectRequestDto
        {
            Name = $"Test Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Act
        var project = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.Equal(dto.Name, project.Name);

        // Verify default roles were created
        var roles = Context.Roles.Where(r => r.ProjectId == project.Id).Include(r => r.Permissions).ToList();
        Assert.Equal(2, roles.Count);
        var adminRole = roles.Single(r => r.Name == "Admin");
        var userRole = roles.Single(r => r.Name == "User");

        AssertRolePermissions(adminRole, DefaultRolePermissions.Admin.AllowedPermissions);
        AssertRolePermissions(userRole, DefaultRolePermissions.User.AllowedPermissions);
    }

    [Fact]
    public async Task CreateProject_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateProjectRequestDto { Name = null!, Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _projectBusiness.CreateProject(uid, oid, dto));

        // Ensure that no project create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateProject_Fails_IfNoUser()
    {
        // Arrange
        var dto = new CreateProjectRequestDto { Name = "Test", Description = "Test Description" };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _projectBusiness.CreateProject(uid2, oid, dto));

        Assert.Contains($"User with id {uid2} not found", exception.Message);
    }

    [Fact]
    public async Task CreateProject_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateProjectRequestDto { Name = "", Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _projectBusiness.CreateProject(uid, oid, dto));

        // Ensure that no project create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateProject_Success_IfOrgIdSupplied()
    {
        // Arrange
        var dto = new CreateProjectRequestDto
        {
            Name = "Idaho Test",
            Description = "Test Description"
        };

        // Act
        var result = await _projectBusiness.CreateProject(uid, oid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(oid, result.OrganizationId);
        var persisted = await Context.Projects.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(oid, persisted.OrganizationId);

        //Ensure
        _organizationBusiness.Verify(x => x.GetAllOrganizations(It.IsAny<bool>()), Times.Never);
        _organizationBusiness.Verify(
            x => x.CreateOrganization(uid, It.IsAny<CreateOrganizationRequestDto>(), It.IsAny<bool>()),
            Times.Never);
        _organizationBusiness.Verify(
            x => x.AddUserToOrganization(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()),
            Times.Never);
    }

    #endregion

    #region GetAllProjects Tests

    [Fact]
    public async Task GetAllProjects_ReturnsProjectsForUser()
    {
        // Act
        var listForTestUser = (await _projectBusiness.GetAllProjects(uid, oid)).ToList();
        var listForLonely = (await _projectBusiness.GetAllProjects(uid3, oid)).ToList();

        // Assert
        Assert.NotNull(listForTestUser);
        Assert.NotEmpty(listForTestUser);
        Assert.Equal(2, listForTestUser.Count);
        Assert.Contains(listForTestUser, p => p.Name == "Test Project");
        Assert.Contains(listForTestUser, p => p.Name == "Other Project");

        Assert.NotNull(listForLonely);
        Assert.NotEmpty(listForLonely);
        Assert.Single(listForLonely);
        Assert.Contains(listForLonely, p => p.Name == "Lone Project");
    }

    [Fact]
    public async Task GetAllProjects_SysAdmin_ReturnsAllProjects()
    {
        // Arrange - Mark TestUser as sys admin
        var user = await Context.Users.FindAsync(uid);
        user!.IsSysAdmin = true;
        await Context.SaveChangesAsync();

        // Act
        var listForSysAdmin = (await _projectBusiness.GetAllProjects(uid, oid)).ToList();

        // Assert
        Assert.NotNull(listForSysAdmin);
        Assert.NotEmpty(listForSysAdmin);
        Assert.Equal(4, listForSysAdmin.Count); // Should see all non-archived projects
        Assert.Contains(listForSysAdmin, p => p.Name == "Test Project");
        Assert.Contains(listForSysAdmin, p => p.Name == "Other Project");
        Assert.Contains(listForSysAdmin, p => p.Name == "Lone Project");
    }

    [Fact]
    public async Task GetAllProjects_SysAdmin_IncludesArchivedWhenSpecified()
    {
        // Arrange - Mark TestUser as sys admin
        var user = await Context.Users.FindAsync(uid);
        user!.IsSysAdmin = true;
        await Context.SaveChangesAsync();

        // Act
        var listWithArchived = (await _projectBusiness.GetAllProjects(uid, oid, false)).ToList();

        // Assert
        Assert.NotNull(listWithArchived);
        Assert.NotEmpty(listWithArchived);
        Assert.Equal(5, listWithArchived.Count); // Should see all projects including archived
        Assert.Contains(listWithArchived, p => p.Name == "Test Project");
        Assert.Contains(listWithArchived, p => p.Name == "Other Project");
        Assert.Contains(listWithArchived, p => p.Name == "Lone Project");
        Assert.Contains(listWithArchived, p => p.Name == "Archived Project");
    }

    [Fact]
    public async Task GetAllProjects_NonSysAdmin_OnlySeesTheirProjects()
    {
        // Arrange - Ensure TestUser is NOT sys admin
        var user = await Context.Users.FindAsync(uid);
        user!.IsSysAdmin = false;
        await Context.SaveChangesAsync();

        // Act
        var listForRegularUser = (await _projectBusiness.GetAllProjects(uid, oid)).ToList();

        // Assert
        Assert.NotNull(listForRegularUser);
        Assert.NotEmpty(listForRegularUser);
        Assert.Equal(2, listForRegularUser.Count); // Should only see projects they're members of
        Assert.Contains(listForRegularUser, p => p.Name == "Test Project");
        Assert.Contains(listForRegularUser, p => p.Name == "Other Project");
        Assert.DoesNotContain(listForRegularUser, p => p.Name == "Lone Project");
    }

    [Fact]
    public async Task GetAllProjects_Fails_IfUserDoesNotExist()
    {
        // Arrange
        const long nonExistentUserId = 999999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _projectBusiness.GetAllProjects(nonExistentUserId, oid));

        Assert.Contains($"User with id {nonExistentUserId} not found.", exception.Message);
    }

    [Fact]
    public async Task GetAllProjects_FiltersByOrganizationId()
    {
        // Arrange
        var user = await Context.Users.FindAsync(uid);
        user!.IsSysAdmin = true;
        await Context.SaveChangesAsync();

        // Act
        var projectsForOrganization = (await _projectBusiness.GetAllProjects(uid, oid)).ToList();

        // Assert
        Assert.NotNull(projectsForOrganization);
        Assert.NotEmpty(projectsForOrganization);
        Assert.Equal(4, projectsForOrganization.Count);
        Assert.All(projectsForOrganization, p => Assert.Equal(oid, p.OrganizationId));
    }

    #endregion

    #region UpdateProject Tests

    [Fact]
    public async Task UpdateProject_Success_ReturnsCorrectValues()
    {
        // Arrange
        var originalProj = await Context.Projects.FindAsync(pid);
        var originalUpdatedAt = originalProj.LastUpdatedAt;

        var dto = new UpdateProjectRequestDto
        {
            Name = $"Updated Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Updated Description",
            Abbreviation = "UPD"
        };

        // Act
        var updatedResult = await _projectBusiness.UpdateProject(uid, oid, pid, dto);

        // Assert
        Assert.True(originalProj.Id == updatedResult.Id);
        Assert.True(originalUpdatedAt <= updatedResult.LastUpdatedAt);
        Assert.Equal(originalProj.Name, updatedResult.Name);
        Assert.Equal(originalProj.Description, updatedResult.Description);
        Assert.Equal(originalProj.Abbreviation, updatedResult.Abbreviation);
        Assert.Equal(originalProj.OrganizationId, updatedResult.OrganizationId);
        Assert.Equal(uid, updatedResult.LastUpdatedBy);

        // Ensure that Project Update Event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(oid, actualEvent.OrganizationId);
        Assert.Equal("project", actualEvent.EntityType);
        Assert.Equal(pid, actualEvent.EntityId);
        Assert.Equal("update", actualEvent.Operation);
    }

    [Fact]
    public async Task UpdateProject_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateProjectRequestDto
        {
            Name = "Updated Project",
            Description = "Updated Description",
            Abbreviation = "UPD"
        };
        const long nonExistentId = 999999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UpdateProject(uid, oid, nonExistentId, dto));

        Assert.Contains($"Project with id {nonExistentId} not found", exception.Message);

        // Ensure that no project update event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteProject Tests

    [Fact]
    public async Task DeleteProject_Success_WhenExists()
    {
        // Act
        var deletedResult = await _projectBusiness.DeleteProject(uid, oid, pid);

        // Assert
        Assert.True(deletedResult);
        var deletedProject = await Context.Projects.FindAsync(pid);
        Assert.Null(deletedProject);
    }

    [Fact]
    public async Task DeleteProject_Fails_IfNotFound()
    {
        // Arrange
        const long nonExistentId = 999999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.DeleteProject(uid, oid, nonExistentId));

        Assert.Contains($"Project with id {nonExistentId} not found.", exception.Message);
    }

    #endregion

    #region ArchiveProject Tests

    [Fact]
    public async Task ArchiveProject_Success_WhenExists()
    {
        // Arrange
        var originalProject = await Context.Projects.FindAsync(pid);
        var originalUpdatedAt = originalProject.LastUpdatedAt;

        // Act
        var archivedResult = await _projectBusiness.ArchiveProject(uid, oid, pid);

        // Assert
        Assert.True(archivedResult);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // verify saved in db
        var archivedProject = await Context.Projects.FindAsync(pid);
        Assert.NotNull(archivedProject);
        Assert.True(archivedProject.IsArchived);

        //verify other fields preserved
        Assert.True(originalProject.Id == archivedProject.Id);
        Assert.True(originalUpdatedAt <= archivedProject.LastUpdatedAt);
        Assert.Equal(originalProject.Name, archivedProject.Name);
        Assert.Equal(originalProject.Description, archivedProject.Description);
        Assert.Equal(originalProject.Abbreviation, archivedProject.Abbreviation);
        Assert.Equal(originalProject.OrganizationId, archivedProject.OrganizationId);
        Assert.Equal(uid, archivedProject.LastUpdatedBy);
        Assert.Equal(originalProject.Config, archivedProject.Config);


        // Ensure that project soft delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("project", actualEvent.EntityType);
        Assert.Equal(pid, actualEvent.EntityId);
        Assert.Equal("archive", actualEvent.Operation);
    }

    [Fact]
    public async Task ArchiveProject_Fails_IfNotFound()
    {
        // Arrange
        const long nonExistentId = 999999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.ArchiveProject(uid, oid, nonExistentId));

        Assert.Contains($"Project with id {nonExistentId}", exception.Message);

        // Ensure that project soft delete event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveProject Tests

    [Fact]
    public async Task UnarchiveProject_Success_WhenArchived()
    {
        // Arrange
        var originalProject = await Context.Projects.FindAsync(pid4);
        var originalUpdatedAt = originalProject.LastUpdatedAt;

        // Act
        var unarchivedResult = await _projectBusiness.UnarchiveProject(uid, oid, pid4); //pid4 is archived

        // Assert
        Assert.True(unarchivedResult);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // Verify archived false in DB
        var unarchivedProject = await Context.Projects.FindAsync(pid4);
        Assert.NotNull(unarchivedProject);
        Assert.False(unarchivedProject.IsArchived);
        Assert.Equal(uid, unarchivedProject.LastUpdatedBy);

        // verify saved in db
        var archivedProject = await Context.Projects.FindAsync(pid4);
        Assert.NotNull(archivedProject);
        Assert.False(archivedProject.IsArchived);

        //verify other fields preserved
        Assert.True(originalProject.Id == archivedProject.Id);
        Assert.True(originalUpdatedAt <= archivedProject.LastUpdatedAt);
        Assert.Equal(originalProject.Name, archivedProject.Name);
        Assert.Equal(originalProject.Description, archivedProject.Description);
        Assert.Equal(originalProject.Abbreviation, archivedProject.Abbreviation);
        Assert.Equal(originalProject.OrganizationId, archivedProject.OrganizationId);
        Assert.Equal(uid, archivedProject.LastUpdatedBy);
        Assert.Equal(originalProject.Config, archivedProject.Config);
    }

    [Fact]
    public async Task UnarchiveProject_Fails_IfNotFound()
    {
        // Arrange
        const long nonExistentId = 999999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UnarchiveProject(uid, oid, nonExistentId));

        Assert.Contains("Project not found or is not archived.", exception.Message);
    }

    [Fact]
    public async Task UnarchiveProject_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UnarchiveProject(uid, oid, pid)); // pid is not archived

        Assert.Contains("Project not found or is not archived.", exception.Message);
    }

    #endregion

    #region DTO Tests

    [Fact]
    public void ProjectRequestDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Act
        var dto = new CreateProjectRequestDto
        {
            Name = "Test Project",
            Description = "Test Description",
            Abbreviation = "TST"
        };

        // Assert
        Assert.Equal("Test Project", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("TST", dto.Abbreviation);
    }

    [Fact]
    public void ProjectResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var dto = new ProjectResponseDto
        {
            Id = 1,
            Name = "Test Project",
            Description = "Test Description",
            Abbreviation = "TST",
            LastUpdatedBy = uid,
            LastUpdatedAt = now,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Project", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("TST", dto.Abbreviation);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
    }

    #endregion

    #region GetProjectMembers Tests

    [Fact]
    public async Task GetProjectMembers_WithOnlyUsers_ReturnsUserMembers()
    {
        // Act
        var result = await _projectBusiness.GetProjectMembers(pid); // proj with only users
        var members = result.ToList();

        // Assert
        Assert.NotEmpty(members);
        Assert.Single(members);
        Assert.Contains(members, m => m.Name == "Test User" && m.Email == "test@example.com" && m.Role == "Test Role");
    }

    [Fact]
    public async Task GetProjectMembers_WithOnlyGroups_ReturnsGroupMembers()
    {
        // Act
        var result = await _projectBusiness.GetProjectMembers(pid5); // proj with only groups
        var members = result.ToList();

        // Assert
        Assert.NotEmpty(members);
        Assert.Single(members);
        Assert.All(members, m => Assert.Empty(m.Email)); // All should have empty emails (groups only)
        Assert.Contains(members, m => m.Name == "Test Group" && m.Email == string.Empty && m.Role == "Test Role");
    }

    [Fact]
    public async Task GetProjectMembers_WithUsersAndGroups_ReturnsBothTypes()
    {
        // Act
        var result = await _projectBusiness.GetProjectMembers(pid2);
        var members = result.ToList();

        // Assert
        Assert.NotEmpty(members);
        Assert.Equal(2, members.Count);

        // Verify user
        Assert.Contains(members, m => m.Name == "Test User" && m.Email == "test@example.com" && m.Role == "Test Role");

        // Verify group
        Assert.Contains(members, m => m.Name == "Test Group" && m.Email == string.Empty && m.Role == "Test Role");

        // Verify mix of emails (users have emails, groups don't)
        var usersWithEmails = members.Where(m => !string.IsNullOrEmpty(m.Email)).ToList();
        var groupsWithoutEmails = members.Where(m => string.IsNullOrEmpty(m.Email)).ToList();
        Assert.Single(usersWithEmails);
        Assert.Single(groupsWithoutEmails);
    }

    #endregion

    #region AddMemberToProject Tests

    [Fact]
    public async Task AddMemberToProject_CanAddUserToProject_WithoutRole()
    {
        // Act
        var result = await _projectBusiness.AddMemberToProject(pid3, null, uid, null);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.UserId == uid);
        Assert.NotNull(projectMember);
        Assert.Null(projectMember.RoleId);
    }

    [Fact]
    public async Task AddMemberToProject_CanAddGroupToProject_WithoutRole()
    {
        // Act
        var result = await _projectBusiness.AddMemberToProject(pid3, null, null, gid);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.GroupId == gid);
        Assert.NotNull(projectMember);
        Assert.Null(projectMember.RoleId);
    }

    [Fact]
    public async Task AddMemberToProject_CanAddUserToProject_WithRole()
    {
        // Act
        var result = await _projectBusiness.AddMemberToProject(pid3, rid, uid, null);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.UserId == uid);
        Assert.NotNull(projectMember);
        Assert.Equal(rid, projectMember.RoleId);
    }

    [Fact]
    public async Task AddMemberToProject_CanAddGroupToProject_WithRole()
    {
        // Act
        var result = await _projectBusiness.AddMemberToProject(pid3, rid, null, gid);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.GroupId == gid);
        Assert.NotNull(projectMember);
        Assert.Equal(rid, projectMember.RoleId);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfBothUserAndGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _projectBusiness.AddMemberToProject(pid3, null, uid, gid));

        Assert.Contains("Please provide only one of User ID or Group ID, not both", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfNeitherUserNorGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _projectBusiness.AddMemberToProject(pid3, null, null, null));

        Assert.Contains("One of User ID or Group ID must be provided", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfUserDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.AddMemberToProject(pid3, null, uid2, null));

        Assert.Contains($"User with id {uid2} not found", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfGroupDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.AddMemberToProject(pid3, null, null, gid2));

        Assert.Contains($"Group with id {gid2} not found", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfRoleDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _projectBusiness.AddMemberToProject(pid3, rid2, uid, null));

        Assert.Contains($"Role with id {rid2} not found", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfProjectDoesNotExist()
    {
        // Act & Assert
        const long MissingProjectId = 999999;
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _projectBusiness.AddMemberToProject(MissingProjectId, null, uid, null)
        );
        Assert.Equal($"Project with id {MissingProjectId} not found", exception.Message);
    }

    [Fact]
    public async Task AddMemberToProject_Fails_IfProjectMemberExists()
    {
        // Add the member first
        await _projectBusiness.AddMemberToProject(pid3, null, uid, null);

        // Act & Assert - try to add the same member again
        var result = await _projectBusiness.AddMemberToProject(pid3, null, uid, null);
        Assert.False(result); // Should return false when member already exists
    }

    #endregion

    #region UpdateProjectMemberRole Tests

    [Fact]
    public async Task UpdateProjectMemberRole_CanUpdateUserRole()
    {
        var originalRole = new Role
        {
            Name = "Original Role",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        var newRole = new Role
        {
            Name = "New Role",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        Context.Roles.AddRange(originalRole, newRole);
        await Context.SaveChangesAsync();

        // Add member with original role
        await _projectBusiness.AddMemberToProject(pid3, originalRole.Id, uid, null);

        // Act
        var result = await _projectBusiness.UpdateProjectMemberRole(pid3, newRole.Id, uid, null);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.UserId == uid);
        Assert.NotNull(projectMember);
        Assert.Equal(newRole.Id, projectMember.RoleId);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_CanUpdateGroupRole()
    {
        // Arrange
        var originalRole = new Role
        {
            Name = "Original Role",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        var newRole = new Role
        {
            Name = "New Role",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false,
            OrganizationId = oid
        };
        Context.Roles.AddRange(originalRole, newRole);
        await Context.SaveChangesAsync();

        // Add member with original role
        await _projectBusiness.AddMemberToProject(pid3, originalRole.Id, null, gid);

        // Act
        var result = await _projectBusiness.UpdateProjectMemberRole(pid3, newRole.Id, null, gid);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.GroupId == gid);
        Assert.NotNull(projectMember);
        Assert.Equal(newRole.Id, projectMember.RoleId);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfBothUserAndGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _projectBusiness.UpdateProjectMemberRole(pid, rid, 1, 1)
            );
        Assert.Equal("Please provide only one of User ID or Group ID, not both", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfNeitherUserNorGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _projectBusiness.UpdateProjectMemberRole(pid, rid, null, null)
            );
        Assert.Equal("One of User ID or Group ID must be provided", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfUserDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UpdateProjectMemberRole(pid, rid, uid2, null)
            );
        Assert.Equal($"User with id {uid2} is not a member of project {pid}", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfGroupDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UpdateProjectMemberRole(pid, rid, null, gid2)
            );
        Assert.Equal($"Group with id {gid2} is not a member of project {pid}", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfRoleDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UpdateProjectMemberRole(pid, rid2, uid, null)
            );
        Assert.Equal($"Role with id {rid2} not found", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfProjectDoesNotExist()
    {
        // Act & Assert
        const long MissingProjectId = 999999;
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _projectBusiness.UpdateProjectMemberRole(MissingProjectId, rid, uid, null)
        );
        Assert.Equal($"User with id {uid} is not a member of project {MissingProjectId}", exception.Message);
    }

    [Fact]
    public async Task UpdateProjectMemberRole_Fails_IfProjectMemberNotExists()
    {
        // Act & Assert (user exists but is not a member of the project)
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.UpdateProjectMemberRole(pid3, rid, uid, null)
            );
        Assert.Equal($"User with id {uid} is not a member of project {pid3}", exception.Message);
    }

    #endregion

    #region RemoveMemberFromProject Tests

    [Fact]
    public async Task RemoveMemberFromProject_CanRemoveUser()
    {
        // Act
        var result = await _projectBusiness.RemoveMemberFromProject(pid, uid, null);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid && pm.UserId == uid);
        Assert.Null(projectMember);
    }

    [Fact]
    public async Task RemoveMemberFromProject_CanRemoveGroup()
    {
        // Add group to project first - we don't seed this data as of now for groups
        await _projectBusiness.AddMemberToProject(pid3, null, null, gid);

        // Act
        var result = await _projectBusiness.RemoveMemberFromProject(pid3, null, gid);

        // Assert
        Assert.True(result);
        var projectMember = await Context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == pid3 && pm.GroupId == gid);
        Assert.Null(projectMember);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfBothUserAndGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _projectBusiness.RemoveMemberFromProject(pid, 1, 1)
            );
        Assert.Equal("Please provide only one of User ID or Group ID, not both", exception.Message);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfNeitherUserNorGroupAreSet()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _projectBusiness.RemoveMemberFromProject(pid, null, null)
            );
        Assert.Equal("One of either User ID or Group ID must be provided", exception.Message);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfUserDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.RemoveMemberFromProject(pid, uid2, null)
            );
        Assert.Equal($"User with id {uid2} is not a member of project {pid}", exception.Message);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfGroupDoesNotExist()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.RemoveMemberFromProject(pid, null, gid2)
            );
        Assert.Equal($"Group with id {gid2} is not a member of project {pid}", exception.Message);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfProjectDoesNotExist()
    {
        // Act & Assert
        const long nonExistentProjectId = 999999;
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _projectBusiness.RemoveMemberFromProject(nonExistentProjectId, uid, null)
        );
        Assert.Equal($"User with id {uid} is not a member of project {nonExistentProjectId}", exception.Message);
    }

    [Fact]
    public async Task RemoveMemberFromProject_Fails_IfProjectMemberNotExists()
    {
        // Act & Assert (user exists but is not a member of the project)
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _projectBusiness.RemoveMemberFromProject(pid3, uid, null)
            );
        Assert.Equal($"User with id {uid} is not a member of project {pid3}", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateProject_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testProject = new Project
        {
            Name = $"Test Project with User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description with User ID",
            Abbreviation = "TST",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };

        // Act
        Context.Projects.Add(testProject);
        await Context.SaveChangesAsync();

        // Assert
        var savedProject = await Context.Projects.FindAsync(testProject.Id);
        Assert.NotNull(savedProject);
        Assert.Equal(uid, savedProject.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateProject_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testProject = new Project
        {
            Name = $"Test Project Navigation {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Navigation Property",
            Abbreviation = "NAV",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };

        Context.Projects.Add(testProject);
        await Context.SaveChangesAsync();

        // Act
        var projectWithUser = await Context.Projects
            .Include(p => p.LastUpdatedByUser)
            .FirstAsync(p => p.Id == testProject.Id);

        // Assert
        Assert.NotNull(projectWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", projectWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test@example.com", projectWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, projectWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateProject_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testProject = new Project
        {
            Name = $"Test Project Null User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test with null LastUpdatedBy",
            Abbreviation = "NUL",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false,
            OrganizationId = oid
        };

        // Act
        Context.Projects.Add(testProject);
        await Context.SaveChangesAsync();

        // Assert
        var savedProject = await Context.Projects.FindAsync(testProject.Id);
        Assert.NotNull(savedProject);
        Assert.Null(savedProject.LastUpdatedBy);

        var projectWithUser = await Context.Projects
            .Include(p => p.LastUpdatedByUser)
            .FirstAsync(p => p.Id == testProject.Id);

        Assert.Null(projectWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateProject_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testProject = new Project
        {
            Name = $"Original Project {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Original Description",
            Abbreviation = "ORI",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            OrganizationId = oid
        };
        Context.Projects.Add(testProject);
        await Context.SaveChangesAsync();

        // Act
        testProject.LastUpdatedBy = uid;
        testProject.Description = "Updated Description";
        testProject.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Projects.Update(testProject);
        await Context.SaveChangesAsync();

        // Assert
        var updatedProject = await Context.Projects
            .Include(p => p.LastUpdatedByUser)
            .FirstAsync(p => p.Id == testProject.Id);

        Assert.Equal(uid, updatedProject.LastUpdatedBy);
        Assert.NotNull(updatedProject.LastUpdatedByUser);
        Assert.Equal("Test User", updatedProject.LastUpdatedByUser.Name);
        Assert.Equal("Updated Description", updatedProject.Description);
    }

    #endregion
}