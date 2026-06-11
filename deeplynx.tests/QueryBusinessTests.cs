using System.Text.Json;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class QueryBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private QueryBusiness _queryBusiness = null!;
    private RecordBusiness _recordBusiness;
    private SensitivityLabelBusiness _sensitivityLabelBusiness;
    private ISensitivityLabelService _sensitivityLabelService = null!;
    private TagBusiness _tagBusiness = null!;
    private long cid;
    private long cid2;
    private long did;
    private long did2;
    private long organizationId;

    private long pid; // project ID
    private long pid2;
    private long pid3;
    private long pid4;
    private long rid; // record ID
    public long roleId;
    private long uid;

    public QueryBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    private long[] pids => [pid, pid2, pid3, pid4];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService);
        _queryBusiness = new QueryBusiness(Context, _sensitivityLabelService);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Test User",
            Email = "test_record@example.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);

        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        // Project 1: Anakin
        var project = new Project
        {
            Name = "Anakin",
            Description = "You turned her against me",
            OrganizationId = organizationId
        };
        await Context.Projects.AddAsync(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var tag = new Tag
        {
            Name = "Padme",
            ProjectId = project.Id,
            OrganizationId = organizationId
        };
        await Context.Tags.AddAsync(tag);
        await Context.SaveChangesAsync();

        var dataSource = new DataSource
        {
            Name = "R2D2",
            Description = "Weeeeeeeee!",
            ProjectId = project.Id,
            OrganizationId = organizationId
        };
        await Context.DataSources.AddAsync(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;

        var dataSource2 = new DataSource
        {
            Name = "R2D2 v2",
            Description = "Weeeeeeeee!",
            ProjectId = project.Id,
            OrganizationId = organizationId
        };
        await Context.DataSources.AddAsync(dataSource2);
        await Context.SaveChangesAsync();
        did2 = dataSource2.Id;

        var testClass = new Class
        {
            Name = "Darth Maul",
            Description = "My legs!",
            ProjectId = project.Id,
            OrganizationId = organizationId
        };
        await Context.Classes.AddAsync(testClass);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        var testClass2 = new Class
        {
            Name = "Test Class 2",
            Description = "Test class 2 for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Classes.Add(testClass2);
        await Context.SaveChangesAsync();
        cid2 = testClass2.Id;

        // Project 2: The Rebellion
        var rebellionProject = new Project
        {
            Name = "The Rebellion",
            Description = "Hope is like the sun",
            OrganizationId = organizationId
        };
        await Context.Projects.AddAsync(rebellionProject);
        await Context.SaveChangesAsync();
        pid2 = rebellionProject.Id;

        var rebelTag = new Tag
        {
            Name = "Alliance",
            ProjectId = pid2,
            OrganizationId = organizationId
        };
        await Context.Tags.AddAsync(rebelTag);
        await Context.SaveChangesAsync();

        var testRole = new Role
        {
            Name = "Test Role",
            Description = "Test role for unit tests",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        roleId = testRole.Id;

        var rebelDataSource = new DataSource
        {
            Name = "Yavin IV Base",
            Description = "May the Force be with you",
            ProjectId = pid2,
            OrganizationId = organizationId
        };
        await Context.DataSources.AddAsync(rebelDataSource);
        await Context.SaveChangesAsync();

        var rebelClass = new Class
        {
            Name = "Rebel Leaders",
            Description = "Leaders of the Rebellion",
            ProjectId = pid2,
            OrganizationId = organizationId
        };
        await Context.Classes.AddAsync(rebelClass);
        await Context.SaveChangesAsync();

        // Project 3: The Empire
        var empireProject = new Project
        {
            Name = "The Galactic Empire",
            Description = "Peace through power",
            OrganizationId = organizationId
        };
        await Context.Projects.AddAsync(empireProject);
        await Context.SaveChangesAsync();
        pid3 = empireProject.Id;

        var imperialTag = new Tag
        {
            Name = "Imperial Officer",
            ProjectId = pid3,
            OrganizationId = organizationId
        };
        await Context.Tags.AddAsync(imperialTag);
        await Context.SaveChangesAsync();

        var empireDataSource = new DataSource
        {
            Name = "Death Star",
            Description = "That's no moon",
            ProjectId = pid3,
            OrganizationId = organizationId
        };
        await Context.DataSources.AddAsync(empireDataSource);
        await Context.SaveChangesAsync();

        var empireClass = new Class
        {
            Name = "Imperial Command",
            Description = "High-ranking Imperial officers",
            ProjectId = pid3,
            OrganizationId = organizationId
        };
        await Context.Classes.AddAsync(empireClass);
        await Context.SaveChangesAsync();

        // Project 4: Mandalorians
        var mandoProject = new Project
        {
            Name = "Mandalorians",
            Description = "This is the Way",
            OrganizationId = organizationId
        };
        await Context.Projects.AddAsync(mandoProject);
        await Context.SaveChangesAsync();
        pid4 = mandoProject.Id;

        var mandoTag = new Tag
        {
            Name = "Bounty Hunter",
            ProjectId = pid4,
            OrganizationId = organizationId
        };
        var clanTag = new Tag
        {
            Name = "Clan Leader",
            ProjectId = pid4,
            OrganizationId = organizationId
        };
        await Context.Tags.AddAsync(mandoTag);
        await Context.Tags.AddAsync(clanTag);
        await Context.SaveChangesAsync();

        var mandoDataSource = new DataSource
        {
            Name = "Nevarro",
            Description = "Covert hideout",
            ProjectId = pid4,
            OrganizationId = organizationId
        };
        await Context.DataSources.AddAsync(mandoDataSource);
        await Context.SaveChangesAsync();

        var mandoClass = new Class
        {
            Name = "Warriors",
            Description = "Mandalorian warriors and bounty hunters",
            ProjectId = pid4,
            OrganizationId = organizationId
        };
        await Context.Classes.AddAsync(mandoClass);
        await Context.SaveChangesAsync();

        // MIXED RECORDS - Project 1 (Anakin) records using various datasources and classes
        var rex = new Record
        {
            Name = "Captain Rex",
            Description = "Clankers!",
            OriginalId = "CT-7567",
            Properties = JsonSerializer.Serialize(new { Legion = "501st" }),
            ProjectId = project.Id,
            DataSourceId = dataSource.Id, // R2D2 datasource
            ClassId = testClass.Id, // Darth Maul class
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(rex);
        await Context.SaveChangesAsync();
        rid = rex.Id;

        var hunter = new Record
        {
            Name = "Hunter",
            Description = "Omega, stop doing that",
            OriginalId = "CT-9901",
            Properties = JsonSerializer.Serialize(new { CloneForce = "99" }),
            ProjectId = project.Id,
            DataSourceId = rebelDataSource.Id, // Using Rebellion datasource!
            ClassId = testClass.Id,
            Tags = new List<Tag> { tag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(hunter);
        await Context.SaveChangesAsync();

        var tech = new Record
        {
            Name = "Tech",
            Description = "RIP",
            OriginalId = "CT-9902",
            Properties = JsonSerializer.Serialize(new { CloneForce = "99" }),
            ProjectId = project.Id,
            DataSourceId = empireDataSource.Id, // Using Empire datasource!
            ClassId = rebelClass.Id, // Using Rebel class!
            Tags = new List<Tag> { tag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(tech);
        await Context.SaveChangesAsync();

        var wrecker = new Record
        {
            Name = "Wrecker",
            Description = "Boom",
            OriginalId = "CT-9903",
            Properties = JsonSerializer.Serialize(new { CloneForce = "99" }),
            ProjectId = project.Id,
            DataSourceId = mandoDataSource.Id, // Using Mando datasource!
            ClassId = mandoClass.Id, // Using Mando class!
            Tags = new List<Tag> { tag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(wrecker);
        await Context.SaveChangesAsync();

        var crosshair = new Record
        {
            Name = "Crosshair",
            Description = "Redemption Arch",
            OriginalId = "CT-9904",
            Properties = JsonSerializer.Serialize(new { CloneForce = "99" }),
            ProjectId = project.Id,
            DataSourceId = dataSource.Id,
            ClassId = empireClass.Id, // Using Empire class!
            Tags = new List<Tag> { tag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(crosshair);
        await Context.SaveChangesAsync();

        var echo = new Record
        {
            Name = "Echo",
            Description = "Repeater",
            OriginalId = "CT-1409",
            Properties = JsonSerializer.Serialize(new { CloneForce = "99" }),
            ProjectId = project.Id,
            DataSourceId = dataSource.Id,
            ClassId = empireClass.Id, // Using Empire class!
            Tags = new List<Tag> { tag },
            Uri = "localhost:8090",
            OrganizationId = organizationId,
            IsArchived = true
        };
        await Context.Records.AddAsync(echo);
        await Context.SaveChangesAsync();

        // MIXED RECORDS - Project 2 (Rebellion) with cross-project references
        var leia = new Record
        {
            Name = "Princess Leia",
            Description = "Rebel leader and princess",
            OriginalId = "REB-001",
            Properties = JsonSerializer.Serialize(new { Homeworld = "Alderaan", Rank = "General" }),
            ProjectId = pid2,
            DataSourceId = rebelDataSource.Id,
            ClassId = rebelClass.Id,
            Tags = new List<Tag> { rebelTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(leia);
        await Context.SaveChangesAsync();

        var luke = new Record
        {
            Name = "Luke Skywalker",
            Description = "Last of the Jedi",
            OriginalId = "REB-002",
            Properties = JsonSerializer.Serialize(new { Homeworld = "Tatooine", Rank = "Commander" }),
            ProjectId = pid2,
            DataSourceId = dataSource.Id, // Using Anakin's R2D2 datasource!
            ClassId = rebelClass.Id,
            Tags = new List<Tag> { rebelTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(luke);
        await Context.SaveChangesAsync();

        var han = new Record
        {
            Name = "Han Solo",
            Description = "Smuggler turned hero",
            OriginalId = "REB-003",
            Properties = JsonSerializer.Serialize(new { Ship = "Millennium Falcon", Rank = "Captain" }),
            ProjectId = pid2,
            DataSourceId = mandoDataSource.Id, // Using Mando datasource!
            ClassId = mandoClass.Id, // Using Mando class!
            Tags = new List<Tag> { rebelTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(han);
        await Context.SaveChangesAsync();

        var wedge = new Record
        {
            Name = "Wedge Antilles",
            Description = "Best pilot in the galaxy",
            OriginalId = "REB-004",
            Properties = JsonSerializer.Serialize(new { Squadron = "Rogue Squadron", Rank = "Wing Commander" }),
            ProjectId = pid2,
            DataSourceId = empireDataSource.Id, // Using Empire datasource!
            ClassId = testClass.Id, // Using Anakin's Darth Maul class!
            Tags = new List<Tag> { rebelTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(wedge);
        await Context.SaveChangesAsync();

        var chewie = new Record
        {
            Name = "Chewbacca",
            Description = "Kiss a Wookiee",
            OriginalId = "REB-005",
            Properties = JsonSerializer.Serialize(new { Homeworld = "Kashyyk", Rank = "Co-pilot" }),
            ProjectId = pid2,
            DataSourceId = rebelDataSource.Id,
            ClassId = rebelClass.Id,
            Tags = new List<Tag> { rebelTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId,
            IsArchived = true
        };
        await Context.Records.AddAsync(chewie);
        await Context.SaveChangesAsync();

        // MIXED RECORDS - Project 3 (Empire) with cross-project references
        var vader = new Record
        {
            Name = "Darth Vader",
            Description = "I find your lack of faith disturbing",
            OriginalId = "IMP-001",
            Properties = JsonSerializer.Serialize(new { Title = "Dark Lord of the Sith", Rank = "Supreme Commander" }),
            ProjectId = pid3,
            DataSourceId = empireDataSource.Id,
            ClassId = empireClass.Id,
            Tags = new List<Tag> { imperialTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(vader);
        await Context.SaveChangesAsync();

        var tarkin = new Record
        {
            Name = "Grand Moff Tarkin",
            Description = "You may fire when ready",
            OriginalId = "IMP-002",
            Properties = JsonSerializer.Serialize(new { Title = "Grand Moff", Station = "Death Star" }),
            ProjectId = pid3,
            DataSourceId = rebelDataSource.Id, // Using Rebellion datasource!
            ClassId = rebelClass.Id, // Using Rebel class! (Infiltration?)
            Tags = new List<Tag> { imperialTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(tarkin);
        await Context.SaveChangesAsync();

        var thrawn = new Record
        {
            Name = "Grand Admiral Thrawn",
            Description = "Tactical genius",
            OriginalId = "IMP-003",
            Properties = JsonSerializer.Serialize(new { Species = "Chiss", Rank = "Grand Admiral" }),
            ProjectId = pid3,
            DataSourceId = dataSource.Id, // Using Anakin's datasource!
            ClassId = mandoClass.Id, // Using Mando class!
            Tags = new List<Tag> { imperialTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(thrawn);
        await Context.SaveChangesAsync();

        // MIXED RECORDS - Project 4 (Mandalorians) with cross-project references
        var dinDjarin = new Record
        {
            Name = "Din Djarin",
            Description = "The Mandalorian",
            OriginalId = "MANDO-001",
            Properties = JsonSerializer.Serialize(new { Armor = "Beskar", Title = "Mand'alor" }),
            ProjectId = pid4,
            DataSourceId = mandoDataSource.Id,
            ClassId = mandoClass.Id,
            Tags = new List<Tag> { mandoTag, clanTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(dinDjarin);
        await Context.SaveChangesAsync();

        var boKatan = new Record
        {
            Name = "Bo-Katan Kryze",
            Description = "Rightful ruler of Mandalore",
            OriginalId = "MANDO-002",
            Properties = JsonSerializer.Serialize(new { Clan = "Kryze", Title = "Leader of Mandalore" }),
            ProjectId = pid4,
            DataSourceId = rebelDataSource.Id, // Using Rebellion datasource!
            ClassId = rebelClass.Id, // Using Rebel class!
            Tags = new List<Tag> { clanTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(boKatan);
        await Context.SaveChangesAsync();

        var bobafett = new Record
        {
            Name = "Boba Fett",
            Description = "Like my father before me",
            OriginalId = "MANDO-003",
            Properties = JsonSerializer.Serialize(new { Ship = "Slave I", Occupation = "Daimyo" }),
            ProjectId = pid4,
            DataSourceId = empireDataSource.Id, // Using Empire datasource!
            ClassId = empireClass.Id, // Using Empire class!
            Tags = new List<Tag> { mandoTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(bobafett);
        await Context.SaveChangesAsync();

        var pazVizsla = new Record
        {
            Name = "Paz Vizsla",
            Description = "Heavy infantry",
            OriginalId = "MANDO-004",
            Properties = JsonSerializer.Serialize(new { Clan = "Vizsla", Weapon = "Heavy Blaster" }),
            ProjectId = pid4,
            DataSourceId = dataSource.Id, // Using Anakin's datasource!
            ClassId = testClass.Id, // Using Anakin's class!
            Tags = new List<Tag> { clanTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(pazVizsla);
        await Context.SaveChangesAsync();

        var projectMember = new ProjectMember
        {
            UserId = uid,
            ProjectId = pid,
            RoleId = roleId
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();
    }

    #region GetMultiProjectRecords Tests

    [Fact]
    public async Task GetMultiProjectRecords_Success_ReturnsRecordsFromMultipleProjects()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
        Assert.All(records, r => Assert.False(r.IsArchived));
    }

    [Fact]
    public async Task GetMultiProjectRecords_Success_ReturnsOnlyUnarchivedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.False(r.IsArchived));
    }

    [Fact]
    public async Task GetMultiProjectRecords_Success_ReturnsWithArchivedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, false);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.Name == "Echo");
        Assert.Contains(records, r => r.Name == "Chewbacca");
    }

    #endregion

    #region GetMultiProjectRecords_SensitivityLabel_Authorization Tests

    [Fact]
    public async Task GetMultiProjectRecords_Success_FiltersUnauthorizedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task GetMultiProjectRecords_ReturnsAuthorizedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task GetMultiProjectRecords_MultipleLabels_FiltersUnauthorizedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task GetMultiProjectRecords_MultipleLabels_ReturnsAuthorizedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null
            && readPermission != null && readPermission2 != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(uid, organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task Search_Success_FindsRecordByFullName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Captain Rex", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }


    [Fact]
    public async Task Search_Success_FindsRecordByPartialName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "capt", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByOriginalId()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "CT-9901", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByPartialDescription()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Omega", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Hunter", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByStringInProperties()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Sith", organizationId, [pid3]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Darth Vader", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsWithSpecialCharacters()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "CT-", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task Search_Success_ReturnsEmptyForNonExistentTerm()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Wookiee", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task Search_Success_RestrictsResultsToSpecifiedProject()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "the", organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.All(records, r => Assert.Equal(pid2, r.ProjectId));
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialTagName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Padme", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialTagNameCaseInsensitive()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "padme", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByTagAcrossMultipleProjects()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Bounty", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsMultipleRecordsByJsonProperties()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "99", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialOriginalId()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "CT-99", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByNumericPartialId()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "99", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialDataSourceName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Yav", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialProjectName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Rebel", organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByShortPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Bo", organizationId, [pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByCaseInsensitivePartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "CAPT", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByMultipleWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "grand adm", organizationId, [pid3]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Grand Admiral Thrawn", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByMiddleOfWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "eck", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Wrecker", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByUriPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "8090", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByBeginningOfWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Wre", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Wrecker", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsAcrossAllAccessibleProjects()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Captain", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordUsingCrossProjectResources()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Death Star", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Tech", records.First().Name);
        Assert.Equal(pid, records.First().ProjectId);
    }

    [Fact]
    public async Task Search_Success_FindsArchivedRecordByName()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Echo", organizationId, [pid], false);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Echo", records.First().Name);
    }

    [Fact]
    public async Task Search_Failure_IfEmptyString()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search(uid, "", organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    [Fact]
    public async Task Search_Failure_IfNull()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search(uid, null, organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    [Fact]
    public async Task Search_Failure_IfWhitespaceOnly()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search(uid, "     ", organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    [Fact]
    public async Task Search_ReturnsEmpty_IfRecordArchived()
    {
        // Act
        var result = await _queryBusiness.Search(uid, "Chewbacca", organizationId, [pid2], true);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    #endregion

    #region Search_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task Search_FiltersRecord_UserUnauthorized()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.Search(uid, "Captain Rex", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task Search_RecordHasLabel_UserAuthorized()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.Search(uid, "Captain Rex", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task Search_RecordHasMultipleLabels_UserUnauthorized()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.Search(uid, "Captain Rex", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task Search_RecordHasMultipleLabels_UserAuthorized()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null
            && readPermission != null && readPermission2 != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.Search(uid, "Captain Rex", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    #endregion

    #region QueryBuilder Tests

    [Fact]
    public async Task QueryBuilderWithNullFiltersThrowsException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _queryBusiness.QueryBuilder(uid, null, organizationId, new[] { pid }));

        Assert.Contains("Custom query request dto cannot be null", exception.Message);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByEqualityOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Tech", records.First().Name);
    }




    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByDateEqualityOperator()
    {
        // Arrange
        // Grab the last_updated_at date from the already-seeded "Tech" record
        var techRecord = await Context.Records.FirstAsync(r => r.Name == "Tech");
        var targetDate = techRecord.LastUpdatedAt.Date;

        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "last_updated_at",
            Operator = "=",
            Value = targetDate.ToString("yyyy-MM-dd")
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert - all returned records should fall within that day
        Assert.NotEmpty(records);
        Assert.All(records, r =>
        {
            Assert.True(r.LastUpdatedAt >= targetDate);
            Assert.True(r.LastUpdatedAt < targetDate.AddDays(1));
        });
    }

    [Fact]
    public async Task QueryBuilder_Success_ExcludesRecordsOutsideDateRange()
    {
        // Arrange - use a date far in the past that no seeded records fall on
        var emptyDate = DateTime.Today.AddYears(-10);

        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "last_updated_at",
            Operator = "=",
            Value = emptyDate.ToString("yyyy-MM-dd")
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryBuilder_Success_ExcludesArchivedRecords()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "Echo"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_AllRecordsExcludesArchivedRecords()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "project_name",
            Operator = "LIKE",
            Value = "rebellion"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid, pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
        Assert.DoesNotContain(records, r => r.Name == "Chewbacca");
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByLikeOperatorCaseInsensitive()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "LIKE",
            Value = "tech"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Tech", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByGreaterThanDateOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "last_updated_at",
            Operator = ">",
            Value = DateTime.Now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
        Assert.All(records, r => Assert.True(r.LastUpdatedAt > DateTime.Now.AddMinutes(-30)));
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByDateRangeRetry()
    {
        // Arrange
        var ahsoka = new Record
        {
            Name = "Ahsoka Tano",
            Description = "Favorite",
            OriginalId = "Snips",
            Properties = JsonSerializer.Serialize(new { Jedi = "Apprentice" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };
        await Context.Records.AddAsync(ahsoka);
        await Context.SaveChangesAsync();

        var baselineAhsoka = ahsoka.LastUpdatedAt.AddMinutes(10);
        var baselineRex = (await Context.Records.FindAsync(rid)).LastUpdatedAt;

        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "last_updated_at",
            Operator = ">",
            Value = baselineRex.ToString("yyyy-MM-dd HH:mm:ss")
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "last_updated_at",
            Operator = "<",
            Value = baselineAhsoka.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(6, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByMultipleAndConditions()
    {
        // Arrange
        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "data_source_name",
            Operator = "LIKE",
            Value = "R2D2"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "original_id",
            Operator = "LIKE",
            Value = "CT-7567"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByOrCondition()
    {
        // Arrange
        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Wrecker"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByMixedNullAndOrConditions()
    {
        // Arrange
        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByMixedAndOrConditions()
    {
        // Arrange
        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "LIKE",
            Value = "Anakin"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByCombinedQueryAndSearchTerm()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "data_source_name",
            Operator = "LIKE",
            Value = "R2D2"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid], "Captain");
        var records = result.ToList();

        // Assert
        Assert.Single(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByContainsOperatorInDescription()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "description",
            Operator = "LIKE",
            Value = "stop"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Hunter", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByOriginalIdPrefix()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "original_id",
            Operator = "LIKE",
            Value = "CT-99"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByMultipleProjectIds()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "a"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid, pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(6, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByProjectNameFirst()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "LIKE",
            Value = "Rebellion"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByProjectNameSecond()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "=",
            Value = "The Galactic Empire"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid3]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByProjectNameThird()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "LIKE",
            Value = "Mandalorians"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByUserAccessToSpecificProjectsOnly()
    {
        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [], organizationId, [pid, pid3]);
        var records = result.ToList();

        // Assert
        Assert.Equal(8, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsInProjectWithCrossProjectResources()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "=",
            Value = "Anakin"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByDataSourceAcrossAllowedProjects()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "data_source_name",
            Operator = "LIKE",
            Value = "Yavin"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_ReturnsEmptyWhenNoProjectAccess()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "a"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, []);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByOriginalIdPrefixWithProjectAccess()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "original_id",
            Operator = "LIKE",
            Value = "REB-"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByComplexQueryWithLimitedProjectAccess()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "original_id",
            Operator = "LIKE",
            Value = "CT-"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid, pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByMultipleProjectsWithOrCondition()
    {
        // Arrange
        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "project_name",
            Operator = "=",
            Value = "Anakin"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "project_name",
            Operator = "=",
            Value = "The Galactic Empire"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2], organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(8, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByKeyValueSearch()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "KEY_VALUE",
            Json = JsonSerializer.Serialize(new { Legion = "501st" })
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByKeyValueSearchMultipleResults()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "KEY_VALUE",
            Json = JsonSerializer.Serialize(new { CloneForce = "99" })
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByLikeOperatorOnPropertiesJsonb()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "LIKE",
            Value = "501"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByPartialMatchWithLikeOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "Prin" // Partial match for "Princess Leia"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Princess Leia", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByPartialMatchOnOriginalId()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "original_id",
            Operator = "LIKE",
            Value = "MANDO-00" // Should find all Mandalorian records
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FindsSpecificRecordWithDataSourceAndSearchTerm()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "data_source_name",
            Operator = "LIKE",
            Value = "R2D2"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid], "CT-7567");
        var records = result.ToList();

        // Assert
        Assert.Single(records);
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForInvalidFilterField()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "InvalidField",
            Operator = "=",
            Value = "test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForInvalidOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "INVALID",
            Value = "test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForInvalidDateFormat()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "last_updated_at",
            Operator = ">",
            Value = "invalid-date"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForNullValue()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForEmptyValue()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND",
            Filter = "name",
            Operator = "=",
            Value = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]));
    }

    #endregion

    #region QueryBuilder_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task QueryBuilder_FilterOutRecordWithInaccessibleLabel_ReturnsOnlyAccessibleRecords()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task QueryBuilder_RecordHasLabel_UserAuthorized_RetrievesRecord()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task QueryBuilder_RecordHasMultipleLabels_UserAuthorizedSingleLabel_FiltersRecord()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task QueryBuilder_RecordHasMultipleLabels_UserAuthorizedAllLabels_ReturnsRecord()
    {
        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Confidential",
            Description = "Very Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);
        Context.ChangeTracker.Clear();

        // Give user read and write permission to attach and retrieve label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        if (writePermission != null && writePermission2 != null && readPermission != null && readPermission2 != null)
        {
            var role = await Context.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Id == roleId);

            if (role != null)
            {
                // Attach the permissions first
                Context.Attach(writePermission);
                Context.Attach(writePermission2);
                Context.Attach(readPermission);
                Context.Attach(readPermission2);

                role.Permissions.Add(writePermission);
                role.Permissions.Add(writePermission2);
                role.Permissions.Add(readPermission);
                role.Permissions.Add(readPermission2);
                await Context.SaveChangesAsync();
            }
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        var dto1 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "name",
            Operator = "LIKE",
            Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR",
            Filter = "name",
            Operator = "=",
            Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder(uid, [dto1, dto2, dto3], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    #endregion

    #region QueryBuilder JSONB Tag LIKE Tests

    [Fact]
    public async Task QueryBuilder_Success_TagLikeSingleCharA_ReturnsOnlyRecordsWhoseTagNameContainsA()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "a"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Equal(4, records.Count);
        Assert.All(records, r => Assert.Contains(r.Name, new[] { "Hunter", "Tech", "Wrecker", "Crosshair" }));
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikeSingleCharD_ReturnsOnlyRecordsWhoseTagNameContainsD()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "d"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Equal(4, records.Count);
        Assert.All(records, r => Assert.Contains(r.Name, new[] { "Hunter", "Tech", "Wrecker", "Crosshair" }));
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikeNumericChar_DoesNotMatchOnTagIdValue()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "6"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikeCharWithNoMatches_ReturnsEmpty()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "z"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikeCharZ_MatchesAcrossProjectsOnTagNameOnly()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "z"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid4]);
        var records = result.ToList();

        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikePartialWord_MatchesCorrectRecords()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "hunt"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid4]);
        var records = result.ToList();

        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Contains(r.Name, new[] { "Din Djarin", "Boba Fett" }));
    }

    [Fact]
    public async Task QueryBuilder_Success_TagLikeFullTagName_ReturnsAllRecordsWithThatTag()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "tags",
            Operator = "LIKE",
            Value = "Alliance"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        Assert.Equal(4, records.Count);
        Assert.All(records,
            r => Assert.Contains(r.Name, new[] { "Princess Leia", "Luke Skywalker", "Han Solo", "Wedge Antilles" }));
    }

    #endregion

    #region QueryBuilder JSONB Properties LIKE Tests

    [Fact]
    public async Task QueryBuilder_Success_PropertiesLike_DoesNotMatchOnKeyNames()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "LIKE",
            Value = "Rank"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryBuilder_Success_PropertiesLike_MatchesOnValueNotKey()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "LIKE",
            Value = "General"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid2]);
        var records = result.ToList();

        Assert.Single(records);
        Assert.Equal("Princess Leia", records[0].Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_PropertiesLike_MatchesPartialValue()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "LIKE",
            Value = "501"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_PropertiesLike_ValueSharedAcrossMultipleRecords()
    {
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = null,
            Filter = "properties",
            Operator = "LIKE",
            Value = "99"
        };

        var result = await _queryBusiness.QueryBuilder(uid, [dto], organizationId, [pid]);
        var records = result.ToList();

        Assert.Equal(4, records.Count);
        Assert.All(records, r => Assert.Contains(r.Name, new[] { "Hunter", "Tech", "Wrecker", "Crosshair" }));
    }

    #endregion

    #region GetRecentlyAddedRecords Tests

    [Fact]
    public async Task GetRecentlyAddedRecords_ReturnsRecords_ForUserProjects()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert
        Assert.Equal(9, records.Count); // 5 from pid + 4 from pid2
        // Check for records from Project 1 (Anakin)
        Assert.Contains(records, r => r.Name == "Captain Rex");
        Assert.Contains(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
        // Check for records from Project 2 (Rebellion)
        Assert.Contains(records, r => r.Name == "Princess Leia");
        Assert.Contains(records, r => r.Name == "Luke Skywalker");
        Assert.Contains(records, r => r.Name == "Han Solo");
        Assert.Contains(records, r => r.Name == "Wedge Antilles");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_ExcludesArchivedRecords()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - Archived records should not appear
        Assert.All(records, r => Assert.False(r.IsArchived));
        Assert.DoesNotContain(records, r => r.Name == "Echo");
        Assert.DoesNotContain(records, r => r.Name == "Chewbacca");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_ReturnsEmpty_WhenEmptyProjectArray()
    {
        // Arrange
        var projectIds = new long[] { };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    #endregion

    #region GetRecentlyAddedRecords_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task GetRecentlyAddedRecords_FilterOutRecordWithInaccessibleLabel_ReturnsOnlyAccessibleRecords()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        if (role != null && writePermission != null)
        {
            var permissionToRemove = role.Permissions.FirstOrDefault(p => p.Id == writePermission.Id);
            if (permissionToRemove != null)
            {
                role.Permissions.Remove(permissionToRemove);
                await Context.SaveChangesAsync();
            }
        }

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - Captain Rex should be filtered out due to lack of label access
        Assert.Equal(4, records.Count);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
        Assert.Contains(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_UserHasReadAccessToLabel_ReturnsAllRecords()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);

        Context.ChangeTracker.Clear();

        // Act - User still has write permission
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - All records including Captain Rex should be returned
        Assert.Equal(5, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
        Assert.Contains(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_NoLabelsAttached_ReturnsAllRecords()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Act - No labels attached to any records
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - All records should be returned when no labels are present
        Assert.Equal(5, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
        Assert.Contains(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_RecordWithMultipleLabels_UserHasAccessToAll_ReturnsRecord()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Create ProjectMember to link user to project with a role
        var projectMember = new ProjectMember
        {
            UserId = uid,
            ProjectId = pid,
            RoleId = roleId
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label"
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label"
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && readPermission1 != null)
        {
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label1 to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null && readPermission2 != null)
        {
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label2 to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Act - User has access to both labels
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - Captain Rex should be returned since user has access to all labels
        Assert.Equal(5, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_RecordWithMultipleLabels_UserMissingAccessToOne_FiltersOutRecord()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Create ProjectMember to link user to project with a role
        var projectMember = new ProjectMember
        {
            UserId = uid,
            ProjectId = pid,
            RoleId = roleId
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label"
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label"
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label1 to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2 (temporarily to attach it)
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label2 to Captain Rex
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for label2 (user only has access to label1)
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            var permissionToRemove = role.Permissions.FirstOrDefault(p => p.Id == writePermission2.Id);
            if (permissionToRemove != null)
            {
                role.Permissions.Remove(permissionToRemove);
                await Context.SaveChangesAsync();
            }
        }

        Context.ChangeTracker.Clear();

        // Act - User lacks access to label2
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - Captain Rex should be filtered out (user must have access to ALL labels)
        Assert.Equal(4, records.Count);
        Assert.DoesNotContain(records, r => r.Name == "Captain Rex");
        Assert.Contains(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_MultipleRecordsWithDifferentLabels_FiltersCorrectly()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Create ProjectMember to link user to project with a role
        var projectMember = new ProjectMember
        {
            UserId = uid,
            ProjectId = pid,
            RoleId = roleId
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "AccessibleLabel_" + Guid.NewGuid(),
            Description = "Label user has access to"
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "RestrictedLabel_" + Guid.NewGuid(),
            Description = "Label user doesn't have access to"
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && readPermission1 != null)
        {
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach accessible label to Captain Rex (rid)
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label (temporarily to attach)
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Get Hunter's record ID and attach restricted label
        var hunterRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Name == "Hunter" && r.ProjectId == pid);

        await _recordBusiness.AttachLabel(uid, organizationId, pid, hunterRecord.Id, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for restricted label
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            var permissionToRemove = role.Permissions.FirstOrDefault(p => p.Id == writePermission2.Id);
            if (permissionToRemove != null)
            {
                role.Permissions.Remove(permissionToRemove);
                await Context.SaveChangesAsync();
            }
        }

        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(uid, organizationId, projectIds);
        var records = result.ToList();

        // Assert - Captain Rex (accessible) should be included, Hunter (restricted) should be filtered out
        Assert.Equal(4, records.Count);
        Assert.Contains(records, r => r.Name == "Captain Rex");
        Assert.DoesNotContain(records, r => r.Name == "Hunter");
        Assert.Contains(records, r => r.Name == "Tech");
        Assert.Contains(records, r => r.Name == "Wrecker");
        Assert.Contains(records, r => r.Name == "Crosshair");
    }

    #endregion

    #region GetRecordsPaginated Tests

    [Fact]
    public async Task GetRecordsPaginated_ReturnsEmpty_WhenNoProjectIdsProvided()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, []);

        // Assert
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetRecordsPaginated_ReturnsRecords_ForSingleProject()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid]);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, r => Assert.Equal(pid, r.ProjectId));
    }

    [Fact]
    public async Task GetRecordsPaginated_ReturnsRecords_ForMultipleProjects()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid, pid2]);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, r => r.ProjectId == pid);
        Assert.Contains(result.Items, r => r.ProjectId == pid2);
    }

    [Fact]
    public async Task GetRecordsPaginated_ExcludesArchivedRecords()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid, pid2]);

        // Assert
        Assert.All(result.Items, r => Assert.False(r.IsArchived));
        Assert.DoesNotContain(result.Items, r => r.Name == "Echo");
        Assert.DoesNotContain(result.Items, r => r.Name == "Chewbacca");
    }

    [Fact]
    public async Task GetRecordsPaginated_SortByNameAZ_ReturnsSortedAscending()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid]);
        var names = result.Items.Select(r => r.Name).ToList();

        // Assert
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public async Task GetRecordsPaginated_SortByNameZA_ReturnsSortedDescending()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameZA,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid]);
        var names = result.Items.Select(r => r.Name).ToList();

        // Assert
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    [Fact]
    public async Task GetRecordsPaginated_SortByDateNew_ReturnsMostRecentFirst()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.DateNew,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid]);
        var dates = result.Items.Select(r => r.LastUpdatedAt).ToList();

        // Assert
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    [Fact]
    public async Task GetRecordsPaginated_SortByDateOld_ReturnsOldestFirst()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.DateOld,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 25 }, [pid]);
        var dates = result.Items.Select(r => r.LastUpdatedAt).ToList();

        // Assert
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
    }

    [Fact]
    public async Task GetRecordsPaginated_Pagination_ReturnsCorrectPage()
    {
        // Arrange - pid has 5 non-archived records; fetch page 2 with 2 per page
        var page1 = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 2 }, [pid]);
        var page2 = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 2, PageSize = 2 }, [pid]);

        // Assert
        Assert.Equal(2, page1.Items.Count());
        Assert.Equal(2, page2.Items.Count());
        Assert.Empty(page1.Items.Select(r => r.Id).Intersect(page2.Items.Select(r => r.Id)));
    }

    [Fact]
    public async Task GetRecordsPaginated_Pagination_TotalCountIsCorrect()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 2 }, [pid]);

        // Assert - 5 non-archived records in pid
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetRecordsPaginated_Pagination_LastPageHasRemainder()
    {
        // pid has 5 non-archived records; page 3 with 2 per page should have 1 record
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 3, PageSize = 2 }, [pid]);

        // Assert
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetRecordsPaginated_Pagination_BeyondLastPageReturnsEmpty()
    {
        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 99, PageSize = 10 }, [pid]);

        // Assert
        Assert.Empty(result.Items);
    }

    #endregion

    #region GetRecordsPaginated_SensitivityLabel_Authorization Tests

    [Fact]
    public async Task GetRecordsPaginated_FiltersUnauthorizedRecord()
    {
        // Arrange
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid]);

        // Assert
        Assert.DoesNotContain(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(4, result.Items.Count());
    }

    [Fact]
    public async Task GetRecordsPaginated_ReturnsAuthorizedRecord_WhenUserHasReadPermission()
    {
        // Arrange
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label"
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid]);

        // Assert
        Assert.Contains(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(5, result.Items.Count());
    }

    [Fact]
    public async Task GetRecordsPaginated_MultipleLabels_FiltersWhenUserMissingOneLabel()
    {
        // Arrange
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Label A", Description = "A" }, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Label B", Description = "B" }, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");
        var writePermission2 = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");
        var readPermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");
        // Intentionally omit readPermission2

        var role = await Context.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null && readPermission != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid]);

        // Assert - user lacks read on label2, so Captain Rex is filtered
        Assert.DoesNotContain(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(4, result.Items.Count());
    }

    [Fact]
    public async Task GetRecordsPaginated_MultipleLabels_ReturnsRecordWhenUserHasAllPermissions()
    {
        // Arrange
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Label A", Description = "A" }, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Label B", Description = "B" }, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");
        var writePermission2 = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");
        var readPermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");
        var readPermission2 = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        var role = await Context.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && writePermission2 != null
            && readPermission != null && readPermission2 != null)
        {
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(readPermission);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label2.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid]);

        // Assert
        Assert.Contains(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(5, result.Items.Count());
    }

    #endregion

    #region GetRecordsPaginated_AdminBypass Tests

    [Fact]
    public async Task GetRecordsPaginated_SysAdmin_BypassesSensitivityLabelFilter()
    {
        // Arrange - attach a label that the user has no read permission for
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Restricted", Description = "Restricted" }, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        Context.ChangeTracker.Clear();

        // Act - isSysAdmin bypasses label check
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid], isSysAdmin: true);

        // Assert - Captain Rex is included despite no read permission
        Assert.Contains(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(5, result.Items.Count());
    }

    [Fact]
    public async Task GetRecordsPaginated_OrgAdmin_BypassesSensitivityLabelFilter()
    {
        // Arrange
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Restricted", Description = "Restricted" }, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid], isOrgAdmin: true);

        // Assert
        Assert.Contains(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(5, result.Items.Count());
    }

    [Fact]
    public async Task GetRecordsPaginated_ProjectAdmin_BypassesSensitivityLabelFilter()
    {
        // Arrange
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid,
            new CreateSensitivityLabelRequestDto { Name = "Restricted", Description = "Restricted" }, pid, organizationId);
        Context.ChangeTracker.Clear();

        var writePermission = await Context.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, label.Id);
        Context.ChangeTracker.Clear();

        // Act
        var result = await _queryBusiness.GetRecordsPaginated(uid, organizationId, SortRecordsRequestDto.NameAZ,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 10 }, [pid], isProjectAdmin: true);

        // Assert
        Assert.Contains(result.Items, r => r.Name == "Captain Rex");
        Assert.Equal(5, result.Items.Count());
    }

    #endregion
}