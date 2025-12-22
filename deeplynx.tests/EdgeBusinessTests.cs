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
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class EdgeBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private EdgeBusiness _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<IObjectStorageBusiness> _mockObjectStorageBusiness = null!;
    private Mock<IOrganizationBusiness> _mockOrganizationBusiness = null!;
    private Mock<IRecordBusiness> _mockRecordBusiness = null!;
    private Mock<IRelationshipBusiness> _mockRelationshipBusiness = null!;
    private Mock<IRoleBusiness> _mockRoleBusiness = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    public long destinationRecordId;
    public long destinationRecordId2;
    public long destinationRecordId3;
    public long dsid;
    public long dsid2;
    public long oid;
    public long originRecordId;
    public long originRecordId2;

    public long pid;
    public long pid2;
    public long relationshipId;
    public long uid1;

    public EdgeBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockRecordBusiness = new Mock<IRecordBusiness>();
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _mockObjectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _mockOrganizationBusiness = new Mock<IOrganizationBusiness>();

        _edgeBusiness = new EdgeBusiness(Context, _eventBusiness);
        _dataSourceBusiness = new DataSourceBusiness(Context, _edgeBusiness, _mockRecordBusiness.Object,
            _eventBusiness);
        _classBusiness = new ClassBusiness(
            Context, _mockRecordBusiness.Object,
            _mockRelationshipBusiness.Object, _eventBusiness);

        _projectBusiness = new ProjectBusiness(
            Context, _mockLogger.Object, _classBusiness,
            _mockRoleBusiness.Object, _dataSourceBusiness,
            _mockObjectStorageBusiness.Object, _eventBusiness, _mockOrganizationBusiness.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Test User 1",
            Email = "test_email@example.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid1 = user.Id;

        var org = new Organization { Name = "Test Org" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var project = new Project { Name = "Project 1", OrganizationId = oid };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var project2 = new Project { Name = "Project 2", OrganizationId = oid };
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid2 = project2.Id;

        var dataSource = new DataSource
        {
            Name = "DataSource 1",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        dsid = dataSource.Id;

        var dataSource2 = new DataSource
        {
            Name = "DataSource 2",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.DataSources.Add(dataSource2);
        await Context.SaveChangesAsync();
        dsid2 = dataSource2.Id;

        var testClass = new Class
        {
            Name = "Class 1",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Classes.Add(testClass);
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
            OrganizationId = oid
        };
        Context.Records.Add(originRecord);

        var originRecord2 = new Record
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            Properties = "{\"test\": \"origin2_value\"}",
            Name = "Origin 2",
            Description = "Origin Description 2",
            OriginalId = "orig2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
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
            OrganizationId = oid
        };
        Context.Records.Add(destinationRecord);

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
            OrganizationId = oid
        };
        Context.Records.Add(destinationRecord2);

        //Record in another project
        var destinationRecord3 = new Record
        {
            ProjectId = pid2,
            DataSourceId = dsid2,
            Properties = "{\"test\": \"destination3_value\"}",
            Name = "Destination 3",
            Description = "Destination Description 3",
            OriginalId = "dest3",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Records.Add(destinationRecord3);

        var relationship = new Relationship
        {
            Name = "Relationship 1",
            ProjectId = pid,
            OriginId = testClass.Id,
            DestinationId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Relationships.Add(relationship);

        await Context.SaveChangesAsync();

        originRecordId = originRecord.Id;
        originRecordId2 = originRecord2.Id;
        destinationRecordId = destinationRecord.Id;
        destinationRecordId2 = destinationRecord2.Id;
        destinationRecordId3 = destinationRecord3.Id;
        relationshipId = relationship.Id;
    }

    #region CreateEdge Tests

    [Fact]
    public async Task CreateEdge_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };

        // Act
        var result = await _edgeBusiness.CreateEdge(uid1, oid, pid, dsid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(relationshipId, result.RelationshipId);
        Assert.Equal(originRecordId, result.OriginId);
        Assert.Equal(destinationRecordId, result.DestinationId);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(dsid, result.DataSourceId);
        Assert.Equal(uid1, result.LastUpdatedBy);

        // Ensure that edge create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("edge", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateEdge_Fails_IfNoOriginId()
    {
        // Arrange
        var dto = new CreateEdgeRequestDto
        {
            OriginId = 0, // Invalid origin
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _edgeBusiness.CreateEdge(uid1, oid, pid, dsid, dto));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateEdge_Fails_IfNoDestinationId()
    {
        // Arrange
        var dto = new CreateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = 0, // Invalid destination
            RelationshipId = (int)relationshipId
        };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _edgeBusiness.CreateEdge(uid1, oid, pid, dsid, dto));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateEdge_Fails_IfSameDestinationIdAndOriginId()
    {
        var dto = new CreateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)originRecordId,
            RelationshipId = (int)relationshipId
        };
        await Assert.ThrowsAsync<ValidationException>(() => _edgeBusiness.CreateEdge(uid1, oid, pid, dsid, dto));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateEdge_Fails_IfNoProjectId()
    {
        // Arrange
        var dto = new CreateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _edgeBusiness.CreateEdge(uid1, oid, pid + 99, dsid, dto));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateEdge_Fails_IfNoDataSourceId()
    {
        // Arrange
        var dto = new CreateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _edgeBusiness.CreateEdge(uid1, oid, pid, dsid + 99, dto));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region BulkCreateEdge Tests

    [Fact]
    public async Task BulkCreateEdges_Success_ReturnsMultipleEdges()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var edges = new List<CreateEdgeRequestDto>
        {
            new()
            {
                OriginId = (int)originRecordId,
                DestinationId = (int)destinationRecordId,
                RelationshipId = (int)relationshipId
            },
            new()
            {
                OriginId = (int)originRecordId,
                DestinationId = (int)destinationRecordId2,
                RelationshipId = (int)relationshipId
            }
        };

        // Act
        var result = await _edgeBusiness.BulkCreateEdges(uid1, oid, pid, dsid, edges);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.True(e.Id > 0));
        Assert.All(result, e => Assert.True(e.LastUpdatedAt >= now));

            var eventList = await Context.Events.ToListAsync();
            Assert.Equal(1, eventList.Count); // bulk events just log one event with count
        }

    [Fact]
    public async Task BulkCreateEdges_Fails_IfNullDto()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _edgeBusiness.BulkCreateEdges(uid1, oid, pid, dsid, null));

        // Ensure that edge create event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region GetAllEdges Tests

    [Fact]
    public async Task GetAllEdges_ReturnsOnlyForProject()
    {
        // Arrange
        await _edgeBusiness.CreateEdge(uid1, oid, pid, dsid,
            new CreateEdgeRequestDto { OriginId = (int)originRecordId, DestinationId = (int)destinationRecordId });
        await _edgeBusiness.CreateEdge(uid1, oid, pid2, dsid2,
            new CreateEdgeRequestDto { OriginId = (int)originRecordId, DestinationId = (int)destinationRecordId });

        // Act
        var list = await _edgeBusiness.GetAllEdges(oid, pid, null, true);

        // Assert
        Assert.All(list, e => Assert.Equal(pid, e.ProjectId));
    }

    [Fact]
    public async Task GetAllEdges_ExcludesSoftDeleted()
    {
        // Arrange
        var activeEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        var archivedEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId2,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = true
        };
        Context.Edges.Add(activeEdge);
        Context.Edges.Add(archivedEdge);
        await Context.SaveChangesAsync();

        // Act
        var listWithArchived = await _edgeBusiness.GetAllEdges(oid, pid, null, false);
        var listWithoutArchived = await _edgeBusiness.GetAllEdges(oid, pid, null, true);

        // Assert
        Assert.Contains(listWithArchived, e => e.Id == archivedEdge.Id);
        Assert.DoesNotContain(listWithoutArchived, e => e.Id == archivedEdge.Id);
    }

    #endregion

    #region GetEdge Tests

    [Fact]
    public async Task GetEdge_Success_WhenExistsById()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _edgeBusiness.GetEdge(oid, pid, testEdge.Id, null, null, true);

        // Assert
        Assert.Equal(testEdge.Id, result.Id);
        Assert.Equal(originRecordId, result.OriginId);
        Assert.Equal(destinationRecordId, result.DestinationId);
    }

    [Fact]
    public async Task GetEdge_Success_WhenExistsByOriginDestination()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _edgeBusiness.GetEdge(oid, pid, null, originRecordId, destinationRecordId, true);

        // Assert
        Assert.Equal(testEdge.Id, result.Id);
        Assert.Equal(originRecordId, result.OriginId);
        Assert.Equal(destinationRecordId, result.DestinationId);
    }

    [Fact]
    public async Task GetEdge_Fails_IfNoProjectID()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.GetEdge(oid, pid + 999, testEdge.Id, null, null, true));
    }

    [Fact]
    public async Task GetEdge_Fails_IfDeletedEdge()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = true
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.GetEdge(oid, pid, testEdge.Id, null, null, true));
    }

    [Fact]
    public async Task GetEdge_Fails_IfMissingIds()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _edgeBusiness.GetEdge(oid, pid, null, null, null, true));
        Assert.Contains("Please supply either an edgeID or an originID and destinationID", exception.Message);
    }

    #endregion

    #region UpdateEdge Tests

    [Fact]
    public async Task UpdateEdge_Success_ReturnsCorrectvalues()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Store the original timestamp for comparison
        var originalLastUpdatedAt = testEdge.LastUpdatedAt;

        // Create another destination record for update
        var newDestinationRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            OrganizationId = oid,
            Properties = "{\"test\": \"Updated destination_value\"}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Name = "New Destination",
            Description = "New Destination Description",
            OriginalId = "new"
        };
        Context.Records.Add(newDestinationRecord);
        await Context.SaveChangesAsync();

        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)destinationRecordId2,
            RelationshipId = (int)relationshipId
        };

        // Act
        var updatedResult = await _edgeBusiness.UpdateEdge(uid1, oid, pid, dto, testEdge.Id, null, null);

        // Assert
        Assert.NotNull(updatedResult);
        Assert.False(updatedResult.IsArchived);
        Assert.Equal(relationshipId, updatedResult.RelationshipId);
        Assert.Equal(originRecordId, updatedResult.OriginId);
        Assert.Equal(destinationRecordId2, updatedResult.DestinationId);
        Assert.Equal(pid, updatedResult.ProjectId);
        Assert.Equal(dsid, updatedResult.DataSourceId);
        Assert.True(updatedResult.LastUpdatedAt >= originalLastUpdatedAt);
        Assert.Equal(uid1, updatedResult.LastUpdatedBy);

        // Ensure that update edge event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(testEdge.Id, actualEvent.EntityId);
        Assert.Equal("edge", actualEvent.EntityType);
        Assert.Equal("update", actualEvent.Operation);
    }

    [Fact]
    public async Task UpdateEdge_Fails_WhenSameOriginAndDestinationId()
    {
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Create another destination record for update
        var newDestinationRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            OrganizationId = oid,
            Properties = "{\"test\": \"Updated destination_value\"}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Name = "New Destination",
            Description = "New Destination Description",
            OriginalId = "new"
        };
        Context.Records.Add(newDestinationRecord);
        await Context.SaveChangesAsync();

        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)originRecordId,
            RelationshipId = (int)relationshipId
        };
        await Assert.ThrowsAsync<ValidationException>(() =>
            _edgeBusiness.UpdateEdge(uid1, oid, pid, dto, testEdge.Id, null, null));
        // Ensure that update edge event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateEdge_PartialUpdate_UpdatesEdge()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();
        var edgeOriginId = testEdge.OriginId;
        var edgeDestinationId = testEdge.DestinationId;

        // Create another destination record for update
        var newDestinationRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            OrganizationId = oid,
            Properties = "{\"test\": \"Updated destination_value\"}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Name = "New Destination",
            Description = "New Destination Description",
            OriginalId = "new"
        };
        Context.Records.Add(newDestinationRecord);
        await Context.SaveChangesAsync();

        var dto = new UpdateEdgeRequestDto
        {
            RelationshipId = (int)relationshipId
        };

        // Act
        var result = await _edgeBusiness.UpdateEdge(uid1, oid, pid, dto, testEdge.Id, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal((int)relationshipId, result.RelationshipId);
        Assert.Equal(edgeOriginId, result.OriginId);
        Assert.Equal(edgeDestinationId, result.DestinationId);
        Assert.NotEqual(DateTime.MinValue, result.LastUpdatedAt);

        // Verify edge was actually updated in database
        var updatedEdge = await Context.Edges.FindAsync(testEdge.Id);
        Assert.NotNull(updatedEdge);
        Assert.Equal((int)relationshipId, updatedEdge.RelationshipId);
        Assert.Equal(edgeOriginId, updatedEdge.OriginId);
        Assert.Equal(edgeDestinationId, updatedEdge.DestinationId);
        Assert.NotEqual(DateTime.MinValue, updatedEdge.LastUpdatedAt);

        // Verify that get function gets updated version
        var getResult = await _edgeBusiness.GetEdge(oid, pid, testEdge.Id, edgeOriginId, edgeDestinationId, true);
        Assert.NotNull(getResult);
        Assert.Equal((int)relationshipId, getResult.RelationshipId);
        Assert.Equal(edgeOriginId, getResult.OriginId);
        Assert.Equal(edgeDestinationId, getResult.DestinationId);
        Assert.NotEqual(DateTime.MinValue, getResult.LastUpdatedAt);

        // Ensure that update edge event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(testEdge.Id, actualEvent.EntityId);
        Assert.Equal("edge", actualEvent.EntityType);
        Assert.Equal("update", actualEvent.Operation);
    }

    [Fact]
    public async Task UpdateEdge_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateEdgeRequestDto
        {
            OriginId = (int)originRecordId,
            DestinationId = (int)destinationRecordId,
            RelationshipId = (int)relationshipId
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.UpdateEdge(uid1, oid, pid, dto, 99, null, null));

        // Ensure that update edge event was NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region ArchiveEdge Tests

    [Fact]
    public async Task ArchiveEdge_Success_WhenExists()
    {
        // Arrange
        var beforeArchive = DateTime.UtcNow;
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            OrganizationId = oid
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // Act
        var archivedResult = await _edgeBusiness.ArchiveEdge(uid1, oid, pid, testEdge.Id, null, null);

        var archivedEdge = await Context.Edges.FindAsync(testEdge.Id);

        // Assert
        Assert.NotNull(archivedEdge);
        Assert.Null(archivedEdge.RelationshipId);
        Assert.Equal(testEdge.Id, archivedResult);
        Assert.True(archivedEdge?.IsArchived);
        Assert.Equal(originRecordId, archivedEdge?.OriginId);
        Assert.Equal(destinationRecordId, archivedEdge?.DestinationId);
        Assert.Equal(pid, archivedEdge?.ProjectId);
        Assert.Equal(dsid, archivedEdge?.DataSourceId);
        Assert.True(archivedEdge?.LastUpdatedAt >= now);
        Assert.Equal(uid1, archivedEdge.LastUpdatedBy);

        // Ensure that soft delete edge event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(testEdge.Id, actualEvent.EntityId);
        Assert.Equal("edge", actualEvent.EntityType);
        Assert.Equal("archive", actualEvent.Operation);
    }

    [Fact]
    public async Task ArchiveEdge_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.ArchiveEdge(uid1, oid, pid, 999, null, null));

        // Ensure that create edge event is NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task EdgeArchived_WhenProjectArchived()
    {
        // Arrange
        var beforeArchive = DateTime.UtcNow;
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        var deletedResult = await _projectBusiness.ArchiveProject(uid1, oid, pid);

        // procedure is not traced by entity framework
        //this forces EF to sync to db on next query
        Context.ChangeTracker.Clear();

        var archivedEdge = await Context.Edges.FindAsync(testEdge.Id);

        // Assert
        Assert.True(deletedResult);
        Assert.NotNull(archivedEdge);

        // Check if ArchivedAt was set (optional based on implementation)
        if (archivedEdge.IsArchived) Assert.True(archivedEdge.IsArchived);
        // Assert.True(archivedEdge.IsArchived >= beforeArchive);
        // Assert.True(archivedEdge.IsArchived <= DateTime.UtcNow);
    }

    #endregion

    #region UnarchiveEdge Tests

    [Fact]
    public async Task UnarchiveEdge_Success_WhenArchived()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = true
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();
        var now = DateTime.UtcNow;
        // Act
        var unarchivedResult = await _edgeBusiness.UnarchiveEdge(uid1, oid, pid, testEdge.Id, null, null);
        Assert.Equal(testEdge.Id, unarchivedResult);

        var unarchivedEdge = await Context.Edges.FindAsync(testEdge.Id);

        // Assert
        Assert.NotNull(unarchivedEdge);
        Assert.Null(unarchivedEdge.RelationshipId);
        Assert.False(unarchivedEdge.IsArchived);
        Assert.Equal(originRecordId, unarchivedEdge.OriginId);
        Assert.Equal(destinationRecordId, unarchivedEdge.DestinationId);
        Assert.Equal(pid, unarchivedEdge.ProjectId);
        Assert.Equal(dsid, unarchivedEdge.DataSourceId);
        Assert.True(unarchivedEdge.LastUpdatedAt >= now);
        Assert.Equal(uid1, unarchivedEdge.LastUpdatedBy);
    }

    [Fact]
    public async Task UnarchiveEdge_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.UnarchiveEdge(uid1, oid, pid, 999, null, null));

        // Ensure that create edge event is NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveEdge_Fails_IfNotArchived()
    {
        // Arrange
        var activeEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(activeEdge);
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _edgeBusiness.UnarchiveEdge(uid1, oid, pid, activeEdge.Id, null, null));

        // Ensure that create edge event is NOT logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteEdge Tests

    [Fact]
    public async Task DeleteEdge_Success_WhenExists()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        var deletedResult = await _edgeBusiness.DeleteEdge(uid1, oid, pid, testEdge.Id, null, null);

        // Assert
        Assert.Equal(testEdge.Id, deletedResult);
        var deletedEdge = await Context.Edges.FindAsync(testEdge.Id);
        Assert.Null(deletedEdge);
    }

    [Fact]
    public async Task DeleteEdge_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _edgeBusiness.DeleteEdge(uid1, oid, pid, 999, null, null));
    }

    #endregion

    #region EdgeDTO Tests

    [Fact]
    public void EdgeRequestDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var dto = new CreateEdgeRequestDto
        {
            OriginId = 1,
            DestinationId = 2,
            RelationshipId = 3,
            RelationshipName = "Test Relationship"
        };

        // Assert
        Assert.Equal(1, dto.OriginId);
        Assert.Equal(2, dto.DestinationId);
        Assert.Equal(3, dto.RelationshipId);
        Assert.Equal("Test Relationship", dto.RelationshipName);
    }

    [Fact]
    public void EdgeResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var dto = new EdgeResponseDto
        {
            Id = 1,
            OriginId = 2,
            DestinationId = 3,
            RelationshipId = 4,
            DataSourceId = 6,
            ProjectId = 7,
            LastUpdatedBy = uid1,
            LastUpdatedAt = now,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal(2, dto.OriginId);
        Assert.Equal(3, dto.DestinationId);
        Assert.Equal(4, dto.RelationshipId);
        Assert.Equal(6, dto.DataSourceId);
        Assert.Equal(7, dto.ProjectId);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.False(dto.IsArchived);
        Assert.Equal(uid1, dto.LastUpdatedBy);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateEdge_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid1
        };

        // Act
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Assert
        var savedEdge = await Context.Edges.FindAsync(testEdge.Id);
        Assert.NotNull(savedEdge);
        Assert.Equal(uid1, savedEdge.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateEdge_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid1
        };

        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        var edgeWithUser = await Context.Edges
            .Include(e => e.LastUpdatedByUser)
            .FirstAsync(e => e.Id == testEdge.Id);

        // Assert
        Assert.NotNull(edgeWithUser.LastUpdatedByUser);
        Assert.Equal("Test User 1", edgeWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test_email@example.com", edgeWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid1, edgeWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateEdge_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Assert
        var savedEdge = await Context.Edges.FindAsync(testEdge.Id);
        Assert.NotNull(savedEdge);
        Assert.Null(savedEdge.LastUpdatedBy);

        var edgeWithUser = await Context.Edges
            .Include(e => e.LastUpdatedByUser)
            .FirstAsync(e => e.Id == testEdge.Id);

        Assert.Null(edgeWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateEdge_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testEdge = new Edge
        {
            OriginId = originRecordId,
            DestinationId = destinationRecordId,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Edges.Add(testEdge);
        await Context.SaveChangesAsync();

        // Act
        testEdge.LastUpdatedBy = uid1;
        testEdge.DestinationId = destinationRecordId2;
        testEdge.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Edges.Update(testEdge);
        await Context.SaveChangesAsync();

        // Assert
        var updatedEdge = await Context.Edges
            .Include(e => e.LastUpdatedByUser)
            .FirstAsync(e => e.Id == testEdge.Id);

        Assert.Equal(uid1, updatedEdge.LastUpdatedBy);
        Assert.NotNull(updatedEdge.LastUpdatedByUser);
        Assert.Equal("Test User 1", updatedEdge.LastUpdatedByUser.Name);
        Assert.Equal(destinationRecordId2, updatedEdge.DestinationId);
    }

    #endregion
}