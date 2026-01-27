using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class GraphBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private EdgeBusiness _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private GraphBusiness _graphBusiness = null!;
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
    private IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;
    
    public long classId;
    public long dsid;
    public long oid;
    public long pid;
    public long uid1;
    public long relationshipId;
    public long record1Id;
    public long record2Id;
    public long record3Id;
    public long record4Id;
    public long record5Id;

    public GraphBusinessTests(TestSuiteFixture fixture) : base(fixture)
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
        _bulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _bulkCopyUpsertExecutor);
        
        _mockOrganizationBusiness = new Mock<IOrganizationBusiness>();

        _graphBusiness = new GraphBusiness(Context, _eventBusiness);
        _edgeBusiness = new EdgeBusiness(Context, _eventBusiness, _bulkCopyUpsertExecutor);
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
            Name = "Test User",
            Email = "test@example.com",
            Password = "password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid1 = user.Id;

        var org = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var project = new Project { Name = "Test Project", OrganizationId = oid };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var dataSource = new DataSource
        {
            Name = "Test DataSource",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        dsid = dataSource.Id;

        var testClass = new Class
        {
            Name = "Test Class",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        classId = testClass.Id;

        var relationship = new Relationship
        {
            Name = "ConnectsTo",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Relationships.Add(relationship);
        await Context.SaveChangesAsync();
        relationshipId = relationship.Id;

        // Create test records
        var records = new[]
        {
            new Record
            {
                ProjectId = pid,
                DataSourceId = dsid,
                ClassId = classId,
                OrganizationId = oid,
                Name = "Record 1",
                Description = "Description for Record 1",
                Properties = "{}",
                OriginalId = "r1",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Record
            {
                ProjectId = pid,
                DataSourceId = dsid,
                ClassId = classId,
                OrganizationId = oid,
                Name = "Record 2",
                Description = "Description for Record 2",
                Properties = "{}",
                OriginalId = "r2",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Record
            {
                ProjectId = pid,
                DataSourceId = dsid,
                ClassId = classId,
                OrganizationId = oid,
                Name = "Record 3",
                Description = "Description for Record 3",
                Properties = "{}",
                OriginalId = "r3",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Record
            {
                ProjectId = pid,
                DataSourceId = dsid,
                ClassId = classId,
                OrganizationId = oid,
                Name = "Record 4",
                Description = "Description for Record 4",
                Properties = "{}",
                OriginalId = "r4",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Record
            {
                ProjectId = pid,
                DataSourceId = dsid,
                ClassId = classId,
                OrganizationId = oid,
                Name = "Record 5",
                Description = "Description for Record 5",
                Properties = "{}",
                OriginalId = "r5",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            }
        };

        Context.Records.AddRange(records);
        await Context.SaveChangesAsync();

        record1Id = records[0].Id;
        record2Id = records[1].Id;
        record3Id = records[2].Id;
        record4Id = records[3].Id;
        record5Id = records[4].Id;
    }

    #region GetEdgesByRecord Additional Tests

    [Fact]
    public async Task GetEdgesByRecord_ReturnsEmptyList_WhenNoEdgesExist()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Act
        var result = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 1, 20);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEdgesByRecord_ExcludesArchivedDestinations()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var archivedRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            ClassId = classId,
            OrganizationId = oid,
            Name = "Archived Record",
            Description = "Archived Record Description",
            Properties = "{}",
            OriginalId = "archived",
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Records.Add(archivedRecord);
        await Context.SaveChangesAsync();

        var edge1 = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        var edge2 = new Edge
        {
            OriginId = record1Id,
            DestinationId = archivedRecord.Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        Context.Edges.AddRange(edge1, edge2);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 1, 20);

        // Assert
        Assert.Single(result);
        Assert.Equal("Record 2", result[0].RelatedRecordName);
    }

    [Fact]
    public async Task GetEdgesByRecord_ExcludesArchivedOrigins()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var archivedRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            ClassId = classId,
            OrganizationId = oid,
            Name = "Archived Origin",
            Description = "Archived Origin Description",
            Properties = "{}",
            OriginalId = "archived_origin",
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Records.Add(archivedRecord);
        await Context.SaveChangesAsync();

        var edge1 = new Edge
        {
            OriginId = record2Id,
            DestinationId = record1Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        var edge2 = new Edge
        {
            OriginId = archivedRecord.Id,
            DestinationId = record1Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        Context.Edges.AddRange(edge1, edge2);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, false, 1, 20);

        // Assert
        Assert.Single(result);
        Assert.Equal("Record 2", result[0].RelatedRecordName);
    }

    [Fact]
    public async Task GetEdgesByRecord_IncludesRelationshipName_WhenRelationshipExists()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var edge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        Context.Edges.Add(edge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 1, 20);

        // Assert
        Assert.Single(result);
        Assert.Equal("ConnectsTo", result[0].RelationshipName);
    }

    [Fact]
    public async Task GetEdgesByRecord_PaginatesCorrectly_WithMultiplePages()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Create 5 edges
        for (int i = 2; i <= 5; i++)
        {
            Context.Edges.Add(new Edge
            {
                OriginId = record1Id,
                DestinationId = i == 2 ? record2Id : i == 3 ? record3Id : i == 4 ? record4Id : record5Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }
        await Context.SaveChangesAsync();

        // Act - Get page 1 (2 items)
        var page1 = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 1, 2);
        
        // Act - Get page 2 (2 items)
        var page2 = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 2, 2);
        
        // Assert
        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
     
        
        // Verify no duplicates across pages
        var allIds = page1.Select(r => r.RelatedRecordId)
            .Concat(page2.Select(r => r.RelatedRecordId))
            .ToList();
        Assert.Equal(4, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetEdgesByRecord_ReturnsEmptyList_WhenPageExceedsTotalResults()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var edge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Edges.Add(edge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, 5, 20);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEdgesByRecord_ThrowsArgumentException_WhenPageIsNegative()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _graphBusiness.GetEdgesByRecord(oid, pid, record1Id, true, -1, 20));
    }

    [Fact]
    public async Task GetEdgesByRecord_ThrowsKeyNotFoundException_WhenRecordIsArchived()
    {
        // Arrange
        var archivedRecord = new Record
        {
            ProjectId = pid,
            DataSourceId = dsid,
            ClassId = classId,
            OrganizationId = oid,
            Name = "Archived",
            Description = "Archived Description",
            Properties = "{}",
            OriginalId = "archived",
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Records.Add(archivedRecord);
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _graphBusiness.GetEdgesByRecord(oid, pid, archivedRecord.Id, true, 1, 20));
    }

    #endregion

    #region GetGraphDataForRecord Additional Tests

    [Fact]
    public async Task GetGraphData_ReturnsOnlyRootNode_WhenNoEdgesExist()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 3);

        // Assert
        Assert.Single(result.Nodes);
        Assert.Equal(record1Id, result.Nodes[0].Id);
        Assert.Equal("root", result.Nodes[0].Type);
        Assert.Empty(result.Links);
    }

    [Fact]
    public async Task GetGraphData_HandlesCircularReferences_WithoutDuplication()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Create circular edges: 1 -> 2 -> 3 -> 1
        var edges = new[]
        {
            new Edge
            {
                OriginId = record1Id,
                DestinationId = record2Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Edge
            {
                OriginId = record2Id,
                DestinationId = record3Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Edge
            {
                OriginId = record3Id,
                DestinationId = record1Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            }
        };
        Context.Edges.AddRange(edges);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 3);

        // Assert
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(3, result.Links.Count);
        
        // Verify each node appears only once
        var nodeIds = result.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(nodeIds.Distinct().Count(), nodeIds.Count);
    }

    [Fact]
    public async Task GetGraphData_RespectsBidirectionalEdges()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Create bidirectional edges between record1 and record2
        var edges = new[]
        {
            new Edge
            {
                OriginId = record1Id,
                DestinationId = record2Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new Edge
            {
                OriginId = record2Id,
                DestinationId = record1Id,
                DataSourceId = dsid,
                ProjectId = pid,
                OrganizationId = oid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            }
        };
        Context.Edges.AddRange(edges);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 1);

        // Assert
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(2, result.Links.Count);
        
        // Verify both directions are present
        Assert.Contains(result.Links, l => l.Source == record1Id && l.Target == record2Id);
        Assert.Contains(result.Links, l => l.Source == record2Id && l.Target == record1Id);
    }

    [Fact]
    public async Task GetGraphData_ExcludesArchivedEdges()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var activeEdge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        var archivedEdge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record3Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        Context.Edges.AddRange(activeEdge, archivedEdge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 1);

        // Assert
        Assert.Equal(2, result.Nodes.Count); // record1 and record2 only
        Assert.Single(result.Links);
        Assert.DoesNotContain(result.Nodes, n => n.Id == record3Id);
    }

    [Fact]
    public async Task GetGraphData_IncludesRelationshipMetadata()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var edge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            RelationshipId = relationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        Context.Edges.Add(edge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 1);

        // Assert
        Assert.Single(result.Links);
        Assert.Equal(relationshipId, result.Links[0].RelationshipId);
        Assert.Equal("ConnectsTo", result.Links[0].RelationshipName);
        Assert.Equal(edge.Id, result.Links[0].EdgeId);
    }

    [Fact]
    public async Task GetGraphData_HandlesComplexMultiLevelGraph()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Create a tree structure:
        //       R1
        //      /  \
        //     R2   R3
        //    /      \
        //   R4      R5
        var edges = new[]
        {
            new Edge { OriginId = record1Id, DestinationId = record2Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record1Id, DestinationId = record3Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record2Id, DestinationId = record4Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record3Id, DestinationId = record5Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) }
        };
        Context.Edges.AddRange(edges);
        await Context.SaveChangesAsync();

        // Act - Depth 2 should get all 5 nodes
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 2);

        // Assert
        Assert.Equal(5, result.Nodes.Count);
        Assert.Equal(4, result.Links.Count);
        
        // Verify root node type
        Assert.Single(result.Nodes.Where(n => n.Type == "root"));
        Assert.Equal(4, result.Nodes.Count(n => n.Type == "node"));
    }

    [Fact]
    public async Task GetGraphData_StopsAtSpecifiedDepth()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Create chain: R1 -> R2 -> R3 -> R4 -> R5
        var edges = new[]
        {
            new Edge { OriginId = record1Id, DestinationId = record2Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record2Id, DestinationId = record3Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record3Id, DestinationId = record4Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            new Edge { OriginId = record4Id, DestinationId = record5Id, DataSourceId = dsid, ProjectId = pid, OrganizationId = oid, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) }
        };
        Context.Edges.AddRange(edges);
        await Context.SaveChangesAsync();

        // Act - Depth 2 should only get R1, R2, R3
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 2);

        // Assert
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(2, result.Links.Count);
        Assert.Contains(result.Nodes, n => n.Id == record1Id);
        Assert.Contains(result.Nodes, n => n.Id == record2Id);
        Assert.Contains(result.Nodes, n => n.Id == record3Id);
        Assert.DoesNotContain(result.Nodes, n => n.Id == record4Id);
        Assert.DoesNotContain(result.Nodes, n => n.Id == record5Id);
    }

    [Fact]
    public async Task GetGraphData_ThrowsArgumentException_WhenDepthExceeds3()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 4));
    }

    [Fact]
    public async Task GetGraphData_ThrowsAccessViolationException_WhenUserLacksProjectAccess()
    {
        // Arrange - Don't add user to project

        // Act & Assert
        await Assert.ThrowsAsync<AccessViolationException>(() =>
            _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 1));
    }

    [Fact]
    public async Task GetGraphData_WorksWithGroupMembership()
    {
        // Arrange
        var group = new Group
        {
            Name = "Test Group",
            OrganizationId = oid
        };
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();
        
        var user = await Context.Users.FindAsync(uid1);
        if (user != null)
        {
            group.Users.Add(user);
            await Context.SaveChangesAsync();
        }

        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            GroupId = group.Id
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        var edge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Edges.Add(edge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 1);

        // Assert
        Assert.Equal(2, result.Nodes.Count);
        Assert.Single(result.Links);
    }

    [Fact]
    public async Task GetGraphData_HandlesDepthZero_ReturnsOnlyRoot()
    {
        // Arrange
        await _projectBusiness.AddMemberToProject(pid, null, uid1, null);

        var edge = new Edge
        {
            OriginId = record1Id,
            DestinationId = record2Id,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Edges.Add(edge);
        await Context.SaveChangesAsync();

        // Act
        var result = await _graphBusiness.GetGraphDataForRecord(oid, pid, record1Id, uid1, 0);

        // Assert
        Assert.Single(result.Nodes);
        Assert.Equal(record1Id, result.Nodes[0].Id);
        Assert.Empty(result.Links);
    }

    #endregion
}