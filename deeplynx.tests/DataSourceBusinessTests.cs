using System.Text.Json.Nodes;
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

public class DataSourceBusinessTests : IntegrationTestBase
{
    private readonly EventBusiness _eventBusiness;
    private readonly Mock<IEdgeBusiness> _mockEdgeBusiness;
    private readonly Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private readonly Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private readonly Mock<IRecordBusiness> _mockRecordBusiness;
    private readonly INotificationBusiness _notificationBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness;
    public long did;
    public long did2;
    public long did3;
    private long oid;
    public long pid;
    public long pid2;
    private long uid;

    public DataSourceBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
        _mockEdgeBusiness = new Mock<IEdgeBusiness>();
        _mockRecordBusiness = new Mock<IRecordBusiness>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _dataSourceBusiness = new DataSourceBusiness(
            Context,
            _mockEdgeBusiness.Object,
            _mockRecordBusiness.Object,
            _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        var testUser = new User
        {
            Name = "John Smith",
            Email = "john.smith@company.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(testUser);
        await Context.SaveChangesAsync();
        uid = testUser.Id;

        var org = new Organization { Name = "Test Org" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var project = new Project { Name = "Project 1", OrganizationId = oid };
        var project2 = new Project { Name = "Project2", OrganizationId = oid };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);

        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        var dataSource = new DataSource
        {
            Name = "Customer CRM Database",
            Description = "Primary customer relationship management database",
            Abbreviation = "CRM_DB",
            Type = "SQL Server",
            BaseUri = "Server=crm-prod.company.com;Database=CustomerData;",
            Config =
                @"{""driver"":""sqlserver"",""host"":""crm-prod.company.com"",""port"":1433,""database"":""CustomerData"",""ssl_enabled"":true}",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedBy = testUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false
        };
        var dataSource2 = new DataSource
        {
            Name = "Customer CRM Database 2",
            Description = "Primary customer relationship management database",
            Abbreviation = "CRM_DB",
            Type = "SQL Server",
            BaseUri = "Server=crm-prod.company.com;Database=CustomerData;",
            Config =
                @"{""driver"":""sqlserver"",""host"":""crm-prod.company.com"",""port"":1433,""database"":""CustomerData"",""ssl_enabled"":true}",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedBy = testUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false
        };
        var dataSource3 = new DataSource
        {
            Name = "Customer CRM Database 3",
            Description = "Primary customer relationship management database",
            Abbreviation = "CRM_DB",
            Type = "SQL Server",
            BaseUri = "Server=crm-prod.company.com;Database=CustomerData;",
            Config =
                @"{""driver"":""sqlserver"",""host"":""crm-prod.company.com"",""port"":1433,""database"":""CustomerData"",""ssl_enabled"":true}",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedBy = testUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = true
        };
        Context.DataSources.Add(dataSource);
        Context.DataSources.Add(dataSource2);
        Context.DataSources.Add(dataSource3);
        await Context.SaveChangesAsync();
        did = dataSource.Id;
        did2 = dataSource2.Id;
        did3 = dataSource3.Id;
    }

    #region GetDefaultDataSource Tests

    [Fact]
    public async Task GetDefaultDataSource_ProjectLevel_ReturnsProjectDefault()
    {
        // Arrange - Set one of the existing data sources as project-level default
        var projectDefault = await Context.DataSources.FindAsync(did);
        projectDefault!.Default = true;
        await Context.SaveChangesAsync();

        // Act
        var result = await _dataSourceBusiness.GetDefaultDataSource(oid, pid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(did, result.Id);
        Assert.Equal("Customer CRM Database", result.Name);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultDataSource_OrgLevel_ReturnsOrgDefault()
    {
        // Arrange - Create an org-level default (ProjectId = null)
        var orgLevelDefault = new DataSource
        {
            Name = "Org-Wide Data Source",
            Description = "Organization-level default",
            Abbreviation = "ORG_DS",
            Type = "PostgreSQL",
            BaseUri = "Server=org-db.company.com;Database=OrgData;",
            Config = @"{""driver"":""postgresql""}",
            OrganizationId = oid,
            ProjectId = null, // Org-level
            Default = true,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(orgLevelDefault);
        await Context.SaveChangesAsync();

        // Act - Request org-level default (projectId = null)
        var result = await _dataSourceBusiness.GetDefaultDataSource(oid, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orgLevelDefault.Id, result.Id);
        Assert.Equal("Org-Wide Data Source", result.Name);
        Assert.Null(result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultDataSource_ProjectLevel_NoDefault_ThrowsKeyNotFoundException()
    {
        // Arrange - All seeded data sources have Default = false by default

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.GetDefaultDataSource(oid, pid));

        Assert.Contains($"Default data source for project {pid} not found", exception.Message);
    }

    [Fact]
    public async Task GetDefaultDataSource_OrgLevel_NoDefault_ThrowsKeyNotFoundException()
    {
        // Arrange - Only project-level data sources exist (all have ProjectId = pid)
        // No org-level defaults exist

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.GetDefaultDataSource(oid, null));

        Assert.Contains($"Default data source for organization {oid} not found", exception.Message);
    }

    [Fact]
    public async Task GetDefaultDataSource_ProjectLevel_IgnoresArchivedDefaults()
    {
        // Arrange - Set the archived data source as default
        var archivedDataSource = await Context.DataSources.FindAsync(did3);
        archivedDataSource!.Default = true;
        await Context.SaveChangesAsync();

        // Act & Assert - Should not find archived default
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.GetDefaultDataSource(oid, pid));

        Assert.Contains($"Default data source for project {pid} not found", exception.Message);
    }

    [Fact]
    public async Task GetDefaultDataSource_ProjectLevel_DoesNotReturnOrgDefault()
    {
        // Arrange - Create org-level default and project-level default
        var orgLevelDefault = new DataSource
        {
            Name = "Org Default",
            ProjectId = null, // Org-level
            Default = true,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(orgLevelDefault);

        var projectDefault = await Context.DataSources.FindAsync(did);
        projectDefault!.Default = true;
        await Context.SaveChangesAsync();

        // Act - Request project-level default
        var result = await _dataSourceBusiness.GetDefaultDataSource(oid, pid);

        // Assert - Should return project default, NOT org default
        Assert.NotNull(result);
        Assert.Equal(did, result.Id);
        Assert.Equal(pid, result.ProjectId);
        Assert.NotEqual(orgLevelDefault.Id, result.Id);
    }

    [Fact]
    public async Task GetDefaultDataSource_OrgLevel_DoesNotReturnProjectDefault()
    {
        // Arrange - Set existing project data source as default
        var projectDefault = await Context.DataSources.FindAsync(did);
        projectDefault!.Default = true;
        await Context.SaveChangesAsync();

        // Act & Assert - Requesting org-level should NOT return project-level
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.GetDefaultDataSource(oid, null));

        Assert.Contains($"Default data source for organization {oid} not found", exception.Message);
    }

    #endregion

    #region SetDefaultDataSource

    [Fact]
    public async Task SetDefaultDataSource_ProjectLevel_SetsDefaultAndUnsetsPrevious()
    {
        // Arrange - Set did as current default
        var currentDefault = await Context.DataSources.FindAsync(did);
        currentDefault!.Default = true;
        await Context.SaveChangesAsync();

        // Act - Set did2 as new default
        var result = await _dataSourceBusiness.SetDefaultDataSource(oid, pid, uid, did2);

        // Assert - New default is set
        Assert.NotNull(result);
        Assert.Equal(did2, result.Id);
        Assert.True(result.Default);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Assert - Previous default is unset
        Context.ChangeTracker.Clear();
        var previousDefault = await Context.DataSources.FindAsync(did);
        Assert.False(previousDefault!.Default);
        Assert.Equal(uid, previousDefault.LastUpdatedBy);

        // Assert - Event was created
        var events = await Context.Events.Where(e => e.EntityId == did2).ToListAsync();
        Assert.Single(events);
        Assert.Equal("update", events[0].Operation);
        Assert.Equal("data_source", events[0].EntityType);
    }

    [Fact]
    public async Task SetDefaultDataSource_OrgLevel_SetsDefaultAndUnsetsPrevious()
    {
        // Arrange - Create two org-level data sources
        var orgDefault1 = new DataSource
        {
            Name = "Org Default 1",
            ProjectId = null,
            Default = true,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        var orgDefault2 = new DataSource
        {
            Name = "Org Default 2",
            ProjectId = null,
            Default = false,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.AddRange(orgDefault1, orgDefault2);
        await Context.SaveChangesAsync();

        // Act - Set orgDefault2 as default
        var result = await _dataSourceBusiness.SetDefaultDataSource(oid, null, uid, orgDefault2.Id);

        // Assert - New default is set
        Assert.NotNull(result);
        Assert.Equal(orgDefault2.Id, result.Id);
        Assert.True(result.Default);
        Assert.Null(result.ProjectId);

        // Assert - Previous org-level default is unset
        Context.ChangeTracker.Clear();
        var previousDefault = await Context.DataSources.FindAsync(orgDefault1.Id);
        Assert.False(previousDefault!.Default);
    }

    [Fact]
    public async Task SetDefaultDataSource_ProjectLevel_DoesNotAffectOrgLevelDefault()
    {
        // Arrange - Create org-level default and project-level data source
        var orgDefault = new DataSource
        {
            Name = "Org Default",
            ProjectId = null,
            Default = true,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(orgDefault);
        await Context.SaveChangesAsync();

        // Act - Set project-level data source as default
        var result = await _dataSourceBusiness.SetDefaultDataSource(oid, pid, uid, did);

        // Assert - Project default is set
        Assert.True(result.Default);
        Assert.Equal(pid, result.ProjectId);

        // Assert - Org-level default is unchanged
        Context.ChangeTracker.Clear();
        var orgDefaultAfter = await Context.DataSources.FindAsync(orgDefault.Id);
        Assert.True(orgDefaultAfter!.Default); // Should still be true
    }

    [Fact]
    public async Task SetDefaultDataSource_OrgLevel_DoesNotAffectProjectLevelDefaults()
    {
        // Arrange - Set project-level data source as default
        var projectDefault = await Context.DataSources.FindAsync(did);
        projectDefault!.Default = true;
        await Context.SaveChangesAsync();

        // Create org-level data source
        var orgDataSource = new DataSource
        {
            Name = "Org Data Source",
            ProjectId = null,
            Default = false,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(orgDataSource);
        await Context.SaveChangesAsync();

        // Act - Set org-level as default
        var result = await _dataSourceBusiness.SetDefaultDataSource(oid, null, uid, orgDataSource.Id);

        // Assert - Org default is set
        Assert.True(result.Default);
        Assert.Null(result.ProjectId);

        // Assert - Project-level default is unchanged
        Context.ChangeTracker.Clear();
        var projectDefaultAfter = await Context.DataSources.FindAsync(did);
        Assert.True(projectDefaultAfter!.Default); // Should still be true
    }

    #endregion

    #region GetAllDataSources Tests

    [Fact]
    public async Task GetAllDataSources_ValidProjectId_ReturnsActiveDataSources()
    {
        // Act
        var result = await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid });
        var dataSources = result.ToList();

        // Assert
        Assert.Equal(2, dataSources.Count);
        Assert.All(dataSources, ds => Assert.Equal(pid, ds.ProjectId));
        Assert.All(dataSources, ds => Assert.False(ds.IsArchived));
        Assert.Contains(dataSources, ds => ds.Id == did);
        Assert.Contains(dataSources, ds => ds.Id == did2);
        Assert.DoesNotContain(dataSources, ds => ds.Id == did3);
    }

    [Fact]
    public async Task GetAllDataSources_DifferentProject_ReturnsCorrectDataSources()
    {
        // Arrange
        Context.DataSources.Add(new DataSource
            { Name = "Project 2 Data Source", OrganizationId = oid, ProjectId = pid2 });
        await Context.SaveChangesAsync();

        // Act
        var result = await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid });
        var dataSources = result.ToList();

        // Assert
        Assert.Equal(2, dataSources.Count);
        Assert.DoesNotContain(dataSources, ds => ds.Name == "Project 2 Data Source");
        Assert.All(dataSources, ds => Assert.Equal(pid, ds.ProjectId));
    }

    [Fact]
    public async Task GetAllDataSources_ConfigParsing_ReturnsValidJsonObject()
    {
        // Act
        var result = await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid }, false);
        var dataSource = result.First(ds => ds.Id == did);

        // Assert
        Assert.NotNull(dataSource.Config);
        Assert.Equal("sqlserver", dataSource.Config["driver"]?.ToString());
        Assert.Equal("crm-prod.company.com", dataSource.Config["host"]?.ToString());
        Assert.Equal(1433, dataSource.Config["port"]?.GetValue<int>());
    }

    [Fact]
    public async Task GetAllDataSources_NullConfig_ReturnsNullJsonObject()
    {
        // Arrange
        var dataSourceWithNullConfig = new DataSource
        {
            Name = "Null Config Test",
            OrganizationId = oid,
            Description = "Primary customer relationship management database",
            Abbreviation = "CRM_DB",
            Type = "SQL Server",
            BaseUri = "Server=crm-prod.company.com;Database=CustomerData;",
            Config = null,
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false
        };
        await Context.DataSources.AddAsync(dataSourceWithNullConfig);
        await Context.SaveChangesAsync();

        // Act
        var result = await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid }, false);
        var dataSource = result.First(ds => ds.Name == "Null Config Test");

        // Assert
        Assert.Null(dataSource.Config);
    }

    #endregion

    #region GetDataSource Tests

    [Fact]
    public async Task GetDataSource_ValidIds_ReturnsDataSource()
    {
        // Act
        var result = await _dataSourceBusiness.GetDataSource(oid, pid, did, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(did, result.Id);
        Assert.Equal("Customer CRM Database", result.Name);
        Assert.Equal("Primary customer relationship management database", result.Description);
        Assert.Equal("CRM_DB", result.Abbreviation);
        Assert.Equal("SQL Server", result.Type);
        Assert.Equal("Server=crm-prod.company.com;Database=CustomerData;", result.BaseUri);
        Assert.Equal(pid, result.ProjectId);
        Assert.NotNull(result.Config);
    }

    [Fact]
    public async Task GetDataSource_NonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.GetDataSource(oid, pid, 999, false));

        Assert.Contains("Data Source with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task GetDataSource_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _dataSourceBusiness.GetDataSource(oid,
            pid2,
            did,
            false)); // DataSource belongs to project with pid, not pid2

        Assert.Contains($"Data Source with id {did} not found", exception.Message);
    }

    [Fact]
    public async Task GetDataSource_ArchivedDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.GetDataSource(oid, pid, did3, true)); // did3 is archived

        Assert.Contains($"Data Source with id {did3} is archived", exception.Message);
    }

    [Fact]
    public async Task GetDataSource_ValidDataSource_ParsesConfigCorrectly()
    {
        // Act
        var result = await _dataSourceBusiness.GetDataSource(oid, pid, did, false);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal("sqlserver", result.Config["driver"]?.ToString());
        Assert.Equal("crm-prod.company.com", result.Config["host"]?.ToString());
        Assert.Equal(1433, result.Config["port"]?.GetValue<int>());
    }

    #endregion

    #region CreateDataSource Tests

    [Fact]
    public async Task CreateDataSource_ValidDto_ReturnsCorrectValues()
    {
        // Arrange
        var config = new JsonObject
        {
            ["driver"] = "postgresql",
            ["host"] = "localhost",
            ["port"] = 5432
        };

        var dto = new CreateDataSourceRequestDto
        {
            Name = "New Test Data Source",
            Description = "A newly created test data source",
            Abbreviation = "NEW_TEST",
            Type = "PostgreSQL",
            BaseUri = "Server=localhost;Database=NewTest;",
            Config = config
        };

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New Test Data Source", result.Name);
        Assert.Equal("A newly created test data source", result.Description);
        Assert.Equal("NEW_TEST", result.Abbreviation);
        Assert.Equal("PostgreSQL", result.Type);
        Assert.Equal("Server=localhost;Database=NewTest;", result.BaseUri);
        Assert.Equal(pid, result.ProjectId);
        Assert.NotNull(result.Config);
        Assert.Equal("postgresql", result.Config["driver"]?.ToString());
        Assert.Equal(uid, result.LastUpdatedBy);

        // Verify it was actually saved to database
        var savedDataSource = await Context.DataSources.FindAsync(result.Id);
        Assert.NotNull(savedDataSource);
        Assert.Equal("New Test Data Source", savedDataSource.Name);

        // Ensure that datasource create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateDataSource_NullDto_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _dataSourceBusiness.CreateDataSource(oid, pid, uid, null));

        // Ensure that datasource create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateDataSource_NullConfig_CreatesWithNullConfig()
    {
        // Arrange
        var dto = new CreateDataSourceRequestDto
        {
            Name = "No Config Data Source",
            Description = "Data source without config",
            Type = "File System"
        };

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Config);

        // Ensure that datasource create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateDataSource_SetsCreatedAtAndCreatedBy()
    {
        // Arrange
        var dto = new CreateDataSourceRequestDto
        {
            Name = "Timestamp Test",
            Type = "Test"
        };

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert
        Assert.True(result.LastUpdatedAt >= beforeCreate);
        Assert.True(result.LastUpdatedAt <= DateTime.UtcNow);
        Assert.Equal(uid, result.LastUpdatedBy);
        // Ensure that datasource create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateDataSource_ProjectLevel_AsDefault_UnsetsPreviousDefault()
    {
        // Arrange - Set existing data source as default
        var existingDefault = await Context.DataSources.FindAsync(did);
        existingDefault!.Default = true;
        await Context.SaveChangesAsync();

        var dto = new CreateDataSourceRequestDto
        {
            Name = "New Project Default",
            Description = "New default data source",
            Abbreviation = "NEW_DS",
            Type = "PostgreSQL",
            BaseUri = "Server=new-db.company.com",
            Default = true,
            Config = new JsonObject { ["driver"] = "postgresql" }
        };

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert - New data source is default
        Assert.NotNull(result);
        Assert.True(result.Default);
        Assert.Equal("New Project Default", result.Name);
        Assert.Equal(pid, result.ProjectId);

        // ExecuteUpdateAsync bypasses change tracker - must clear to see DB changes
        Context.ChangeTracker.Clear();

        // Assert - Previous default is unset
        var previousDefault = await Context.DataSources.FindAsync(did);
        Assert.False(previousDefault!.Default);
        Assert.Equal(uid, previousDefault.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateDataSource_OrgLevel_AsDefault_UnsetsPreviousOrgDefault()
    {
        // Arrange - Create existing org-level default
        var existingOrgDefault = new DataSource
        {
            Name = "Existing Org Default",
            ProjectId = null,
            Default = true,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(existingOrgDefault);
        await Context.SaveChangesAsync();

        var dto = new CreateDataSourceRequestDto
        {
            Name = "New Org Default",
            Description = "New org-level default",
            Abbreviation = "NEW_ORG",
            Type = "MySQL",
            Default = true
        };

        // Act - Create new org-level default (projectId = null)
        var result = await _dataSourceBusiness.CreateDataSource(oid, null, uid, dto);

        // Assert - New data source is default
        Assert.True(result.Default);
        Assert.Null(result.ProjectId);

        // ExecuteUpdateAsync bypasses change tracker - must clear to see DB changes
        Context.ChangeTracker.Clear();

        // Assert - Previous org-level default is unset
        var previousDefault = await Context.DataSources.FindAsync(existingOrgDefault.Id);
        Assert.False(previousDefault!.Default);
    }

    [Fact]
    public async Task CreateDataSource_ProjectLevel_AsDefault_DoesNotAffectOrgDefault()
    {
        // Arrange - Create org-level default
        var orgDefault = new DataSource
        {
            Name = "Org Default",
            ProjectId = null,
            Default = true,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.DataSources.Add(orgDefault);
        await Context.SaveChangesAsync();

        var dto = new CreateDataSourceRequestDto
        {
            Name = "New Project Default",
            Abbreviation = "PROJ_DS",
            Type = "SQL Server",
            Default = true
        };

        // Act - Create project-level default
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert - New project default is created
        Assert.True(result.Default);
        Assert.Equal(pid, result.ProjectId);

        // Assert - Org-level default is unchanged
        var orgDefaultAfter = await Context.DataSources.FindAsync(orgDefault.Id);
        Assert.True(orgDefaultAfter!.Default);
    }

    [Fact]
    public async Task CreateDataSource_OrgLevel_AsDefault_DoesNotAffectProjectDefaults()
    {
        // Arrange - Set existing project data source as default
        var projectDefault = await Context.DataSources.FindAsync(did);
        projectDefault!.Default = true;
        await Context.SaveChangesAsync();

        var dto = new CreateDataSourceRequestDto
        {
            Name = "New Org Default",
            Abbreviation = "ORG_DS",
            Type = "MongoDB",
            Default = true
        };

        // Act - Create org-level default
        var result = await _dataSourceBusiness.CreateDataSource(oid, null, uid, dto);

        // Assert - New org default is created
        Assert.True(result.Default);
        Assert.Null(result.ProjectId);

        // Assert - Project-level default is unchanged
        var projectDefaultAfter = await Context.DataSources.FindAsync(did);
        Assert.True(projectDefaultAfter!.Default);
    }

    #endregion

    #region UpdateDataSource Tests

    [Fact]
    public async Task UpdateDataSource_ValidUpdate_UpdatesDataSource()
    {
        // Arrange
        var config = new JsonObject
        {
            ["driver"] = "mysql",
            ["host"] = "updated.com",
            ["port"] = 3306
        };

        var dto = new UpdateDataSourceRequestDto
        {
            Name = "Updated Test Data Source",
            Description = "Updated description",
            Abbreviation = "UPD_TEST",
            Type = "MySQL",
            BaseUri = "Server=updated.com;Database=UpdatedDB;",
            Config = config
        };

        var beforeUpdate = DateTime.UtcNow;

        // Act
        var result = await _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(did, result.Id);
        Assert.True(result.LastUpdatedAt >= beforeUpdate);
        Assert.True(result.LastUpdatedAt <= DateTime.UtcNow);
        Assert.Equal("Updated Test Data Source", result.Name);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal("UPD_TEST", result.Abbreviation);
        Assert.Equal("MySQL", result.Type);
        Assert.Equal("Server=updated.com;Database=UpdatedDB;", result.BaseUri);
        Assert.Equal("mysql", result?.Config?["driver"]?.ToString());

        // Verify it was actually updated in database
        var updatedDataSource = await Context.DataSources.FindAsync(did);
        Assert.Equal("Updated Test Data Source", updatedDataSource?.Name);

        // Ensure that datasource update event was logged
        var eventList = await Context.Events.ToListAsync();

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateDataSource_PartialUpdate_UpdatesDataSource()
    {
        // Arrange
        var updateDto = new UpdateDataSourceRequestDto
        {
            Description = "Updated Description"
        };

        var beforeUpdate = DateTime.UtcNow;

        // Act
        var result = await _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did, updateDto);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(did, result.Id);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.True(result.LastUpdatedAt >= beforeUpdate);
        Assert.Equal("Customer CRM Database", result.Name);
        Assert.Equal("CRM_DB", result.Abbreviation);
        Assert.Equal("SQL Server", result.Type);
        Assert.Equal("Server=crm-prod.company.com;Database=CustomerData;", result.BaseUri);
        Assert.NotNull(result.Config);
        Assert.False(result.IsArchived);

        // Verify it was actually updated in database
        var updatedDataSource = await Context.DataSources.FindAsync(did);
        Assert.NotNull(updatedDataSource);
        Assert.Equal("Updated Description", updatedDataSource.Description);
        Assert.NotNull(updatedDataSource?.LastUpdatedAt);

        // Ensure that datasource update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateDataSource_NonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateDataSourceRequestDto
        {
            Name = "Update Test",
            Type = "Test"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.UpdateDataSource(oid, pid, uid, 999, dto));

        Assert.Contains("Data Source with id 999 not found", exception.Message);

        // Ensure that datasource update event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateDataSource_WrongProject_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateDataSourceRequestDto
        {
            Name = "Update Test",
            Type = "Test"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.UpdateDataSource(oid, pid2, uid, did, dto)); // did belongs to pid not pid2

        Assert.Contains($"Data Source with id {did} not found", exception.Message);

        // Ensure that datasource update event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateDataSource_ArchivedDataSource_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateDataSourceRequestDto
        {
            Name = "Update Test",
            Type = "Test"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did3, dto)); // DataSource 3 is archived

        Assert.Contains($"Data Source with id {did3} not found", exception.Message);

        // Ensure that datasource update event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateDataSource_NullConfig_UpdatesWithEmptyConfig()
    {
        // Arrange
        var dto = new UpdateDataSourceRequestDto
        {
            Name = "No Config Update",
            Type = "Test",
            Config = null
        };

        // Act
        var result = await _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did, dto);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Empty(result.Config);
        Assert.Equal(0, result.Config.Count);

        // Ensure that datasource update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    #endregion

    #region DeleteDataSource Tests

    [Fact]
    public async Task DeleteDataSource_ValidDataSource_DeletesSuccessfully()
    {
        // Act
        var result = await _dataSourceBusiness.DeleteDataSource(oid, pid, did);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from database
        var deletedDataSource = await Context.DataSources.FindAsync(did);
        Assert.Null(deletedDataSource);
    }

    [Fact]
    public async Task DeleteDataSource_NonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _dataSourceBusiness.DeleteDataSource(oid, pid, 999));

        Assert.Contains("Data Source with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task DeleteDataSource_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.DeleteDataSource(oid, pid2, did)); // DataSource 1 belongs to project 1

        Assert.Contains($"Data Source with id {did} not found", exception.Message);
    }

    #endregion

    #region ArchiveDataSource Tests

    [Fact]
    public async Task ArchiveDataSource_ValidDataSource_ArchivesSuccessfully()
    {
        var now = DateTime.UtcNow;
        // Act
        var result = await _dataSourceBusiness.ArchiveDataSource(oid, pid, uid, did);

        // Assert
        Assert.True(result);

        Context.ChangeTracker.Clear();

        // Verify it was actually archived in database
        var archivedDataSource = await Context.DataSources.FindAsync(did);
        Assert.NotNull(archivedDataSource);
        Assert.Equal(did, archivedDataSource.Id);
        Assert.Equal("Customer CRM Database", archivedDataSource.Name);
        Assert.Equal("Primary customer relationship management database", archivedDataSource.Description);
        Assert.Equal("CRM_DB", archivedDataSource.Abbreviation);
        Assert.Equal("SQL Server", archivedDataSource.Type);
        Assert.Equal("Server=crm-prod.company.com;Database=CustomerData;", archivedDataSource.BaseUri);
        Assert.Equal(pid, archivedDataSource.ProjectId);
        Assert.NotNull(archivedDataSource.Config);
        Assert.True(archivedDataSource.LastUpdatedAt >= now);
        Assert.Equal(uid, archivedDataSource.LastUpdatedBy);
        Assert.True(archivedDataSource.IsArchived);

        // Ensure that data source soft delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(did, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveDataSource_NonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.ArchiveDataSource(oid, pid, uid, 999));

        Assert.Contains("Data Source with id 999 not found", exception.Message);

        // Ensure that data source soft delete event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveDataSource_AlreadyArchivedDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _dataSourceBusiness.ArchiveDataSource(oid, pid, uid, did3)); // DataSource 3 is already archived

        Assert.Contains($"Data Source with id {did3} not found", exception.Message);

        // Ensure that data source soft delete event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveDataSource_ArchivedDataSourceNotReturnedInGetAll()
    {
        // Arrange
        var initialCount = (await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid })).Count();

        // Act
        await _dataSourceBusiness.ArchiveDataSource(oid, pid, uid, did);
        var finalCount = (await _dataSourceBusiness.GetAllDataSources(oid, new[] { pid })).Count();

        // Assert
        Assert.Equal(initialCount - 1, finalCount);

        // Ensure that data source soft delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("data_source", actualEvent.EntityType);
        Assert.Equal(did, actualEvent.EntityId);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task DataSourceOperations_ConcurrentModification_HandlesCorrectly()
    {
        // This test simulates concurrent operations on the same data source
        // In a real scenario, you might want to test with actual concurrent tasks

        // Arrange
        var dto1 = new UpdateDataSourceRequestDto
        {
            Name = "Concurrent Update 1",
            Type = "Test1"
        };

        var dto2 = new UpdateDataSourceRequestDto
        {
            Name = "Concurrent Update 2",
            Type = "Test2"
        };

        // As noted above, DbContext is not thread-safe so there's not a great way to truly simulate concurrent operations
        // so for now we take the sequential approach

        // Act
        await _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did, dto1);
        await _dataSourceBusiness.UpdateDataSource(oid, pid, uid, did, dto2);

        // Assert
        // Verify it was actually updated in database
        var updatedDataSource = await Context.DataSources.FindAsync(did);
        Assert.NotNull(updatedDataSource);
        Assert.Equal("Concurrent Update 2", updatedDataSource.Name);
    }

    [Fact]
    public async Task DataSourceOperations_LargeConfigJson_HandlesCorrectly()
    {
        // Arrange
        var largeConfig = new JsonObject();
        for (var i = 0; i < 100; i++)
        {
            largeConfig[$"property_{i}"] = $"value_{i}";
            largeConfig[$"nested_{i}"] = new JsonObject
            {
                ["sub_property"] = i,
                ["sub_array"] = new JsonArray { i, i + 1, i + 2 }
            };
        }

        var dto = new CreateDataSourceRequestDto
        {
            Name = "Large Config Test",
            Type = "Test",
            Config = largeConfig
        };

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal("value_50", result.Config["property_50"]?.ToString());
        Assert.Equal(50, result.Config["nested_50"]?["sub_property"]?.GetValue<int>());
    }

    [Fact]
    public async Task DataSourceOperations_SpecialCharactersInFields_HandlesCorrectly()
    {
        // Arrange
        var dto = new CreateDataSourceRequestDto
        {
            Name = "Test with émojis 🚀 and ñ special chars",
            Description = "Description with quotes \"test\" and 'single quotes'",
            Abbreviation = "SPEC_CHAR",
            Type = "Test & Special",
            BaseUri = "https://test.com/path?param=value&other=123",
            Config = new JsonObject
            {
                ["special"] = "Value with quotes \"test\" and newlines\nand tabs\t",
                ["unicode"] = "Unicode: 中文, العربية, русский"
            }
        };

        // Act
        var result = await _dataSourceBusiness.CreateDataSource(oid, pid, uid, dto);

        // Assert
        Assert.Equal("Test with émojis 🚀 and ñ special chars", result.Name);
        Assert.Contains("quotes \"test\"", result.Description);
        Assert.Equal("Test & Special", result.Type);
        Assert.Contains("Unicode: 中文", result.Config["unicode"]?.ToString());
    }

    #endregion

    #region DataSourceDTO Tests

    [Fact]
    public void DataSourceRequestDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var config = new JsonObject { ["test"] = "value" };
        var dto = new CreateDataSourceRequestDto
        {
            Name = "Test Name",
            Description = "Test Description",
            Abbreviation = "TEST",
            Type = "Test Type",
            BaseUri = "http://test.com",
            Config = config
        };

        // Assert
        Assert.Equal("Test Name", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("TEST", dto.Abbreviation);
        Assert.Equal("Test Type", dto.Type);
        Assert.Equal("http://test.com", dto.BaseUri);
        Assert.Equal(config, dto.Config);
    }

    [Fact]
    public void DataSourceResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var config = new JsonObject { ["test"] = "value" };
        var now = DateTime.UtcNow;

        var dto = new DataSourceResponseDto
        {
            Id = 1,
            Name = "Test Name",
            Description = "Test Description",
            Abbreviation = "TEST",
            Type = "Test Type",
            BaseUri = "http://test.com",
            Config = config,
            ProjectId = 1,
            LastUpdatedBy = uid,
            LastUpdatedAt = now,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Name", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("TEST", dto.Abbreviation);
        Assert.Equal("Test Type", dto.Type);
        Assert.Equal("http://test.com", dto.BaseUri);
        Assert.Equal(config, dto.Config);
        Assert.Equal(1, dto.ProjectId);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
    }

    #endregion

    #region UnarchiveDataSource Tests

    [Fact]
    public async Task UnarchiveDataSource_ValidArchivedDataSource_UnarchivesSuccessfully()
    {
        var now = DateTime.UtcNow;
        // Act
        var result = await _dataSourceBusiness.UnarchiveDataSource(oid, pid, uid, did3);

        // Assert
        Assert.True(result);

        Context.ChangeTracker.Clear();
        var reloaded = await Context.DataSources.FindAsync(did3);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsArchived);
        Assert.Equal(uid, reloaded.LastUpdatedBy);
        Assert.True(reloaded.LastUpdatedAt >= now);
        Assert.Equal("Customer CRM Database 3", reloaded.Name);
        Assert.Equal("Primary customer relationship management database", reloaded.Description);
        Assert.Equal("CRM_DB", reloaded.Abbreviation);
        Assert.Equal("SQL Server", reloaded.Type);
        Assert.Equal("Server=crm-prod.company.com;Database=CustomerData;", reloaded.BaseUri);
        Assert.NotNull(reloaded.Config);
        Assert.Equal(pid, reloaded.ProjectId);
        Assert.Equal(did3, reloaded.Id);
    }

    [Fact]
    public async Task UnarchiveDataSource_NonExistent_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.UnarchiveDataSource(oid, pid, uid, 99999));

        Assert.Contains("Data Source with id 99999 not found", ex.Message);
        // Ensure that data source unarchive event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveDataSource_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.UnarchiveDataSource(oid, pid2, uid, did3)); // did3 is archived and belongs to pid

        Assert.Contains($"Data Source with id {did3} not found", ex.Message);
        // Ensure that data source unarchive event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveDataSource_NotArchived_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _dataSourceBusiness.UnarchiveDataSource(oid, pid, uid, did)); // did is not archived

        Assert.Contains($"Data Source with id {did} not found", ex.Message);
        // Ensure that data source unarchive event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateDataSource_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testDataSource = new DataSource
        {
            Name = $"Test DataSource with User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description with User ID",
            Type = "Test Type",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Act
        Context.DataSources.Add(testDataSource);
        await Context.SaveChangesAsync();

        // Assert
        var savedDataSource = await Context.DataSources.FindAsync(testDataSource.Id);
        Assert.NotNull(savedDataSource);
        Assert.Equal(uid, savedDataSource.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateDataSource_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testDataSource = new DataSource
        {
            Name = $"Test DataSource Navigation {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Navigation Property",
            Type = "Test Type",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        Context.DataSources.Add(testDataSource);
        await Context.SaveChangesAsync();

        // Act
        var dataSourceWithUser = await Context.DataSources
            .Include(ds => ds.LastUpdatedByUser)
            .FirstAsync(ds => ds.Id == testDataSource.Id);

        // Assert
        Assert.NotNull(dataSourceWithUser.LastUpdatedByUser);
        Assert.Equal("John Smith", dataSourceWithUser.LastUpdatedByUser.Name);
        Assert.Equal("john.smith@company.com", dataSourceWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, dataSourceWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateDataSource_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testDataSource = new DataSource
        {
            Name = $"Test DataSource Null User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test with null LastUpdatedBy",
            Type = "Test Type",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false
        };

        // Act
        Context.DataSources.Add(testDataSource);
        await Context.SaveChangesAsync();

        // Assert
        var savedDataSource = await Context.DataSources.FindAsync(testDataSource.Id);
        Assert.NotNull(savedDataSource);
        Assert.Null(savedDataSource.LastUpdatedBy);

        var dataSourceWithUser = await Context.DataSources
            .Include(ds => ds.LastUpdatedByUser)
            .FirstAsync(ds => ds.Id == testDataSource.Id);

        Assert.Null(dataSourceWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateDataSource_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testDataSource = new DataSource
        {
            Name = $"Original DataSource {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            OrganizationId = oid,
            Description = "Original Description",
            Type = "Original Type",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.DataSources.Add(testDataSource);
        await Context.SaveChangesAsync();

        // Act
        testDataSource.LastUpdatedBy = uid;
        testDataSource.Description = "Updated Description";
        testDataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.DataSources.Update(testDataSource);
        await Context.SaveChangesAsync();

        // Assert
        var updatedDataSource = await Context.DataSources
            .Include(ds => ds.LastUpdatedByUser)
            .FirstAsync(ds => ds.Id == testDataSource.Id);

        Assert.Equal(uid, updatedDataSource.LastUpdatedBy);
        Assert.NotNull(updatedDataSource.LastUpdatedByUser);
        Assert.Equal("John Smith", updatedDataSource.LastUpdatedByUser.Name);
        Assert.Equal("Updated Description", updatedDataSource.Description);
    }

    #endregion
}