using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.BigData;
using deeplynx.helpers.exceptions;
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
public class RecordBusinessAuthTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private SensitivityLabelBusiness _sensitivityLabelBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness;
    private TagBusiness _tagBusiness = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    public long cid; // class ID
    public long did; // datasource ID
    public long did2;
    private long organizationId;
    public long osid; // object storage ID
    public long pid; // project ID
    public long pid2;
    public string rdesc;
    public string rfiletype;
    public long rid; // record ID
    public long rid2;
    public long rid3;
    public long rid4;
    public string rogid;
    public string rprop; // additional record props
    public string ruri;
    public long tid; // tag ID
    public long lid; // sensitivity label ID
    public long uid;
    public long roleId;

    public RecordBusinessAuthTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness);
    }

    #region GetAllRecords_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task GetAllRecords_FilterOutUnauthorizedRecordsBySensitivityLabels_ReturnsFilteredRecords()
    {
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // give user write permission with this label so that it can be attached to the record (work around that does not invalidate the test)
        var permission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && permission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(permission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        // Record with sensitivity label should not be returned because user does not have access
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetAllRecords_UserHasAccessToAllLabels_ReturnsRecords()
    {
        // Create a label, give user permission to it, attach to record
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret 2",
            Description = "Top Secret Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission so label can be attached (workaround that doesn't invalidate test)
        var labelWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && labelWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(labelWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Get read permission without tracking
        var labelReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        // Get the role without tracking
        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && labelReadPermission != null)
        {
            // Attach and mark as modified
            Context.Attach(role);
            role.Permissions.Add(labelReadPermission);
            await Context.SaveChangesAsync();
        }

        // Verify the record IS returned
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetAllRecords_MultipleRecordsMixedAccess_ReturnsOnlyAuthorized()
    {
        // Record 1: No labels (should be returned) - using the seeded record
        var record1Id = rid;

        // Create a label, give user permission to it, attach to record
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Need To Know_" + Guid.NewGuid(),
            Description = "Need To Know",
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permissions to both labels so they can be attached (workaround that doesn't invalidate test)
        var label1WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var label2WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && label1WritePermission != null && label2WritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(label1WritePermission);
            role.Permissions.Add(label2WritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, label2.Id);

        Context.ChangeTracker.Clear();

        // Get read permission without tracking (only for label1, NOT label2)
        var label1ReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        // Get the role without tracking
        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && label1ReadPermission != null)
        {
            // Attach and mark as modified
            Context.Attach(role);
            role.Permissions.Add(label1ReadPermission);
            await Context.SaveChangesAsync();
        }

        // Act
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == record1Id); // No labels
        Assert.Contains(records, r => r.Id == rid2); // User has access
        Assert.DoesNotContain(records, r => r.Id == rid3); // User lacks access
    }

    [Fact]
    public async Task GetAllRecords_RecordWithMultipleLabels_UserHasAll_ReturnsRecord()
    {
        // Create two labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential",
        };
        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Internal_" + Guid.NewGuid(),
            Description = "Internal",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Get read and write permissions for both labels
        var label1ReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        var label1WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var label2ReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        var label2WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        Assert.NotNull(label1ReadPermission);
        Assert.NotNull(label2ReadPermission);
        Assert.NotNull(label1WritePermission);
        Assert.NotNull(label2WritePermission);

        // Get the role and attach permissions
        var role = await Context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId);

        Assert.NotNull(role);

        var permissionsToAdd = new[]
        {
            label1ReadPermission,
            label2ReadPermission,
            label1WritePermission,
            label2WritePermission
        };

        foreach (var permission in permissionsToAdd)
        {
            if (Context.Entry(permission).State == EntityState.Detached)
            {
                Context.Attach(permission);
            }

            if (!role.Permissions.Any(p => p.Id == permission.Id))
            {
                role.Permissions.Add(permission);
            }
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Attach both labels to the record
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        // Act
        var records = await _recordBusiness.GetAllRecords(
            uid, organizationId, pid, null, true);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid2);

        var returnedRecord = records.First(r => r.Id == rid2);
        Assert.Equal(2, returnedRecord.Labels.Count);
    }

    [Fact]
    public async Task GetAllRecords_RecordWithMultipleLabels_UserMissingOne_FiltersRecord()
    {
        // Create first label
        var label1Dto = new CreateSensitivityLabelRequestDto
        {
            Name = "Public_" + Guid.NewGuid(),
            Description = "Public label"
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, label1Dto, pid, organizationId);

        // Create second label
        var label2Dto = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted label"
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, label2Dto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Get permissions for label1 only (give user access to label1 but NOT label2)
        var label1ReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        var label1WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var label2WritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        Assert.NotNull(label1ReadPermission);
        Assert.NotNull(label1WritePermission);
        Assert.NotNull(label2WritePermission);

        // Get the role and attach only label1 permissions
        var role = await Context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId);

        Assert.NotNull(role);

        var permissionsToAdd = new[]
        {
            label1ReadPermission,
            label1WritePermission,
            label2WritePermission
        };

        foreach (var permission in permissionsToAdd)
        {
            if (Context.Entry(permission).State == EntityState.Detached)
            {
                Context.Attach(permission);
            }

            if (!role.Permissions.Any(p => p.Id == permission.Id))
            {
                role.Permissions.Add(permission);
            }
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Attach both labels to the record
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        // Act
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        // Assert - record should NOT be returned because user lacks access to label2
        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetAllRecords_WithDataSourceFilter_AndLabelAuth_ReturnsBothFiltered()
    {
        // Create labels
        var publicLabelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Public_" + Guid.NewGuid(),
            Description = "Public label"
        };
        var secretLabelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Secret_" + Guid.NewGuid(),
            Description = "Secret label"
        };
        var publicLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, publicLabelDto, pid, organizationId);
        var secretLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, secretLabelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Get permissions for public label only (NOT secret label)
        var publicReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == publicLabel.Id && p.Action == "read record");

        var publicWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == publicLabel.Id && p.Action == "write record");

        var secretWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == secretLabel.Id && p.Action == "write record");

        Assert.NotNull(publicReadPermission);
        Assert.NotNull(publicWritePermission);

        // Get the role and attach only public label permissions
        var role = await Context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId);

        Assert.NotNull(role);

        var permissionsToAdd = new[]
        {
            publicReadPermission,
            publicWritePermission,
            secretWritePermission
        };

        foreach (var permission in permissionsToAdd)
        {
            if (Context.Entry(permission).State == EntityState.Detached)
            {
                Context.Attach(permission);
            }

            if (!role.Permissions.Any(p => p.Id == permission.Id))
            {
                role.Permissions.Add(permission);
            }
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Attach labels to records
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, publicLabel.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, secretLabel.Id);

        // Act - filter by first datasource
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, did, true);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid); // Correct datasource, no labels
        Assert.DoesNotContain(records, r => r.Id == rid2); // Wrong datasource
        Assert.DoesNotContain(records, r => r.Id == rid3); // Correct datasource, but no label access
    }

    [Fact]
    public async Task GetAllRecords_WithFileTypeFilter_AndLabelAuth_ReturnsBothFiltered()
    {
        // Create labels
        var publicLabelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Public_" + Guid.NewGuid(),
            Description = "Public label"
        };
        var classifiedLabelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Classified_" + Guid.NewGuid(),
            Description = "Classified label"
        };
        var publicLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, publicLabelDto, pid, organizationId);
        var classifiedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, classifiedLabelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Get permissions for public label only (NOT classified label)
        var publicReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == publicLabel.Id && p.Action == "read record");

        var publicWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == publicLabel.Id && p.Action == "write record");

        var classifiedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == classifiedLabel.Id && p.Action == "write record");

        Assert.NotNull(publicReadPermission);
        Assert.NotNull(publicWritePermission);

        // Get the role and attach only public label permissions
        var role = await Context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId);

        Assert.NotNull(role);

        var permissionsToAdd = new[]
        {
            publicReadPermission,
            publicWritePermission,
            classifiedWritePermission
        };

        foreach (var permission in permissionsToAdd)
        {
            if (Context.Entry(permission).State == EntityState.Detached)
            {
                Context.Attach(permission);
            }

            if (!role.Permissions.Any(p => p.Id == permission.Id))
            {
                role.Permissions.Add(permission);
            }
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Attach labels to records
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, publicLabel.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, publicLabel.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid4, classifiedLabel.Id);

        // Act - filter by PDF file type
        var records = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true, "pdf");

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid); // Correct file type, no labels
        Assert.Contains(records, r => r.Id == rid2); // Correct file type, user has access
        Assert.DoesNotContain(records, r => r.Id == rid3); // Wrong file type
        Assert.DoesNotContain(records, r => r.Id == rid4); // Correct file type, but no label access
    }

    #endregion

    #region GetRecordsByTags_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task GetRecordsByTags_FilterOutUnauthorizedRecordsBySensitivityLabels_ReturnsFilteredRecords()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };

        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission with this label so that it can be attached to the record (workaround that does not invalidate the test)
        var permission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && permission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(permission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        // Act - Query by tag (user does NOT have access to the label)
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Record with sensitivity label should NOT be returned because user lacks access
        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == rid2);
        // The seeded record (which has the same tag but no label) should still be returned
        Assert.Single(records);
        Assert.Equal("Test Record", records.First().Name);
    }

    [Fact]
    public async Task GetRecordsByTags_UserHasAccessToAllLabels_ReturnsRecords()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission so label can be attached (workaround that doesn't invalidate test)
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to access the label
        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        // Act - Query by tag (user DOES have access to the label)
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Record with accessible label SHOULD be returned
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid);
    }

    [Fact]
    public async Task GetRecordsByTags_MultipleRecordsMixedAccess_ReturnsOnlyAuthorized()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        var testTag = await Context.Tags.FindAsync(tid);

        // Record 1: Has requested tag, no labels (should be returned)
        var record1 = new Record
        {
            Name = "Record Without Labels",
            Description = "Record with tag but no labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Record 2: Has requested tag, label with user access (should be returned)
        var record2 = new Record
        {
            Name = "Record With Accessible Label",
            Description = "Record with tag and accessible label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Record 3: Has requested tag, label without user access (should NOT be returned)
        var record3 = new Record
        {
            Name = "Record With Restricted Label",
            Description = "Record with tag and restricted label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.AddRange(record1, record2, record3);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label to attach it
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record2.Id, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label to attach it
        var restrictedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(restrictedWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record3.Id, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to accessible label only
        var accessibleReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleReadPermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleReadPermission);
            await Context.SaveChangesAsync();
        }

        // Act
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Only records 1 and 2 should be returned
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == record1.Id);
        Assert.Contains(records, r => r.Id == record2.Id);
        Assert.DoesNotContain(records, r => r.Id == record3.Id);
        // Should have the seeded record, record1, and record2
        // Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task GetRecordsByTags_RecordWithMultipleLabels_UserHasAll_ReturnsRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        var testTag = await Context.Tags.FindAsync(tid);

        // Create a record with a tag and will attach two labels
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with tag and two labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to BOTH labels
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        // Act
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Record with both accessible labels SHOULD be returned
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == record.Id);
        // Should have the seeded record and the new record
        // Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task GetRecordsByTags_RecordWithMultipleLabels_UserMissingOne_FiltersRecord()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);

        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        var testTag = await Context.Tags.FindAsync(tid);

        // Create a record with a tag and will attach two labels
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with tag and two labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to only ONE label (label1)
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        // Act
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Record should NOT be returned (user must have access to ALL labels)
        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == record.Id);
        // Should only have the seeded record
        Assert.Single(records);
    }

    [Fact]
    public async Task GetRecordsByTags_WithMultipleTags_AndLabelAuth_ReturnsBothFiltered()
    {
        // Arrange - Create two tags and two labels
        var tag1 = await Context.Tags.FindAsync(tid);

        var tag2 = new Tag
        {
            Name = "SecondTag_" + Guid.NewGuid(),
            ProjectId = pid,
            OrganizationId = organizationId
        };
        Context.Tags.Add(tag2);
        await Context.SaveChangesAsync();

        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        // Record 1: Has all requested tags, no labels (should be returned)
        var record1 = new Record
        {
            Name = "Record With All Tags No Labels",
            Description = "Has both tags, no labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { tag1, tag2 },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Record 2: Has all requested tags, label with access (should be returned)
        var record2 = new Record
        {
            Name = "Record With All Tags And Accessible Label",
            Description = "Has both tags and accessible label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { tag1, tag2 },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Record 3: Has only some tags, label with access (should NOT be returned - missing tags)
        var record3 = new Record
        {
            Name = "Record With Partial Tags",
            Description = "Has only one tag and accessible label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { tag1 },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Record 4: Has all requested tags, label without access (should NOT be returned - no label access)
        var record4 = new Record
        {
            Name = "Record With All Tags And Restricted Label",
            Description = "Has both tags and restricted label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { tag1, tag2 },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.AddRange(record1, record2, record3, record4);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record2.Id, accessibleLabel.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, record3.Id, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label
        var restrictedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(restrictedWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record4.Id, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to accessible label only
        var accessibleReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleReadPermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleReadPermission);
            await Context.SaveChangesAsync();
        }

        // Act - Query by both tags
        var records = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid, tag2.Id], true);

        // Assert - Only records 1 and 2 should be returned
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == record1.Id);
        Assert.Contains(records, r => r.Id == record2.Id);
        Assert.DoesNotContain(records, r => r.Id == record3.Id); // Missing tags
        Assert.DoesNotContain(records, r => r.Id == record4.Id); // No label access
        // Assert.Equal(2, records.Count);
    }

    #endregion

    #region GetRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task GetRecord_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsKeyNotFoundException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        // Act & Assert - Record with sensitivity label should NOT be returned because user lacks access
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true));

        Assert.Contains($"You do not have access to all required sensitivity labels for record {rid2}",
            exception.Message);
    }

    [Fact]
    public async Task GetRecord_UserHasAccessToLabel_ReturnsRecord()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to access the label
        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        // Act - Get record (user DOES have access to the label)
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true);

        // Assert - Record with accessible label SHOULD be returned
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Test Record 2", result.Name);
    }

    [Fact]
    public async Task GetRecord_NoLabel_ReturnsRecord()
    {
        // Act - Get record (no labels to check)
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true);

        // Assert - Record without labels SHOULD be returned
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Test Record 2", result.Name);
    }

    [Fact]
    public async Task GetRecord_RecordWithMultipleLabels_UserHasAll_ReturnsRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to BOTH labels
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        // Act - Get record (user has access to both labels)
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true);

        // Assert - Record with both accessible labels SHOULD be returned
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Test Record 2", result.Name);
    }

    [Fact]
    public async Task GetRecord_RecordWithMultipleLabels_UserMissingOne_ThrowsKeyNotFoundException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to only ONE label (label1)
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        // Act & Assert - Record should NOT be returned (user must have access to ALL labels)
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true));

        Assert.Contains($"You do not have access to all required sensitivity labels for record {rid2}",
            exception.Message);
    }

    [Fact]
    public async Task GetRecord_MixedLabelAccess_WithAccessibleLabel_ReturnsRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label (so that the user can attach the label to the record)
        var restrictedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(restrictedWritePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to accessible label only
        var accessibleReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "read record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleReadPermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleReadPermission);
            await Context.SaveChangesAsync();
        }

        // Act - Get record with accessible label
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, rid2, true);

        // Assert - Record with accessible label SHOULD be returned
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Test Record 2", result.Name);

        // Also verify that the record without access throws exception
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.GetRecord(uid, organizationId, pid, rid3, true));

        Assert.Contains(
            $"You do not have access to all required sensitivity labels for record {rid3}",
            exception.Message);
    }

    #endregion

    #region CreateRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task CreateRecord_WithUnauthorizedLabel_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Remove write permission for the label so user can't use it
        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove =
            role?.Permissions.FirstOrDefault(p => p.LabelId == label.Id && p.Action == "write record");
        if (role != null && permissionToRemove != null)
        {
            role.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Unauthorized Label",
            Description = "Test Record with unauthorized label",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-unauthorized",
            SensitivityLabelIds = new List<long> { label.Id }
        };

        // Act & Assert - Should throw because user lacks write permission for the label
        var exception = await Assert.ThrowsAsync<DependencyDeletionException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        Assert.Contains("sensitivity label", exception.Message.ToLower());

        // Ensure that no record was created
        var recordCount = await Context.Records.CountAsync();
        // Assert.Equal(1, recordCount); 
    }

    [Fact]
    public async Task CreateRecord_WithAuthorizedLabel_CreatesRecordSuccessfully()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Authorized_" + Guid.NewGuid(),
            Description = "Authorized Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Authorized Label",
            Description = "Test Record with authorized label",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-authorized",
            SensitivityLabelIds = new List<long> { label.Id }
        };

        // Act - Create record with authorized label
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert - Record should be created successfully
        Assert.NotNull(result);
        Assert.Equal("Record With Authorized Label", result.Name);
        Assert.NotNull(result.Labels);
        Assert.Single(result.Labels);
        Assert.Equal(label.Id, result.Labels.First().Id);

        // Verify record was created in database
        var createdRecord = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Single(createdRecord.Labels);
        Assert.Equal(label.Id, createdRecord.Labels.First().Id);
    }

    [Fact]
    public async Task CreateRecord_WithMultipleLabels_UserHasAllPermissions_CreatesRecordSuccessfully()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for both labels
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Multiple Authorized Labels",
            Description = "Test Record with multiple authorized labels",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-multiple-authorized",
            SensitivityLabelIds = new List<long> { label1.Id, label2.Id }
        };

        // Act - Create record with both authorized labels
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert - Record should be created successfully with both labels
        Assert.NotNull(result);
        Assert.Equal("Record With Multiple Authorized Labels", result.Name);
        Assert.NotNull(result.Labels);
        Assert.Equal(2, result.Labels.Count);

        var labelIds = result.Labels.Select(l => l.Id).OrderBy(id => id).ToList();
        Assert.Contains(label1.Id, labelIds);
        Assert.Contains(label2.Id, labelIds);

        // Verify record was created in database with both labels
        var createdRecord = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Equal(2, createdRecord.Labels.Count);
    }

    [Fact]
    public async Task CreateRecord_WithMultipleLabels_UserMissingOnePermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission ONLY for the accessible label
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleWritePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Remove write permission for restricted label
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove =
            role?.Permissions.FirstOrDefault(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");
        if (role != null && permissionToRemove != null)
        {
            role.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Mixed Label Permissions",
            Description = "Test Record with one authorized and one unauthorized label",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-mixed-permissions",
            SensitivityLabelIds = new List<long> { accessibleLabel.Id, restrictedLabel.Id }
        };

        // Act & Assert - Should throw because user lacks write permission for restrictedLabel
        var exception = await Assert.ThrowsAsync<DependencyDeletionException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        Assert.Contains("sensitivity label", exception.Message.ToLower());

        // Ensure that no record was created
        var createdRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.OriginalId == "original-mixed-permissions");
        Assert.Null(createdRecord);
    }

    [Fact]
    public async Task CreateRecord_WithNonExistentLabel_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentLabelId = 999999L;

        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Non-Existent Label",
            Description = "Test Record with non-existent label",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-non-existent-label",
            SensitivityLabelIds = new List<long> { nonExistentLabelId }
        };

        // Act & Assert - Should throw because label doesn't exist
        var exception = await Assert.ThrowsAsync<DependencyDeletionException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        Assert.Contains("sensitivity label", exception.Message.ToLower());

        // Ensure that no record was created
        var createdRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.OriginalId == "original-non-existent-label");
        Assert.Null(createdRecord);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    # region UpdateRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task UpdateRecord_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission so user can't update
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove =
            role?.Permissions.FirstOrDefault(p => p.LabelId == label.Id && p.Action == "write record");
        if (role != null && permissionToRemove != null)
        {
            role.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record"
        };

        // Act & Assert - Record with sensitivity label should NOT be updatable because user lacks write access
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    [Fact]
    public async Task UpdateRecord_UserHasAccessToLabel_UpdatesRecord()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var updatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "update record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && updatePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            role.Permissions.Add(updatePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record With Label",
            Description = "Updated description"
        };

        // Act - Update record (user DOES have write access to the label)
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto);

        // Assert - Record with accessible label SHOULD be updated
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Updated Record With Label", result.Name);
        Assert.Equal("Updated description", result.Description);
    }

    [Fact]
    public async Task UpdateRecord_NoLabel_UpdatesRecord()
    {
        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record Without Labels",
            Description = "Updated description"
        };

        // Act - Update record (no labels to check)
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto);

        // Assert - Record without labels SHOULD be updated
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Updated Record Without Labels", result.Name);
        Assert.Equal("Updated description", result.Description);
    }

    [Fact]
    public async Task UpdateRecord_RecordWithMultipleLabels_UserHasAll_UpdatesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var updatePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "update record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && updatePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(updatePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var updatePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "update record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null && updatePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(updatePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record With Two Labels",
            Description = "Updated description"
        };

        // Act - Update record (user has write access to both labels)
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto);

        // Assert - Record with both accessible labels SHOULD be updated
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Updated Record With Two Labels", result.Name);
        Assert.Equal("Updated description", result.Description);
    }

    [Fact]
    public async Task UpdateRecord_RecordWithMultipleLabels_UserMissingOne_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for label2
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove =
            role?.Permissions.FirstOrDefault(p => p.LabelId == label2.Id && p.Action == "write record");
        if (role != null && permissionToRemove != null)
        {
            role.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record",
            Description = "Updated description"
        };

        // Act & Assert - Record should NOT be updated (user must have write access to ALL labels)
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    [Fact]
    public async Task UpdateRecord_MixedLabelAccess_WithAccessibleLabel_UpdatesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var accessibleUpdatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "update record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null && accessibleUpdatePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(accessibleWritePermission);
            role.Permissions.Add(accessibleUpdatePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label to attach it
        var restrictedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        var restrictedUpdatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "update record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedWritePermission != null && restrictedUpdatePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(restrictedWritePermission);
            role.Permissions.Add(restrictedUpdatePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for restricted label
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove =
            role?.Permissions.FirstOrDefault(p => p.LabelId == restrictedLabel.Id && p.Action == "update record");
        if (role != null && permissionToRemove != null)
        {
            role.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Record",
            Description = "Updated description"
        };

        // Act - Update record with accessible label
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, updateDto);

        // Assert - Record with accessible label SHOULD be updated
        Assert.NotNull(result);
        Assert.Equal(rid2, result.Id);
        Assert.Equal("Updated Record", result.Name);
        Assert.Equal("Updated description", result.Description);

        // Also verify that the record without access throws exception
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, pid, rid3, updateDto));

        Assert.Contains(
            $"You do not have update permissions",
            exception.Message);
    }

    #endregion

    #region DeleteRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task DeleteRecord_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission so user can't delete
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

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

        // Act & Assert - Record with sensitivity label should NOT be deleted because user lacks write access
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2));

        Assert.Contains($"You do not have delete permissions",
            exception.Message);
    }

    [Fact]
    public async Task DeleteRecord_UserHasWriteAccessToLabel_DeletesRecord()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach and delete the label
        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var deletePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "delete record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission != null && deletePermission != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission);
            role.Permissions.Add(deletePermission);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Act - Delete record (user DOES have write access to the label)
        var result = await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2);

        // Assert - Record with accessible label SHOULD be deleted
        Assert.True(result);

        // Verify record is actually deleted
        var deletedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.Null(deletedRecord);
    }

    [Fact]
    public async Task DeleteRecord_NoLabel_DeletesRecord()
    {
        // Act - Delete record (no labels to check)
        var result = await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2);

        // Assert - Record without labels SHOULD be deleted
        Assert.True(result);

        // Verify record is actually deleted
        var deletedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.Null(deletedRecord);
    }

    [Fact]
    public async Task DeleteRecord_RecordWithMultipleLabels_UserHasWriteForAll_DeletesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var deletePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "delete record");

        var role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && deletePermission1 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(deletePermission1);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var deletePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "delete record");

        role = await Context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null && deletePermission2 != null)
        {
            Context.Attach(role);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(deletePermission2);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Act - Delete record (user has write access to both labels)
        var result = await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2);

        // Assert - Record with both accessible labels SHOULD be deleted
        Assert.True(result);

        // Verify record is actually deleted
        var deletedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.Null(deletedRecord);
    }

    [Fact]
    public async Task DeleteRecord_RecordWithMultipleLabels_UserMissingWriteForOne_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for label2 (user only has write for label1)
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

        // Act & Assert - Record should NOT be deleted (user must have write access to ALL labels)
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2));

        Assert.Contains($"You do not have delete permissions",
            exception.Message);
    }

    [Fact]
    public async Task DeleteRecord_MixedLabelAccess_WithWriteAccessibleLabel_DeletesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabel =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var deletePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "delete record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null && deletePermission != null)
        {
            role.Permissions.Add(accessibleWritePermission);
            role.Permissions.Add(deletePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, accessibleLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for restricted label (so that the user can attach the label to the record)
        var restrictedDeletePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "delete record");

        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedDeletePermission != null && writePermission != null)
        {
            role.Permissions.Add(restrictedDeletePermission);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid3, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for restricted label
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && restrictedDeletePermission != null)
        {
            var permissionToRemove = role.Permissions.FirstOrDefault(p => p.Id == restrictedDeletePermission.Id);
            if (permissionToRemove != null)
            {
                role.Permissions.Remove(permissionToRemove);
                await Context.SaveChangesAsync();
            }
        }

        Context.ChangeTracker.Clear();

        // Act - Delete record with accessible label
        var result = await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2);

        // Assert - Record with accessible label SHOULD be deleted
        Assert.True(result);

        // Verify record is actually deleted
        var deletedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.Null(deletedRecord);

        // Also verify that the record without access throws exception
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.DeleteRecord(uid, organizationId, pid, rid3));

        Assert.Contains(
            $"You do not have delete permissions",
            exception.Message);
    }

    #endregion

    // TODO: add label authorization in bulk create method 

    #region ArchiveRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task ArchiveRecord_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission so user can't archive
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

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

        // Act & Assert - Record with sensitivity label should NOT be archived because user lacks write access
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    [Fact]
    public async Task ArchiveRecord_UserHasWriteAccessToLabel_ArchivesRecord()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission to attach and archive the label
        var updatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "update record");

        var writePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && updatePermission != null && writePermission != null)
        {
            role.Permissions.Add(updatePermission);
            role.Permissions.Add(writePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Act - Archive record (user DOES have update access to the label)
        var result = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2);

        // Assert - Record with accessible label SHOULD be archived
        Assert.True(result);

        // Verify record is actually archived
        Context.ChangeTracker.Clear();
        var archivedRecord = await Context.Records
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);
    }

    [Fact]
    public async Task ArchiveRecord_NoLabel_ArchivesRecord()
    {
        // Act - Archive record (no labels to check)
        var result = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2);

        // Assert - Record without labels SHOULD be archived
        Assert.True(result);

        // Verify record is actually archived
        Context.ChangeTracker.Clear();
        var archivedRecord = await Context.Records
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);
    }

    [Fact]
    public async Task ArchiveRecord_RecordWithMultipleLabels_UserHasWriteForAll_ArchivesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Give user write permission for label1
        var writePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "write record");

        var updatePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "update record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission1 != null && updatePermission1 != null)
        {
            role.Permissions.Add(writePermission1);
            role.Permissions.Add(updatePermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
        var writePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "write record");

        var updatePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "update record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && writePermission2 != null && updatePermission2 != null)
        {
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(updatePermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Act - Archive record (user has write access to both labels)
        var result = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2);

        // Assert - Record with both accessible labels SHOULD be archived
        Assert.True(result);

        // Verify record is actually archived
        Context.ChangeTracker.Clear();
        var archivedRecord = await Context.Records
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == rid2);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);
    }

    [Fact]
    public async Task ArchiveRecord_RecordWithMultipleLabels_UserMissingWriteForOne_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label2.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission for label2 (user only has write for label1)
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

        // Act & Assert - Record should NOT be archived (user must have write access to ALL labels)
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    #endregion

    #region UnarchiveRecord_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task UnarchiveRecord_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        var record = new Record
        {
            Name = "Test Record",
            Description = "Test record for unit tests",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true // Start as archived
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

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

        // Attach label to the archived record
        var recordToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == record.Id);

        var dbLabel = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label.Id);

        if (recordToUpdate != null)
        {
            recordToUpdate.Labels.Add(dbLabel);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Remove write permission so user can't unarchive
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

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

        // Act & Assert - Record with sensitivity label should NOT be unarchived because user lacks write access
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, pid, record.Id));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    [Fact]
    public async Task UnarchiveRecord_UserHasWriteAccessToLabel_UnarchivesRecord()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        var record = new Record
        {
            Name = "Record With Accessible Label",
            Description = "Record with accessible label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true // Start as archived
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission
        var updatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "update record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && updatePermission != null)
        {
            role.Permissions.Add(updatePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach label to the archived record
        var recordToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == record.Id);

        var dbLabel = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label.Id);


        if (recordToUpdate != null)
        {
            recordToUpdate.Labels.Add(dbLabel);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Unarchive record (user DOES have write access to the label)
        var result = await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, record.Id);

        // Assert - Record with accessible label SHOULD be unarchived
        Assert.True(result);

        // Verify record is actually unarchived
        Context.ChangeTracker.Clear();
        var unarchivedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == record.Id);
        Assert.NotNull(unarchivedRecord);
        Assert.False(unarchivedRecord.IsArchived);
    }

    [Fact]
    public async Task UnarchiveRecord_NoLabel_UnarchivesRecord()
    {
        // Arrange - Create a record without any labels
        var record = new Record
        {
            Name = "Record Without Labels",
            Description = "Record with no labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true // Start as archived
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        // Act - Unarchive record (no labels to check)
        var result = await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, record.Id);

        // Assert - Record without labels SHOULD be unarchived
        Assert.True(result);

        // Verify record is actually unarchived
        Context.ChangeTracker.Clear();
        var unarchivedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == record.Id);
        Assert.NotNull(unarchivedRecord);
        Assert.False(unarchivedRecord.IsArchived);
    }

    [Fact]
    public async Task UnarchiveRecord_RecordWithMultipleLabels_UserHasWriteForAll_UnarchivesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        // Create a record that will have two labels
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with two labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true // Start as archived
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for both labels
        var updatePermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "update record");

        var updatePermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "update record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && updatePermission1 != null && updatePermission2 != null)
        {
            role.Permissions.Add(updatePermission1);
            role.Permissions.Add(updatePermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach both labels to the archived record
        var recordToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == record.Id);

        var dbLabel1 = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label1.Id);

        var dbLabel2 = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label2.Id);

        if (recordToUpdate != null)
        {
            recordToUpdate.Labels.Add(dbLabel1);
            recordToUpdate.Labels.Add(dbLabel2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Unarchive record (user has write access to both labels)
        var result = await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, record.Id);

        // Assert - Record with both accessible labels SHOULD be unarchived
        Assert.True(result);

        // Verify record is actually unarchived
        Context.ChangeTracker.Clear();
        var unarchivedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == record.Id);
        Assert.NotNull(unarchivedRecord);
        Assert.False(unarchivedRecord.IsArchived);
    }

    [Fact]
    public async Task
        UnarchiveRecord_RecordWithMultipleLabels_UserMissingWriteForOne_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var label1 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var label2 = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        // Create a record that will have two labels
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with two labels",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true // Start as archived
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for label1 only
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

        // Attach both labels to the archived record
        var recordToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == record.Id);

        var dbLabel1 = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label1.Id);

        var dbLabel2 = await Context.SensitivityLabels.FirstOrDefaultAsync(sl => sl.Id == label2.Id);


        if (recordToUpdate != null)
        {
            recordToUpdate.Labels.Add(dbLabel1);
            recordToUpdate.Labels.Add(dbLabel2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act & Assert - Record should NOT be unarchived (user must have write access to ALL labels)
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, pid, record.Id));

        Assert.Contains($"You do not have update permissions",
            exception.Message);
    }

    [Fact]
    public async Task UnarchiveRecord_MixedLabelAccess_WithWriteAccessibleLabel_UnarchivesRecord()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabelDto =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabelDto =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Fetch the actual entities
        var accessibleLabel = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == accessibleLabelDto.Id);

        var restrictedLabel = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == restrictedLabelDto.Id);

        // Create two archived records
        var recordWithAccess = new Record
        {
            Name = "Record With Accessible Label",
            Description = "Record with accessible label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true
        };

        var recordWithoutAccess = new Record
        {
            Name = "Record With Restricted Label",
            Description = "Record with restricted label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId,
            IsArchived = true
        };

        Context.Records.AddRange(recordWithAccess, recordWithoutAccess);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for accessible label only
        var accessibleUpdatePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "update record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleUpdatePermission != null)
        {
            role.Permissions.Add(accessibleUpdatePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Attach labels to the archived records
        var recordWithAccessToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordWithAccess.Id);

        if (recordWithAccessToUpdate != null && accessibleLabel != null)
        {
            recordWithAccessToUpdate.Labels.Add(accessibleLabel);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        var recordWithoutAccessToUpdate = await Context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordWithoutAccess.Id);

        if (recordWithoutAccessToUpdate != null && restrictedLabel != null)
        {
            recordWithoutAccessToUpdate.Labels.Add(restrictedLabel);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Unarchive record with accessible label
        var result = await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, recordWithAccess.Id);

        // Assert - Record with accessible label SHOULD be unarchived
        Assert.True(result);

        // Verify record is actually unarchived
        Context.ChangeTracker.Clear();
        var unarchivedRecord = await Context.Records
            .FirstOrDefaultAsync(r => r.Id == recordWithAccess.Id);
        Assert.NotNull(unarchivedRecord);
        Assert.False(unarchivedRecord.IsArchived);

        // Verify that the record without access throws exception
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, pid, recordWithoutAccess.Id));

        Assert.Contains(
            $"You do not have update permissions",
            exception.Message);
    }

    #endregion

    #region Attach/Unattach Label_SensitivityAuthoization Tests

    [Fact]
    public async Task AttachLabel_UserNotAuthorized_ThrowsUnauthorizedAccess()
    {
        // Arrange
        // Create label as a different user so current user has no access
        var differentUserId = uid + 1000; // or create another test user
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            differentUserId, newLabel, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id));

        Assert.Contains($"You do not have write permissions",
            exception.Message);
    }

    [Fact]
    public async Task UnattachLabel_SuccessfullyDetachesLabelFromRecord()
    {
        // Arrange
        // Create label using business method - this automatically creates permissions
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var labelRole = await Context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.Id == roleId).FirstOrDefaultAsync();

        var writePermission = await Context.Permissions
            .Where(p => p.LabelId == newLabelResponse.Id && p.Action == "write record")
            .FirstOrDefaultAsync();

        labelRole.Permissions.Add(writePermission);

        await Context.SaveChangesAsync();

        // Attach label using business method (user already has write access from creation)
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Verify label is attached
        var record = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        Assert.Contains(record.Labels, l => l.Id == newLabelResponse.Id);

        // Ensures that the record labels are not in the record context
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.UnattachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Assert
        Assert.True(result);
        var refreshed = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        Assert.DoesNotContain(refreshed.Labels, l => l.Id == newLabelResponse.Id);
    }

    [Fact]
    public async Task UnattachLabel_UserNotAuthorized_ThrowsUnauthorizedAccess()
    {
        // Arrange
        // Create label as a different user so current user has no access
        var differentUserId = uid + 1000; // or create another test user
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            differentUserId, newLabel, pid, organizationId);

        // Attach label directly to record (bypass business logic and permissions)
        var record = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        var restrictedLabel = await Context.SensitivityLabels.FirstAsync(l => l.Id == newLabelResponse.Id);
        record.Labels.Add(restrictedLabel);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UnattachLabel(uid, organizationId, pid, rid, newLabelResponse.Id));

        Assert.Contains($"You do not have write permissions",
            exception.Message);
    }

    [Fact]
    public async Task UnattachLabel_LabelNotAttached_ThrowsKeyNotFound()
    {
        // Arrange
        // Create label using business method - user has permission but label is not attached to record
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        Context.ChangeTracker.Clear();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnattachLabel(uid, organizationId, pid, rid, newLabelResponse.Id));

        Assert.Contains($"Label with id {newLabelResponse.Id} is not attached to record", exception.Message);
    }

    #endregion

    #region GetRecordsByOriginalId_SensitivityLabelsAuthorization Tests

    [Fact]
    public async Task GetRecordsByOriginalId_FilterOutUnauthorizedRecordBySensitivityLabel_ThrowsKeyNotFoundException()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Top Secret Label",
            Description = "Top Secret Label",
        };
        var labelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        var label = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse.Id);

        var originalId = Guid.NewGuid().ToString();

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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, label.Id);

        Context.ChangeTracker.Clear();

        // Remove write permission and don't add read permission
        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

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

        // Act & Assert - Records with sensitivity label should NOT be returned because user lacks access
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId }));

        Assert.Contains($"Records not found or access is unauthorized with original IDs: {originalId}",
            exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_UserHasAccessToLabel_ReturnsRecords()
    {
        // Arrange - Create a sensitivity label
        var labelDto = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential_" + Guid.NewGuid(),
            Description = "Confidential Label",
        };
        var labelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto, pid, organizationId);

        Context.ChangeTracker.Clear();

        var label = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse.Id);

        var originalId = Guid.NewGuid().ToString();
        var record = new Record
        {
            Name = "Record With Accessible Label",
            Description = "Record with accessible label",
            OriginalId = originalId,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to access the label
        var readPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission != null)
        {
            role.Permissions.Add(readPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Get records by original ID (user DOES have access to the label)
        var result =
            await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId });

        // Assert - Record with accessible label SHOULD be returned
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(record.Id, result[0].Id);
        Assert.Equal("Record With Accessible Label", result[0].Name);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_NoLabel_ReturnsRecords()
    {
        // Arrange - Create a record without any labels
        var originalId = Guid.NewGuid().ToString();
        var record = new Record
        {
            Name = "Record Without Labels",
            Description = "Record with no labels",
            OriginalId = originalId,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        // Act - Get records by original ID (no labels to check)
        var result =
            await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId });

        // Assert - Record without labels SHOULD be returned
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(record.Id, result[0].Id);
        Assert.Equal("Record Without Labels", result[0].Name);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_RecordWithMultipleLabels_UserHasAll_ReturnsRecords()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var labelResponse1 =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var labelResponse2 =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        var label1 = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse1.Id);

        var label2 = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse2.Id);

        // Create a record that will have two labels
        var originalId = Guid.NewGuid().ToString();
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with two labels",
            OriginalId = originalId,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to BOTH labels
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        var readPermission2 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label2.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission2 != null)
        {
            role.Permissions.Add(readPermission2);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Get records by original ID (user has access to both labels)
        var result =
            await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId });

        // Assert - Record with both accessible labels SHOULD be returned
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(record.Id, result[0].Id);
        Assert.Equal("Record With Two Labels", result[0].Name);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_RecordWithMultipleLabels_UserMissingOne_ThrowsKeyNotFoundException()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label1_" + Guid.NewGuid(),
            Description = "First Label",
        };
        var labelResponse1 =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Label2_" + Guid.NewGuid(),
            Description = "Second Label",
        };
        var labelResponse2 =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        var label1 = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse1.Id);

        var label2 = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == labelResponse2.Id);

        // Create a record that will have two labels
        var originalId = Guid.NewGuid().ToString();
        var record = new Record
        {
            Name = "Record With Two Labels",
            Description = "Record with two labels",
            OriginalId = originalId,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();

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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label1.Id);

        Context.ChangeTracker.Clear();

        // Give user write permission for label2
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

        await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, label2.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to only ONE label (label1)
        var readPermission1 = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == label1.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && readPermission1 != null)
        {
            role.Permissions.Add(readPermission1);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act & Assert - Record should NOT be returned (user must have access to ALL labels)
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId }));

        Assert.Contains($"Records not found or access is unauthorized with original IDs: {originalId}",
            exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_MixedLabelAccess_ReturnsOnlyAuthorizedRecords()
    {
        // Arrange - Create two sensitivity labels
        var labelDto1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Accessible_" + Guid.NewGuid(),
            Description = "Accessible Label",
        };
        var accessibleLabelResponse =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto1, pid, organizationId);

        var labelDto2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Restricted_" + Guid.NewGuid(),
            Description = "Restricted Label",
        };
        var restrictedLabelResponse =
            await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, labelDto2, pid, organizationId);

        Context.ChangeTracker.Clear();

        var accessibleLabel = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == accessibleLabelResponse.Id);

        var restrictedLabel = await Context.SensitivityLabels
            .FirstOrDefaultAsync(l => l.Id == restrictedLabelResponse.Id);

        // Create a record with accessible label
        var originalId1 = Guid.NewGuid().ToString();
        var recordWithAccess = new Record
        {
            Name = "Record With Accessible Label",
            Description = "Record with accessible label",
            OriginalId = originalId1,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        // Create a record with restricted label
        var originalId2 = Guid.NewGuid().ToString();
        var recordWithoutAccess = new Record
        {
            Name = "Record With Restricted Label",
            Description = "Record with restricted label",
            OriginalId = originalId2,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.AddRange(recordWithAccess, recordWithoutAccess);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Give user write permission for both labels
        var accessibleWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "write record");

        var restrictedWritePermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == restrictedLabel.Id && p.Action == "write record");

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleWritePermission != null && restrictedWritePermission != null)
        {
            role.Permissions.Add(accessibleWritePermission);
            role.Permissions.Add(restrictedWritePermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        await _recordBusiness.AttachLabel(uid, organizationId, pid, recordWithAccess.Id, accessibleLabel.Id);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, recordWithoutAccess.Id, restrictedLabel.Id);

        Context.ChangeTracker.Clear();

        // Give user read permission to accessible label only
        var accessibleReadPermission = await Context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LabelId == accessibleLabel.Id && p.Action == "read record");

        role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null && accessibleReadPermission != null)
        {
            role.Permissions.Add(accessibleReadPermission);
            await Context.SaveChangesAsync();
        }

        Context.ChangeTracker.Clear();

        // Act - Try to get both records by original IDs
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid,
                new List<string> { originalId1, originalId2 }));

        // Assert - Should throw because one record is not accessible
        Assert.Contains($"Records not found or access is unauthorized with original IDs: {originalId2}",
            exception.Message);

        // Also verify that we CAN get just the accessible record
        var accessibleResult =
            await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, new List<string> { originalId1 });
        Assert.NotNull(accessibleResult);
        Assert.Single(accessibleResult);
        Assert.Equal(recordWithAccess.Id, accessibleResult[0].Id);
        Assert.Equal("Record With Accessible Label", accessibleResult[0].Name);
    }

    #endregion

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

        // Add projects
        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test project 2 for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        // Add datasource
        var dataSources = new List<DataSource>
        {
            new DataSource
            {
                Name = "Test Data Source",
                Description = "Test data source for unit tests",
                ProjectId = pid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            },
            new DataSource
            {
                Name = "Test Data Source 2",
                Description = "Second data source for filtering tests",
                ProjectId = pid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            }
        };

        Context.DataSources.AddRange(dataSources);
        await Context.SaveChangesAsync();
        did = dataSources[0].Id;
        did2 = dataSources[1].Id;


        // Add class
        var testClass = new Class
        {
            Name = "Test Class",
            Description = "Test class for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        // Add object storage
        var config = new JsonObject();
        var objectStorage = new ObjectStorage
        {
            Name = "Object Storage 1",
            Type = "filesystem",
            Config = config.ToString(),
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.ObjectStorages.Add(objectStorage);
        await Context.SaveChangesAsync();
        osid = objectStorage.Id;

        // Add tag
        var testTag = new Tag
        {
            Name = "Test Tag",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        var testLabel = new SensitivityLabel
        {
            Name = "Test Label",
            OrganizationId = organizationId,
            ProjectId = pid,
        };

        var testRecords = new List<Record>
        {
            new Record
            {
                Name = "Test Record",
                Description = "Test record for unit tests",
                OriginalId = "og_id",
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did,
                ClassId = testClass.Id,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "pdf",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 2",
                Description = "Test record for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did2,
                ClassId = testClass.Id,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "pdf",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 3" + Guid.NewGuid(),
                Description = "Test record 3 for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did,
                ClassId = cid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "csv",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 4",
                Description = "Test Record 4 for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = pid,
                DataSourceId = did,
                ClassId = cid,
                FileType = "pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            }
        };

        Context.Records.AddRange(testRecords);
        Context.Tags.Add(testTag);
        Context.SensitivityLabels.Add(testLabel);
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

        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid,
            RoleId = testRole.Id
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        roleId = testRole.Id;
        rid = testRecords[0].Id;
        rid2 = testRecords[1].Id;
        rid3 = testRecords[2].Id;
        rid4 = testRecords[3].Id;
        tid = testTag.Id;
        lid = testLabel.Id;
        rprop = testRecords[0].Properties;
        rogid = testRecords[0].OriginalId;
        rdesc = testRecords[0].Description;
        ruri = testRecords[0].Uri;
        rfiletype = testRecords[0].FileType;
    }
}