using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class MetadataBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private EdgeBusiness _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private MetadataBusiness _metadataBusiness = null!;
    private EdgeBusiness _mockEdgeBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private RelationshipBusiness _relationshipBusiness = null!;
    private TagBusiness _tagBusiness = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;
    public long cid; // origin class ID
    public long cid2; // destination class ID
    public long did;
    private long organizationId;

    public long pid;
    public long uid;

    public MetadataBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness = new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);

        // Build leaf dependencies first
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);

        _sensitivityLabelService = new SensitivityLabelService(Context);

        _edgeBusiness = new EdgeBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _sensitivityLabelService);
        _recordBusiness = new RecordBusiness(
            Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness, _sensitivityLabelBusiness, _sensitivityLabelService);
        _relationshipBusiness = new RelationshipBusiness(Context, _edgeBusiness, _eventBusiness);

        // Now classBusiness gets valid dependencies
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _relationshipBusiness, _eventBusiness);

        _metadataBusiness = new MetadataBusiness(
            Context, _classBusiness, _relationshipBusiness, _tagBusiness, _recordBusiness, _edgeBusiness);
    }

    # region Helper methods

    /// <summary>
    ///     This is our file factory in essence within this test suite. We can feed it a C# object,
    ///     and it will serialize it into a JSON file. We can then use this file
    ///     as if it was submitted to our endpoint originally.
    /// </summary>
    /// <param name="content">Structured C# object to be serialized.</param>
    /// <param name="filename">Name of file produced (just required for FormFile instantiation).</param>
    /// <returns>A file instance with contents in the form of JSON.</returns>
    private static FormFile CreateJsonFile(object content, string filename = "metadata.json")
    {
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);

        var file = new FormFile(stream, 0, bytes.Length, "file", filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };

        return file;
    }

    # endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        // Create test project
        var project = new Project
        {
            Name = "Project 1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false,
            OrganizationId = organizationId
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var dataSource = new DataSource
        {
            Name = "Test Datasource",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false, 
            OrganizationId = organizationId
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;

        var originClass = new Class
        {
            Name = "Origin Class",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        var destClass = new Class
        {
            Name = "Dest Class",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false, 
            OrganizationId = organizationId
        };
        Context.Classes.AddRange(originClass, destClass);
        await Context.SaveChangesAsync();
        cid = originClass.Id;
        cid2 = destClass.Id;

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
    }


    #region CreateMetadata Tests - Updated Class Assertions

    [Fact]
    public async Task CreateMetadata_Success_ReturnsIdAndCreatedAt()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var dto = new CreateMetadataRequestDto
        {
            Classes = new List<CreateClassRequestDto>
            {
                new()
                {
                    Name = "Test Metadata Class",
                    Description = "Test Description"
                }
            }
        };
        // Act
        var result = await _metadataBusiness.CreateMetadata(
            uid, pid, organizationId,did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Classes);
        Assert.True(result.Classes.First().Id > 0);
        Assert.True(result.Classes.First().LastUpdatedAt >= now);
        Assert.Equal("Test Metadata Class", result.Classes.First().Name);
        Assert.Equal("Test Description", result.Classes.First().Description);
        Assert.Equal(pid, result.Classes.First().ProjectId);
        Assert.Equal(organizationId, result.Classes.First().OrganizationId);
    }

    [Fact]
    public async Task CreateMetadata_Success_OnBulkCreate()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var dto = new CreateMetadataRequestDto
        {
            Classes = new List<CreateClassRequestDto>
            {
                new()
                {
                    Name = "Bulk Class 1",
                    Description = "First class"
                },
                new()
                {
                    Name = "Bulk Class 2",
                    Description = "Second class"
                }
            }
        };

            // Act
            var result = await _metadataBusiness.CreateMetadata(
                uid, pid, organizationId, did, dto);
            
            // Assert
            Assert.Equal(2, result.Classes.Count);
            Assert.True(result.Classes.All(c => c.LastUpdatedBy == uid && 
                                                c.ProjectId == pid && 
                                                !c.IsArchived));
            Assert.Equal("Bulk Class 1", result.Classes.First().Name);
            Assert.Equal("First class", result.Classes.First().Description);
            Assert.Equal("Bulk Class 2", result.Classes.Last().Name);
            Assert.Equal("Second class", result.Classes.Last().Description);

            // Ensure both create class events are logged
            var eventList = await Context.Events.ToListAsync();
            Assert.Equal(1, eventList.Count); // just one event is created containing the count with the bulk method
        }

    [Fact]
    public async Task CreateMetadata_Success_WithRecordsAndAutoClasses()
    {
        // Arrange
        var dto = new CreateMetadataRequestDto
        {
            Records = new List<CreateRecordRequestDto>
            {
                new()
                {
                    Name = "Test Record",
                    OriginalId = "rec-001",
                    ClassName = "Auto Class",
                    Description = "Test Description",
                    Properties = JsonObject.Parse("{\"test\": \"value\"}") as JsonObject, 
                }
            }
        };

        // Act
        var result = await _metadataBusiness.CreateMetadata(
            uid, pid, organizationId, did, dto);

        // Assert
        Assert.Single(result.Classes);
        Assert.Single(result.Records);
        Assert.Equal("Auto Class", result.Classes.First().Name);
        Assert.Equal(organizationId, result.Classes.First().OrganizationId);
        Assert.Equal("Test Record", result.Records.First().Name);
        Assert.Equal("rec-001", result.Records.First().OriginalId);
        Assert.Equal(result.Classes.First().Id, result.Records.First().ClassId);

        // // Ensure both create class and create record events are logged
        // var eventList = await Context.Events.ToListAsync();
        // Assert.Equal(2, eventList.Count);
        //
        // var actualEvent0 = eventList[0];
        // Assert.Equal(pid, actualEvent0.ProjectId);
        // Assert.Equal("create", actualEvent0.Operation);
        // Assert.Equal("class", actualEvent0.EntityType);
        // Assert.Equal(result.Classes[0].Id, actualEvent0.EntityId);
        //
        // var actualEvent1 = eventList[1];
        // Assert.Equal(pid, actualEvent1.ProjectId);
        // Assert.Equal("create", actualEvent1.Operation);
        // Assert.Equal("record", actualEvent1.EntityType);
        // Assert.Equal(result.Records[0].Id, actualEvent1.EntityId);
    }

    [Fact]
    public async Task CreateMetadata_Success_WithComplexMetadata()
    {
        // Arrange
        var dto = new CreateMetadataRequestDto
        {
            Classes = new List<CreateClassRequestDto>
            {
                new() { Name = "New Class" }
            },
            Relationships = new List<CreateRelationshipRequestDto>
            {
                new()
                {
                    Name = "Test Relationship"
                }
            },
            Tags = new List<CreateTagRequestDto>
            {
                new() { Name = "Test Tag" }
            },
            Records = new List<CreateRecordRequestDto>
            {
                new()
                {
                    Name = "Test Record",
                    OriginalId = "test-1",
                    Description = "Test Description",
                    Properties = JsonObject.Parse("{\"test\": \"value\"}") as JsonObject
                },
                new()
                {
                    Name = "Test Record 2",
                    OriginalId = "test-2",
                    Description = "Test Description 2",
                    Properties = JsonObject.Parse("{\"test2\": \"value 2\"}") as JsonObject
                }
            },
            Edges = new List<CreateEdgeRequestDto>
            {
                new()
                {
                    RelationshipName = "Test Relationship",
                    OriginOid = "test-1",
                    DestinationOid = "test-2"
                }
            }
        };

        var result = await _metadataBusiness.CreateMetadata(
            uid, pid, organizationId, did, dto);

        // Assert
        Assert.Single(result.Classes);
        Assert.Single(result.Relationships);
        Assert.Single(result.Tags);
        Assert.Equal(2, result.Records.Count);
        Assert.Single(result.Edges);

        var createdClass = result.Classes[0];
        Assert.Equal("New Class", createdClass.Name);
        Assert.Equal(pid, createdClass.ProjectId);
        Assert.Equal(organizationId, createdClass.OrganizationId);

        var createdRelationship = result.Relationships[0];
        Assert.Equal("Test Relationship", createdRelationship.Name);
        Assert.Equal(pid, createdRelationship.ProjectId);

        var createdTag = result.Tags[0];
        Assert.Equal("Test Tag", createdTag.Name);
        Assert.Equal(pid, createdTag.ProjectId);

        var createdRecord1 = result.Records[0];
        Assert.Equal("Test Record", createdRecord1.Name);
        Assert.Equal("test-1", createdRecord1.OriginalId);
        Assert.Equal("Test Description", createdRecord1.Description);
        Assert.NotNull(createdRecord1.Properties);
        Assert.Equal(pid, createdRecord1.ProjectId);
        Assert.Equal(did, createdRecord1.DataSourceId);

        var createdRecord2 = result.Records[1];
        Assert.Equal("Test Record 2", createdRecord2.Name);
        Assert.Equal("test-2", createdRecord2.OriginalId);
        Assert.Equal("Test Description 2", createdRecord2.Description);
        Assert.NotNull(createdRecord2.Properties);
        Assert.Equal(pid, createdRecord2.ProjectId);
        Assert.Equal(did, createdRecord2.DataSourceId);

        var createdEdge = result.Edges[0];
        Assert.Equal(createdRelationship.Id, createdEdge.RelationshipId);
        Assert.Equal(createdRecord1.Id, createdEdge.OriginId);
        Assert.Equal(createdRecord2.Id, createdEdge.DestinationId);
        Assert.Equal(pid, createdEdge.ProjectId);
        Assert.Equal(did, createdEdge.DataSourceId);

        // Ensure all complex data events are created and logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(5, eventList.Count);
    }

    #endregion

    #region CreateMetadataFromFile Tests - Updated Class Assertions

    [Fact]
    public async Task CreateMetadataFromFile_Success_ReturnsIdAndCreatedAt()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var metadataContent = new
        {
            classes = new[]
            {
                new
                {
                    name = "Test Metadata Class",
                    description = "Test Description"
                }
            }
        };

        var file = CreateJsonFile(metadataContent);

        // Act
        var result = await _metadataBusiness.CreateMetadataFromFile(
            uid, pid, organizationId, did, file);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Classes);
        Assert.True(result.Classes.First().Id > 0);
        Assert.True(result.Classes.First().LastUpdatedAt >= now);
        Assert.Equal("Test Metadata Class", result.Classes.First().Name);
        Assert.Equal("Test Description", result.Classes.First().Description);
        Assert.Equal(pid, result.Classes.First().ProjectId);
        Assert.Equal(organizationId, result.Classes.First().OrganizationId);

        // // Ensure create class event was logged
        // var eventList = await Context.Events.ToListAsync();
        // Assert.Single(eventList);
        //
        // var actualEvent = eventList[0];
        //
        // Assert.Equal(pid, actualEvent.ProjectId);
        // Assert.Equal("create", actualEvent.Operation);
        // Assert.Equal("class", actualEvent.EntityType);
        // Assert.Equal(result.Classes.First().Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateMetadataFromFile_Success_OnBulkCreate()
    {
        // Arrange
        var metadataContent = new
        {
            classes = new[]
            {
                new { name = "Bulk Class 1", description = "First class" },
                new { name = "Bulk Class 2", description = "Second class" }
            }
        };

        var file = CreateJsonFile(metadataContent);

        // Act
        var result = await _metadataBusiness.CreateMetadataFromFile(
            uid, pid, organizationId,did, file);

        // Assert
        Assert.Equal(2, result.Classes.Count);
        Assert.True(result.Classes.All(c => c.LastUpdatedBy == uid &&
                                            c.ProjectId == pid &&
                                            c.OrganizationId == organizationId &&
                                            !c.IsArchived));
        Assert.Equal("Bulk Class 1", result.Classes.First().Name);
        Assert.Equal("First class", result.Classes.First().Description);
        Assert.Equal("Bulk Class 2", result.Classes.Last().Name);
        Assert.Equal("Second class", result.Classes.Last().Description);

        // var eventList = await Context.Events.ToListAsync();
        // Assert.Equal(2, eventList.Count);
        //
        // var actualEvent0 = eventList[0];
        // Assert.Equal(pid, actualEvent0.ProjectId);
        // Assert.Equal("create", actualEvent0.Operation);
        // Assert.Equal("class", actualEvent0.EntityType);
        // Assert.Equal(result.Classes[0].Id, actualEvent0.EntityId);
        //
        // var actualEvent1 = eventList[1];
        // Assert.Equal(pid, actualEvent1.ProjectId);
        // Assert.Equal("create", actualEvent1.Operation);
        // Assert.Equal("class", actualEvent1.EntityType);
        // Assert.Equal(result.Classes[1].Id, actualEvent1.EntityId);
    }

    [Fact]
    public async Task CreateMetadataFromFile_Success_WithRecordsAndAutoClasses()
    {
        // Arrange
        var metadataContent = new
        {
            records = new[]
            {
                new
                {
                    name = "Test Record",
                    original_id = "rec-001",
                    class_name = "Auto Class",
                    description = "Test Description",
                    properties = new { test = "value" }
                }
            }
        };

        var file = CreateJsonFile(metadataContent);

        // Act
        var result = await _metadataBusiness.CreateMetadataFromFile(
            uid, pid, organizationId, did, file);

        // Assert
        Assert.Single(result.Classes);
        Assert.Single(result.Records);
        Assert.Equal("Auto Class", result.Classes.First().Name);
        Assert.Equal(organizationId, result.Classes.First().OrganizationId);
        Assert.Equal("Test Record", result.Records.First().Name);
        Assert.Equal("rec-001", result.Records.First().OriginalId);
        Assert.Equal(result.Classes.First().Id, result.Records.First().ClassId);

        // var eventList = await Context.Events.ToListAsync();
        // Assert.Equal(2, eventList.Count);
        //
        // var actualEvent0 = eventList[0];
        // Assert.Equal(pid, actualEvent0.ProjectId);
        // Assert.Equal("create", actualEvent0.Operation);
        // Assert.Equal("class", actualEvent0.EntityType);
        // Assert.Equal(result.Classes[0].Id, actualEvent0.EntityId);
        //
        // var actualEvent1 = eventList[1];
        // Assert.Equal(pid, actualEvent1.ProjectId);
        // Assert.Equal("create", actualEvent1.Operation);
        // Assert.Equal("record", actualEvent1.EntityType);
        // Assert.Equal(result.Records[0].Id, actualEvent1.EntityId);
    }

    [Fact]
    public async Task CreateMetadataFromFile_Success_WithComplexMetadata()
    {
        // Arrange
        var metadataContent = new
        {
            classes = new[]
            {
                new { name = "New Class" }
            },
            relationships = new[]
            {
                new
                {
                    name = "Test Relationship",
                    origin_id = cid,
                    destination_id = cid2
                }
            },
            tags = new[]
            {
                new { name = "Test Tag" }
            },
            records = new object[]
            {
                new
                {
                    name = "Test Record",
                    original_id = "test-1",
                    description = "Test Description",
                    properties = new { test = "value" }
                },
                new
                {
                    name = "Test Record 2",
                    original_id = "test-2",
                    description = "Test Description",
                    properties = new { test2 = "value 2" }
                }
            },
            edges = new[]
            {
                new
                {
                    relationship_name = "Test Relationship",
                    origin_oid = "test-1",
                    destination_oid = "test-2"
                }
            }
        };

        var file = CreateJsonFile(metadataContent);

        // Act
        var result = await _metadataBusiness.CreateMetadataFromFile(
            uid, pid, organizationId,did, file);

        // Assert
        Assert.Single(result.Classes);
        Assert.Single(result.Relationships);
        Assert.Single(result.Tags);
        Assert.Equal(2, result.Records.Count);
        Assert.Single(result.Edges);

        // Verify class has organization ID
        Assert.Equal(organizationId, result.Classes[0].OrganizationId);

        // Ensure all complex data events are created and logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Equal(5, eventList.Count);
    }


    [Fact]
    public async Task CreateMetadata_EdgeCanReferenceExistingRecordByOriginalId()
    {
        // Arrange: first upload creates record A
        var firstDto = new CreateMetadataRequestDto
        {
            Records = new List<CreateRecordRequestDto>
            {
                new()
                {
                    Name = "Record A",
                    OriginalId = "record-a",
                    Description = "Record A description",
                    Properties = JsonObject.Parse("{}") as JsonObject
                }
            }
        };

        await _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, firstDto);

        // Act: second upload creates record B and edge A -> B
        var secondDto = new CreateMetadataRequestDto
        {
            Records = new List<CreateRecordRequestDto>
            {
                new()
                {
                    Name = "Record B",
                    OriginalId = "record-b",
                    Description = "Record B description",
                    Properties = JsonObject.Parse("{}") as JsonObject
                }
            },
            Edges = new List<CreateEdgeRequestDto>
            {
                new()
                {
                    OriginOid = "record-a",
                    DestinationOid = "record-b"
                }
            }
        };

        var result = await _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, secondDto);

        // Assert
        Assert.NotNull(result.Edges);
        Assert.Single(result.Edges);
    }
    [Fact]
    public async Task CreateMetadata_ValidMetadataPersists_WhenEdgeReferencesMissingRecord()
    {
        // Arrange
        var dto = new CreateMetadataRequestDto
        {
            Classes = new List<CreateClassRequestDto>
            {
                new()
                {
                    Name = "Independent Class",
                    Description = "Class should persist"
                }
            },
            Relationships = new List<CreateRelationshipRequestDto>
            {
                new()
                {
                    Name = "Independent Relationship"
                }
            },
            Tags = new List<CreateTagRequestDto>
            {
                new()
                {
                    Name = "Independent Tag"
                }
            },
            Records = new List<CreateRecordRequestDto>
            {
                new()
                {
                    Name = "Record Independent",
                    OriginalId = "record-independent",
                    Description = "Record should persist",
                    ClassName = "Independent Class",
                    Tags = new List<string> { "Independent Tag" },
                    Properties = JsonObject.Parse("{}") as JsonObject
                }
            },
            Edges = new List<CreateEdgeRequestDto>
            {
                new()
                {
                    RelationshipName = "Independent Relationship",
                    OriginOid = "record-independent",
                    DestinationOid = "missing-record"
                }
            }
        };

        // Act
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, dto));

        // Assert
        Assert.Contains("missing-record", exception.Message);

        var createdClass = await Context.Classes
            .FirstOrDefaultAsync(c =>
                c.ProjectId == pid &&
                c.OrganizationId == organizationId &&
                c.Name == "Independent Class");

        Assert.NotNull(createdClass);

        var createdRelationship = await Context.Relationships
            .FirstOrDefaultAsync(r =>
                r.ProjectId == pid &&
                r.OrganizationId == organizationId &&
                r.Name == "Independent Relationship");

        Assert.NotNull(createdRelationship);

        var createdTag = await Context.Tags
            .FirstOrDefaultAsync(t =>
                t.ProjectId == pid &&
                t.OrganizationId == organizationId &&
                t.Name == "Independent Tag");

        Assert.NotNull(createdTag);

        var createdRecord = await Context.Records
            .FirstOrDefaultAsync(r =>
                r.ProjectId == pid &&
                r.DataSourceId == did &&
                r.OriginalId == "record-independent");

        Assert.NotNull(createdRecord);
        Assert.Equal(createdClass.Id, createdRecord.ClassId);

        var createdEdge = await Context.Edges
            .FirstOrDefaultAsync(e =>
                e.ProjectId == pid &&
                e.DataSourceId == did &&
                e.OriginId == createdRecord.Id);

        Assert.Null(createdEdge);
    }

    
    [Fact]
    public async Task CreateMetadata_Success_WithLargeRecordAndEdgeBatch()
    {
        const int count = 500;

        var records = Enumerable.Range(1, count)
            .Select(i => new CreateRecordRequestDto
            {
                Name = $"Record {i}",
                OriginalId = $"large-record-{i}",
                Description = $"Record {i} description",
                Properties = JsonObject.Parse("{}") as JsonObject
            })
            .ToList();

        var edges = Enumerable.Range(1, count - 1)
            .Select(i => new CreateEdgeRequestDto
            {
                OriginOid = $"large-record-{i}",
                DestinationOid = $"large-record-{i + 1}"
            })
            .ToList();

        var dto = new CreateMetadataRequestDto
        {
            Records = records,
            Edges = edges
        };

        var result = await _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, dto);

        Assert.Equal(count, result.Records.Count);
        Assert.Equal(count - 1, result.Edges.Count);
    }

[Fact]
    public async Task CreateMetadata_Success_WithLargeCrossBatchEdges()
    {
        const int count = 500;

        var firstBatch = new CreateMetadataRequestDto
        {
            Records = Enumerable.Range(1, count)
                .Select(i => new CreateRecordRequestDto
                {
                    Name = $"Existing Record {i}",
                    OriginalId = $"existing-record-{i}",
                    Description = $"Existing Record {i} description",
                    Properties = JsonObject.Parse("{}") as JsonObject
                })
                .ToList()
        };

        await _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, firstBatch);

        var secondBatch = new CreateMetadataRequestDto
        {
            Records = Enumerable.Range(1, count)
                .Select(i => new CreateRecordRequestDto
                {
                    Name = $"New Record {i}",
                    OriginalId = $"new-record-{i}",
                    Description = $"New Record {i} description",
                    Properties = JsonObject.Parse("{}") as JsonObject
                })
                .ToList(),

            Edges = Enumerable.Range(1, count)
                .Select(i => new CreateEdgeRequestDto
                {
                    OriginOid = $"existing-record-{i}",
                    DestinationOid = $"new-record-{i}"
                })
                .ToList()
        };

        var result = await _metadataBusiness.CreateMetadata(uid, pid, organizationId, did, secondBatch);

        Assert.Equal(count, result.Records.Count);
        Assert.Equal(count, result.Edges.Count);
    }    

    #endregion
}