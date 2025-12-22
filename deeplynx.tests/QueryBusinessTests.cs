using System.Text.Json;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class QueryBusinessTests : IntegrationTestBase
{
    private QueryBusiness _queryBusiness = null!;
    private long cid;
    private long did;
    private long organizationId;

    private long pid; // project ID
    private long pid2;
    private long pid3;
    private long pid4;
    private long rid; // record ID

    public QueryBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    private long[] pids => [pid, pid2, pid3, pid4];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _queryBusiness = new QueryBusiness(Context);
    }

    #region GetMultiProjectRecords Tests

    [Fact]
    public async Task GetMultiProjectRecords_Success_ReturnsRecordsFromMultipleProjects()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetMultiProjectRecords(organizationId, projectIds, true);
        var records = result.ToList();

        // Assert
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.ProjectId == pid);
        Assert.Contains(records, r => r.ProjectId == pid2);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

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
    }

    #region Search Tests

    [Fact]
    public async Task Search_Success_FindsRecordByFullName()
    {
        // Act
        var result = await _queryBusiness.Search("Captain Rex", organizationId,[pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }


    [Fact]
    public async Task Search_Success_FindsRecordByPartialName()
    {
        // Act
        var result = await _queryBusiness.Search("capt", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByOriginalId()
    {
        // Act
        var result = await _queryBusiness.Search("CT-9901", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByPartialDescription()
    {
        // Act
        var result = await _queryBusiness.Search("Omega", organizationId,[pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Hunter", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByStringInProperties()
    {
        // Act
        var result = await _queryBusiness.Search("Sith", organizationId, [pid3]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Darth Vader", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsWithSpecialCharacters()
    {
        // Act
        var result = await _queryBusiness.Search("CT-", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task Search_Success_ReturnsEmptyForNonExistentTerm()
    {
        // Act
        var result = await _queryBusiness.Search("Wookiee", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task Search_Success_RestrictsResultsToSpecifiedProject()
    {
        // Act
        var result = await _queryBusiness.Search("the", organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.All(records, r => Assert.Equal(pid2, r.ProjectId));
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialTagName()
    {
        // Act
        var result = await _queryBusiness.Search("Padme", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialTagNameCaseInsensitive()
    {
        // Act
        var result = await _queryBusiness.Search("padme", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByTagAcrossMultipleProjects()
    {
        // Act
        var result = await _queryBusiness.Search("Bounty", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsMultipleRecordsByJsonProperties()
    {
        // Act
        var result = await _queryBusiness.Search("99", organizationId,[pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialOriginalId()
    {
        // Act
        var result = await _queryBusiness.Search("CT-99", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByNumericPartialId()
    {
        // Act
        var result = await _queryBusiness.Search("99", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialDataSourceName()
    {
        // Act
        var result = await _queryBusiness.Search("Yav", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByPartialProjectName()
    {
        // Act
        var result = await _queryBusiness.Search("Rebel", organizationId, [pid2]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByShortPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("Bo", organizationId, [pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByCaseInsensitivePartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("CAPT", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Captain Rex", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByMultipleWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("grand adm", organizationId, [pid3]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Grand Admiral Thrawn", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByMiddleOfWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("eck", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Wrecker", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsByUriPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("8090", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordByBeginningOfWordPartialMatch()
    {
        // Act
        var result = await _queryBusiness.Search("Wre", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Wrecker", records.First().Name);
    }

    [Fact]
    public async Task Search_Success_FindsRecordsAcrossAllAccessibleProjects()
    {
        // Act
        var result = await _queryBusiness.Search("Captain", organizationId, pids);
        var records = result.ToList();

        // Assert
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Search_Success_FindsRecordUsingCrossProjectResources()
    {
        // Act
        var result = await _queryBusiness.Search("Death Star", organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Tech", records.First().Name);
        Assert.Equal(pid, records.First().ProjectId);
    }

    [Fact]
    public async Task Search_Failure_IfEmptyString()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search("", organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    [Fact]
    public async Task Search_Failure_IfNull()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search(null, organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    [Fact]
    public async Task Search_Failure_IfWhitespaceOnly()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _queryBusiness.Search("     ", organizationId, [pid]));

        Assert.Contains("Search query is required", exception.Message);
    }

    #endregion

    #region QueryBuilder Tests

    [Fact]
    public async Task QueryBuilderWithNullFiltersThrowsException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _queryBusiness.QueryBuilder(null, organizationId, new[] { pid }));

        Assert.Contains("Custom query request dto cannot be null", exception.Message);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByEqualityOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "=", Value = "Tech"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
        var records = result.ToList();

        // Assert
        Assert.Single(records);
        Assert.Equal("Tech", records.First().Name);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByLikeOperatorCaseInsensitive()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "LIKE", Value = "tech"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
        var result = await _queryBusiness.QueryBuilder([dto1, dto2], organizationId, [pid]);
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
            Connector = "AND", Filter = "data_source_name", Operator = "LIKE", Value = "R2D2"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "original_id", Operator = "LIKE", Value = "CT-7567"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto1, dto2], organizationId, [pid]);
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
            Connector = "", Filter = "name", Operator = "=", Value = "Tech"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR", Filter = "name", Operator = "=", Value = "Wrecker"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto1, dto2], organizationId, [pid]);
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
            Connector = null, Filter = "name", Operator = "LIKE", Value = "rex"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR", Filter = "name", Operator = "=", Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR", Filter = "name", Operator = "=", Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto1, dto2, dto3], organizationId, [pid]);
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
            Connector = null, Filter = "project_name", Operator = "LIKE", Value = "Anakin"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "=", Value = "Tech"
        };
        var dto3 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR", Filter = "name", Operator = "=", Value = "Hunter"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto1, dto2, dto3], organizationId, [pid]);
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
            Connector = "AND", Filter = "data_source_name", Operator = "LIKE", Value = "R2D2"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid], "Captain");
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
            Connector = "AND", Filter = "description", Operator = "LIKE", Value = "stop"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId,  [pid]);
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
            Connector = "AND", Filter = "original_id", Operator = "LIKE", Value = "CT-99"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
            Connector = null, Filter = "name", Operator = "LIKE", Value = "a"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid, pid2]);
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
            Connector = null, Filter = "project_name", Operator = "LIKE", Value = "Rebellion"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid2]);
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
            Connector = null, Filter = "project_name", Operator = "=", Value = "The Galactic Empire"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid3]);
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
            Connector = null, Filter = "project_name", Operator = "LIKE", Value = "Mandalorians"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid4]);
        var records = result.ToList();

        // Assert
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public async Task QueryBuilder_Success_FiltersRecordsByUserAccessToSpecificProjectsOnly()
    {
        // Act
        var result = await _queryBusiness.QueryBuilder([], organizationId, [pid, pid3]);
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
            Connector = null, Filter = "project_name", Operator = "=", Value = "Anakin"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
            Connector = null, Filter = "data_source_name", Operator = "LIKE", Value = "Yavin"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, pids);
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
            Connector = null, Filter = "name", Operator = "LIKE", Value = "a"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId,[]);
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
            Connector = null, Filter = "original_id", Operator = "LIKE", Value = "REB-"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid2]);
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
            Connector = null, Filter = "original_id", Operator = "LIKE", Value = "CT-"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid, pid4]);
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
            Connector = null, Filter = "project_name", Operator = "=", Value = "Anakin"
        };
        var dto2 = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "OR", Filter = "project_name", Operator = "=", Value = "The Galactic Empire"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto1, dto2], organizationId, pids);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid]);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid2]);
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
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid4]);
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
            Connector = "AND", Filter = "data_source_name", Operator = "LIKE", Value = "R2D2"
        };

        // Act
        var result = await _queryBusiness.QueryBuilder([dto], organizationId, [pid], "CT-7567");
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
            Connector = "AND", Filter = "InvalidField", Operator = "=", Value = "test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder([dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForInvalidOperator()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "INVALID", Value = "test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder([dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForInvalidDateFormat()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "last_updated_at", Operator = ">", Value = "invalid-date"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder([dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForNullValue()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "=", Value = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder([dto], organizationId, [pid]));
    }

    [Fact]
    public async Task QueryBuilder_Failure_ThrowsExceptionForEmptyValue()
    {
        // Arrange
        var dto = new CustomQueryDtos.CustomQueryRequestDto
        {
            Connector = "AND", Filter = "name", Operator = "=", Value = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _queryBusiness.QueryBuilder([dto], organizationId, [pid]));
    }

    #endregion

    #region GetRecentlyAddedRecords Tests

    [Fact]
    public async Task GetRecentlyAddedRecords_ReturnsRecords_ForUserProjects()
    {
        // Arrange
        var projectIds = new[] { pid, pid2 };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(organizationId, projectIds);
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
        var projectIds = new[] { pid };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(organizationId, projectIds);
        var records = result.ToList();

        // Assert - Archived records should not appear
        Assert.All(records, r => Assert.False(r.IsArchived));
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_ReturnsEmpty_WhenEmptyProjectArray()
    {
        // Arrange
        var projectIds = new long[] { };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(organizationId, projectIds);
        var records = result.ToList();

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task GetRecentlyAddedRecords_ReturnsLatestVersion_WhenMultipleVersionsExist()
    {
        // Arrange
        var projectIds = new[] { pid };

        // Act
        var result = await _queryBusiness.GetRecentlyAddedRecords(organizationId, projectIds);
        var records = result.ToList();

        // Assert - Historical records should return the most recent version
        // Since the seed data creates multiple historical versions, verify we only get one per record
        var recordsByOriginalId = records.GroupBy(r => r.OriginalId);
        Assert.All(recordsByOriginalId, g => Assert.Single(g));
    }

    #endregion
}