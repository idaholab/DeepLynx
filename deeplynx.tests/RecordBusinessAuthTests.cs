using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
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
    private ISensitivityLabelService _sensitivityLabelService = null!;
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
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService);
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
    
    // TODO: add label authorization in bulk create method 

    #region Unattach Label_SensitivityAuthorization Tests

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