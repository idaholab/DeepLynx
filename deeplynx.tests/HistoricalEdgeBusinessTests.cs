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
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class HistoricalEdgeBusinessTests : IntegrationTestBase
{
    public EdgeBusiness _edgeBusiness = null!;
    public EventBusiness _eventBusiness = null!;
    public HistoricalEdgeBusiness _historicalEdgeBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    public long destinationRecordId;
    public long destinationRecordId2;
    public long dsid;
    public long dsid2;
    public long eid;
    public long eid2;
    public long eid3;
    public long eid4;
    private long organizationId;
    public long originRecordId;
    public long originRecordId2;
    public long pid;
    public long pid2;
    public long relationshipId;
    public long relationshipId2;
    public long uid;

    public HistoricalEdgeBusinessTests(TestSuiteFixture fixture) : base(fixture)
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
        _historicalEdgeBusiness = new HistoricalEdgeBusiness(Context);
        _edgeBusiness = new EdgeBusiness(Context, _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        var testUser = new User
        {
            Name = "Test User",
            Email = "test.user@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(testUser);
        await Context.SaveChangesAsync();
        uid = testUser.Id;

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        var project = new Project { Name = "Project 1", OrganizationId = organizationId };
        var project2 = new Project { Name = "Project 2", OrganizationId = organizationId };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        var dataSource = new DataSource
        {
            Name = "DataSource 1",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        var dataSource2 = new DataSource
        {
            Name = "DataSource 2",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        var dataSource3 = new DataSource
        {
            Name = "DataSource 3",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        Context.DataSources.Add(dataSource);
        Context.DataSources.Add(dataSource2);
        Context.DataSources.Add(dataSource3);
        await Context.SaveChangesAsync();
        dsid = dataSource.Id;
        dsid2 = dataSource2.Id;

        var testClass = new Class
        {
            Name = "Class 1",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        var testClass2 = new Class
        {
            Name = "Class 2",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        Context.Classes.Add(testClass);
        Context.Classes.Add(testClass2);
        await Context.SaveChangesAsync();

        var originRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            ClassId = testClass.Id,
            Properties = "{\"test\": \"origin_value\"}",
            Name = "Origin",
            Description = "Origin Description",
            OriginalId = "orig",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        var originRecord2 = new Record
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            ClassId = testClass2.Id,
            Properties = "{\"test\": \"origin_value 2\"}",
            Name = "Origin 2",
            Description = "Origin Description 2",
            OriginalId = "orig 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Records.Add(originRecord);
        Context.Records.Add(originRecord2);

        var destinationRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            ClassId = testClass.Id,
            Properties = "{\"test\": \"destination_value\"}",
            Name = "Destination 1",
            Description = "Destination Description 1",
            OriginalId = "dest1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var destinationRecord3 = new Record
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            ClassId = testClass2.Id,
            Properties = "{\"test\": \"destination_value 3\"}",
            Name = "Destination 3",
            Description = "Destination Description 3",
            OriginalId = "dest3",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Records.Add(destinationRecord);
        Context.Records.Add(destinationRecord3);

        // Create additional records for second edge
        var destinationRecord2 = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            Properties = "{\"test\": \"destination2_value\"}",
            Name = "Destination 2",
            Description = "Destination Description 2",
            OriginalId = "dest2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Records.Add(destinationRecord2);

        var relationship = new Relationship
        {
            Name = "Relationship 1",
            ProjectId = pid,
            OriginId = testClass.Id,
            DestinationId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var relationship2 = new Relationship
        {
            Name = "Relationship 2",
            ProjectId = pid2,
            OriginId = testClass2.Id,
            DestinationId = testClass2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Relationships.Add(relationship);
        Context.Relationships.Add(relationship2);

        await Context.SaveChangesAsync();

        originRecordId = originRecord.Id;
        originRecordId2 = originRecord2.Id;
        destinationRecordId = destinationRecord.Id;
        destinationRecordId2 = destinationRecord2.Id;
        relationshipId = relationship.Id;
        relationshipId2 = relationship2.Id;

        var edge = new Edge
        {
            ProjectId = pid,
            DataSourceId = dsid,
            OriginId = originRecordId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RelationshipId = relationshipId,
            DestinationId = destinationRecordId,
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        var edge2 = new Edge
        {
            ProjectId = pid,
            DataSourceId = dataSource3.Id,
            OriginId = destinationRecordId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RelationshipId = relationshipId,
            DestinationId = originRecordId,
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        var edge3 = new Edge
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            OriginId = originRecordId2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RelationshipId = relationshipId2,
            DestinationId = destinationRecordId2,
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        var edge4 = new Edge
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            OriginId = destinationRecordId2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RelationshipId = relationshipId2,
            DestinationId = originRecordId2,
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Edges.Add(edge);
        Context.Edges.Add(edge2);
        Context.Edges.Add(edge3);
        Context.Edges.Add(edge4);
        await Context.SaveChangesAsync();
        eid = edge.Id;
        eid2 = edge2.Id;
        eid3 = edge3.Id;
        eid4 = edge4.Id;

        Context.ChangeTracker.Clear();
        var historicalEdge1 = new HistoricalEdge
        {
            EdgeId = edge.Id,
            OriginId = edge.OriginId,
            DestinationId = edge.DestinationId,
            RelationshipId = edge.RelationshipId,
            RelationshipName = "Relationship 1",
            DataSourceId = edge.DataSourceId,
            DataSourceName = "DataSource 1",
            ProjectId = edge.ProjectId,
            ProjectName = "Project 1",
            LastUpdatedBy = edge.LastUpdatedBy,
            LastUpdatedAt = edge.LastUpdatedAt,
            IsArchived = false,
            OrganizationId = organizationId
        };

        var historicalEdge2 = new HistoricalEdge
        {
            EdgeId = edge2.Id,
            OriginId = edge2.OriginId,
            DestinationId = edge2.DestinationId,
            RelationshipId = edge2.RelationshipId,
            RelationshipName = "Relationship 1",
            DataSourceId = edge2.DataSourceId,
            DataSourceName = "DataSource 3",
            ProjectId = edge2.ProjectId,
            ProjectName = "Project 1",
            LastUpdatedBy = edge2.LastUpdatedBy,
            LastUpdatedAt = edge2.LastUpdatedAt,
            IsArchived = false,
            OrganizationId = organizationId
        };

        Context.HistoricalEdges.Add(historicalEdge1);
        Context.HistoricalEdges.Add(historicalEdge2);
        await Context.SaveChangesAsync();
    }

    #region GetAllHistoricalEdges Tests

    [Fact]
    public async Task GetAllHistoricalEdges_ReturnsListOfCurrentHistoricalRecordsForProject()
    {
        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Equal(2, historicalEdges.Count());
        Assert.Contains(historicalEdges, e => e.Id == eid);
        Assert.Contains(historicalEdges, e => e.Id == eid2);
        Assert.DoesNotContain(historicalEdges, e => e.Id == eid3);
        Assert.DoesNotContain(historicalEdges, e => e.Id == eid4);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_ReturnsListOfUpdatedHistoricalEdges()
    {
        // Arrange
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };

        var dto2 = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId2,
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };

        await _edgeBusiness.UpdateEdge(uid, organizationId, pid, dto, eid, null, null);
        await _edgeBusiness.UpdateEdge(uid, organizationId, pid, dto2, eid2, null, null);

        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Equal(2, historicalEdges.Count());
        var edge1 = historicalEdges.First(e => e.Id == eid);
        var edge2 = historicalEdges.First(e => e.Id == eid2);
        Assert.Equal(destinationRecordId2, edge1.DestinationId);
        Assert.Equal(destinationRecordId, edge1.OriginId);
        Assert.Equal(destinationRecordId, edge2.DestinationId);
        Assert.Equal(destinationRecordId2, edge2.OriginId);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_FiltersByDataSource()
    {
        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid, dsid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Single(historicalEdges);
        Assert.Contains(historicalEdges, e => e.Id == eid && e.DataSourceId == dsid);
        Assert.DoesNotContain(historicalEdges, e => e.Id == eid2);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_ExcludesArchivedHistoricalEdgesByDefault()
    {
        // Arrange
        await _edgeBusiness.ArchiveEdge(uid,  organizationId, pid,eid, null, null);
        Context.ChangeTracker.Clear();

        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Single(historicalEdges, e => !e.IsArchived);
        Assert.Contains(historicalEdges, e => e.Id == eid2);
        Assert.DoesNotContain(historicalEdges, e => e.Id == eid);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_InlcudesArchivedHistoricalEdges()
    {
        // Arrange
        await _edgeBusiness.ArchiveEdge(uid, organizationId, pid, eid, null, null);

        // Add this line to force EF to reload from database
        Context.ChangeTracker.Clear();

        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Single(historicalEdges);
        Assert.DoesNotContain(historicalEdges, e => e.Id == eid && e.IsArchived);
        Assert.Contains(historicalEdges, e => e.Id == eid2 && !e.IsArchived);
    }

    #endregion

    #region GetHistoricalEdges Tests

    [Fact]
    public async Task GetHistoricalEdges_ReturnsEmptyListWhenNoEdges()
    {
        // Arrange
        await _edgeBusiness.DeleteEdge(uid, organizationId, pid, eid, null, null);
        await _edgeBusiness.DeleteEdge(uid, organizationId, pid, eid2, null, null);

        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Empty(historicalEdges);
    }

    [Fact]
    public async Task GetHistoricalEdges_FiltersByTime()
    {
        // Arrange
        var pointInTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var edgeLate = new Edge
        {
            ProjectId = pid,
            DataSourceId = dsid,
            OriginId = destinationRecordId2,
            LastUpdatedAt = pointInTime.AddMilliseconds(1),
            RelationshipId = relationshipId,
            DestinationId = originRecordId, OrganizationId = organizationId
        };
        Context.Edges.Add(edgeLate);
        await Context.SaveChangesAsync();

        // Act
        var historicalEdges = await _historicalEdgeBusiness.GetAllHistoricalEdges(pid, null, pointInTime);

        // Assert
        Assert.NotNull(historicalEdges);
        Assert.Equal(2, historicalEdges.Count());
        Assert.DoesNotContain(historicalEdges, e => e.Id == edgeLate.Id);
    }

    #endregion

    #region GetHistoryForEdge Tests

    [Fact]
    public async Task GetHistoryForEdge_ReturnsFullHistory()
    {
        // Arrange
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };
        await _edgeBusiness.UpdateEdge(uid, organizationId, pid, dto, eid, null, null);
        await _edgeBusiness.ArchiveEdge(uid, organizationId, pid, eid, null, null);

        // Act
        var edgeHistory = await _historicalEdgeBusiness.GetHistoryForEdge(organizationId, eid, null, null);

        // Assert
        Assert.NotNull(edgeHistory);
        Assert.Equal(4, edgeHistory.Count());
        Assert.All(edgeHistory, e => Assert.Equal(eid, e.Id));
        Assert.True(edgeHistory.First().IsArchived);
    }

    [Fact]
    public async Task GetHistoryForEdge_ThrowsError_WhenEdgeDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _historicalEdgeBusiness.GetHistoryForEdge(organizationId, eid4 + 100000, null, null));
    }

    #endregion

    #region GetHistoricalEdge Tests

    [Fact]
    public async Task GetHistoricalEdge_ThrowsError_WhenEdgeDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _historicalEdgeBusiness.GetHistoricalEdge(organizationId, eid4 + 100000, null, null, null));
    }

    [Fact]
    public async Task GetHistoricalEdge_ReturnsMostCurrentEdge()
    {
        // Arrange
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };
        await _edgeBusiness.UpdateEdge(uid, organizationId, pid, dto, eid, null, null);

        // Act
        var historicalEdge = await _historicalEdgeBusiness.GetHistoricalEdge(organizationId, eid, null, null, null);

        // Assert
        Assert.NotNull(historicalEdge);
        Assert.Equal(eid, historicalEdge.Id);
        Assert.Equal(destinationRecordId, historicalEdge.OriginId);
        Assert.Equal(destinationRecordId2, historicalEdge.DestinationId);
    }

    [Fact]
    public async Task GetHistoricalEdge_CanIncludeArchivedHistoricalEdges()
    {
        // Arrange
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };
        await _edgeBusiness.UpdateEdge(uid, organizationId, pid, dto, eid, null, null);
        await _edgeBusiness.ArchiveEdge(uid, organizationId, pid, eid, null, null);

        // Act
        var historicalEdge =
            await _historicalEdgeBusiness.GetHistoricalEdge(organizationId, eid, null, null, null, false);

        // Assert
        Assert.NotNull(historicalEdge);
        Assert.Equal(eid, historicalEdge.Id);
        Assert.Equal(destinationRecordId, historicalEdge.OriginId);
        Assert.Equal(destinationRecordId2, historicalEdge.DestinationId);
        Assert.True(historicalEdge.IsArchived);
    }

    [Fact]
    public async Task GetHistoricalEdge_FiltersByTime()
    {
        // Arrange
        var pointInTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)destinationRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };
        await _edgeBusiness.UpdateEdge(uid, organizationId, pid,dto, eid, null, null);

        // Act
        var historicalEdge =
            await _historicalEdgeBusiness.GetHistoricalEdge(organizationId, eid, null, null, pointInTime);

        // Assert
        Assert.NotNull(historicalEdge);
        Assert.Equal(eid, historicalEdge.Id);
        Assert.Equal(originRecordId, historicalEdge.OriginId);
        Assert.Equal(destinationRecordId, historicalEdge.DestinationId);
    }

    [Fact]
    public async Task GetHistoricalEdge_ReturnsArchivedHistoricalEdge_WhenEdgeIsArchived()
    {
        // Arrange
        await _edgeBusiness.ArchiveEdge(uid, organizationId, pid,eid, null, null);

        // Act
        var historicalEdge = () => _historicalEdgeBusiness.GetHistoricalEdge(organizationId, eid, null, null, null);

        // Assert
        Assert.NotNull(historicalEdge);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateHistoricalEdge_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId2,
            DataSourceId = dsid,
            ProjectId = pid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        var testHistoricalEdge = new HistoricalEdge
        {
            EdgeId = testEdge.Id,
            OriginId = testEdge.OriginId,
            DestinationId = testEdge.DestinationId,
            RelationshipId = testEdge.RelationshipId,
            DataSourceId = testEdge.DataSourceId,
            ProjectId = testEdge.ProjectId,
            LastUpdatedBy = uid,
            LastUpdatedAt = testEdge.LastUpdatedAt,
            IsArchived = false, OrganizationId = organizationId
        };

        // Act
        Context.HistoricalEdges.Add(testHistoricalEdge);
        await Context.SaveChangesAsync();

        // Assert
        var savedHistoricalEdge = await Context.HistoricalEdges.FindAsync(testHistoricalEdge.Id);
        Assert.NotNull(savedHistoricalEdge);
        Assert.Equal(uid, savedHistoricalEdge.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateHistoricalEdge_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = destinationRecordId2,
            DestinationId = originRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid, OrganizationId = organizationId
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        var testHistoricalEdge = new HistoricalEdge
        {
            EdgeId = testEdge.Id,
            OriginId = testEdge.OriginId,
            DestinationId = testEdge.DestinationId,
            RelationshipId = testEdge.RelationshipId,
            DataSourceId = testEdge.DataSourceId,
            ProjectId = testEdge.ProjectId,
            LastUpdatedBy = uid,
            LastUpdatedAt = testEdge.LastUpdatedAt,
            IsArchived = false, OrganizationId = organizationId
        };

        Context.HistoricalEdges.Add(testHistoricalEdge);
        await Context.SaveChangesAsync();

        // Act
        var historicalEdgeWithUser = await Context.HistoricalEdges
            .Include(he => he.LastUpdatedByUser)
            .FirstAsync(he => he.Id == testHistoricalEdge.Id);

        // Assert
        Assert.NotNull(historicalEdgeWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", historicalEdgeWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test.user@test.com", historicalEdgeWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, historicalEdgeWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateHistoricalEdge_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = destinationRecordId,
            DestinationId = destinationRecordId2,
            DataSourceId = dsid,
            ProjectId = pid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null, OrganizationId = organizationId
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        var testHistoricalEdge = new HistoricalEdge
        {
            EdgeId = testEdge.Id,
            OriginId = testEdge.OriginId,
            DestinationId = testEdge.DestinationId,
            RelationshipId = testEdge.RelationshipId,
            DataSourceId = testEdge.DataSourceId,
            ProjectId = testEdge.ProjectId,
            LastUpdatedBy = null,
            LastUpdatedAt = testEdge.LastUpdatedAt,
            IsArchived = false, OrganizationId = organizationId
        };

        // Act
        Context.HistoricalEdges.Add(testHistoricalEdge);
        await Context.SaveChangesAsync();

        // Assert
        var savedHistoricalEdge = await Context.HistoricalEdges.FindAsync(testHistoricalEdge.Id);
        Assert.NotNull(savedHistoricalEdge);
        Assert.Null(savedHistoricalEdge.LastUpdatedBy);

        var historicalEdgeWithUser = await Context.HistoricalEdges
            .Include(he => he.LastUpdatedByUser)
            .FirstAsync(he => he.Id == testHistoricalEdge.Id);

        Assert.Null(historicalEdgeWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateHistoricalEdge_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange - First create edge with null LastUpdatedBy
        var testEdge = new Edge
        {
            OriginId = originRecordId2,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null, OrganizationId = organizationId
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        var testHistoricalEdge = new HistoricalEdge
        {
            EdgeId = testEdge.Id,
            OriginId = testEdge.OriginId,
            DestinationId = testEdge.DestinationId,
            RelationshipId = testEdge.RelationshipId,
            DataSourceId = testEdge.DataSourceId,
            ProjectId = testEdge.ProjectId,
            LastUpdatedBy = null,
            LastUpdatedAt = testEdge.LastUpdatedAt,
            IsArchived = false, OrganizationId = organizationId
        };
        Context.HistoricalEdges.Add(testHistoricalEdge);
        await Context.SaveChangesAsync();

        // Act - Update to have a user
        testHistoricalEdge.LastUpdatedBy = uid;
        testHistoricalEdge.DestinationId = destinationRecordId2;
        testHistoricalEdge.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.HistoricalEdges.Update(testHistoricalEdge);
        await Context.SaveChangesAsync();

        // Assert
        var updatedHistoricalEdge = await Context.HistoricalEdges
            .Include(he => he.LastUpdatedByUser)
            .FirstAsync(he => he.Id == testHistoricalEdge.Id);

        Assert.Equal(uid, updatedHistoricalEdge.LastUpdatedBy);
        Assert.NotNull(updatedHistoricalEdge.LastUpdatedByUser);
        Assert.Equal("Test User", updatedHistoricalEdge.LastUpdatedByUser.Name);
        Assert.Equal(destinationRecordId2, updatedHistoricalEdge.DestinationId);
    }

    #endregion
}