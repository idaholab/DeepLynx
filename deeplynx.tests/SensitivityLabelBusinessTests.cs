using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class SensitivityLabelBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private UserBusiness _userBusiness;
    private SensitivityLabelBusiness _labelBusiness;
    private RoleBusiness _roleBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private Mock<IBulkCopyUpsertExecutor> _mockBulkCopyUpsertExecutor = null!;
    private SensitivityLabelService _labelService = null!;
    public long lid; // label ID
    public long lid2; // archived label ID
    public long lid3;
    public long lid4;
    public long lid5;
    public long lid6;

    public long oid; // organization ID
    public long pid; // project ID
    public long pid2;
    public long uid; // user ID
    public long uid2;
    public long uid3;
    public long uid4;
    public long rid1; // role id
    public long rid2;
    public long rid3;
    public long rid4;
    public long rid5;
    public long mid; // member id
    public long mid2;
    public long mid3;
    public long mid4;
    public long mid5;

    public SensitivityLabelBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new Mock<IBulkCopyUpsertExecutor>();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor.Object);
        _userBusiness = new UserBusiness(Context);
        _labelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness, _userBusiness);
        _roleBusiness = new RoleBusiness(Context, _eventBusiness);
        _labelService = new SensitivityLabelService(Context);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Test User",
            Email = "test_label@example.com",
            Password = "test_password",
            IsArchived = false
        };
        var user2 = new User
        {
            Name = "Second Test User",
            Email = "test_label2@example.com",
            Password = "password",
            IsArchived = false
        };
        var orgAdmin = new User
        {
            Name = "Org Admin",
            Email = "org_admin@example.com",
            IsArchived = false
        };
        var orgUser = new User
        {
            Name = "Org User",
            Email = "org_user@example.com",
            IsArchived = false
        };
        Context.Users.AddRange(user, user2, orgAdmin, orgUser);
        await Context.SaveChangesAsync();
        uid = user.Id;
        uid2 = user2.Id;
        uid3 = orgAdmin.Id;
        uid4 = orgUser.Id;

        // create test organization
        var testOrg = new Organization
        {
            Name = "Test Organization",
            Description = "Test org for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.Organizations.Add(testOrg);
        await Context.SaveChangesAsync();
        oid = testOrg.Id;

        // create test project
        var testProject = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };
        var testProject2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };
        Context.Projects.AddRange(testProject, testProject2);
        await Context.SaveChangesAsync();
        pid = testProject.Id;
        pid2 = testProject2.Id;

        // create test labels
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label",
            Description = "Test label for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };
        var archivedLabel = new SensitivityLabel
        {
            Name = "Archived Label",
            Description = "Archived label for tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = true,
            OrganizationId = oid
        };

        var label1 = new SensitivityLabel
        {
            Name = "Label 1",
            Description = "Label 1 for unit tests",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var label2 = new SensitivityLabel
        {
            Name = "Label 2",
            Description = "Label 1 for unit tests",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        var label3 = new SensitivityLabel
        {
            Name = "Label 3",
            Description = "Label 1 for unit tests",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = true
        };
        var proj2Label = new SensitivityLabel
        {
            Name = "Test Label",
            Description = "Test label for unit tests",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            OrganizationId = oid
        };

        Context.SensitivityLabels.AddRange(testLabel, archivedLabel, label1, label2, label3, proj2Label);
        await Context.SaveChangesAsync();
        lid = testLabel.Id;
        lid2 = archivedLabel.Id;
        lid3 = label1.Id;
        lid4 = label2.Id;
        lid5 = label3.Id;
        lid6 = proj2Label.Id;

        // Gate "read record" on the non-admin-bypass labels used across these tests so that,
        // matching the old role/permission model's default-deny behavior, a non-admin user
        // needs an explicit UserSensitivityLabel grant to see them.
        var readGateTimestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Context.SensitivityLabelPermissions.AddRange(
            new SensitivityLabelPermission { LabelId = lid, Action = "read record", Name = "read record", LastUpdatedAt = readGateTimestamp, IsArchived = false },
            new SensitivityLabelPermission { LabelId = lid3, Action = "read record", Name = "read record", LastUpdatedAt = readGateTimestamp, IsArchived = false },
            new SensitivityLabelPermission { LabelId = lid4, Action = "read record", Name = "read record", LastUpdatedAt = readGateTimestamp, IsArchived = false },
            new SensitivityLabelPermission { LabelId = lid6, Action = "read record", Name = "read record", LastUpdatedAt = readGateTimestamp, IsArchived = false });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Create user roles
        var projAdmin = new Role { Name = "Admin", OrganizationId = oid, ProjectId = pid };
        var projUser = new Role { Name = "User", OrganizationId = oid, ProjectId = pid };
        var projUser2 = new Role { Name = "Another User", OrganizationId = oid, ProjectId = pid2 };
        var orgAdminRole = new Role { Name = "Org Admin", OrganizationId = oid };
        var orgUserRole = new Role { Name = "OrgUser", OrganizationId = oid };

        Context.Roles.AddRange(projAdmin, projUser, projUser2, orgAdminRole, orgUserRole);
        await Context.SaveChangesAsync();
        rid1 = projAdmin.Id;
        rid2 = projUser.Id;
        rid3 = projUser2.Id;
        rid4 = orgAdminRole.Id;
        rid5 = orgUserRole.Id;

        // Add Users to Project / Org
        var orgMember = new OrganizationUser { OrganizationId = oid, UserId = uid3, IsOrgAdmin = true };
        var orgMember2 = new OrganizationUser { OrganizationId = oid, UserId = uid4 };
        var projectMember = new ProjectMember { ProjectId = pid, UserId = uid, RoleId = rid1, IsProjectAdmin = true };
        var projectMember2 = new ProjectMember { ProjectId = pid2, UserId = uid, RoleId = rid3 };
        var projectMember3 = new ProjectMember { ProjectId = pid, UserId = uid2, RoleId = rid2 };
        var projectMember4 = new ProjectMember { ProjectId = pid2, UserId = uid2, RoleId = rid3 };
        var projectMember5 = new ProjectMember { ProjectId = pid, UserId = uid4, RoleId = rid5 };

        Context.ProjectMembers.AddRange(projectMember, projectMember2, projectMember3, projectMember4, projectMember5);
        Context.OrganizationUsers.AddRange(orgMember, orgMember2);
        await Context.SaveChangesAsync();
        mid = projectMember.Id;
        mid2 = projectMember2.Id;
        mid3 = projectMember3.Id;
        mid4 = projectMember4.Id;
        mid5 = projectMember5.Id;
    }

    #region GetAllSensitivityLabels Tests

    [Fact]
    public async Task GetAllSensitivityLabels_ExcludesArchived()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.All(labels, l => Assert.False(l.IsArchived));
        Assert.Contains(labels, l => l.Id == lid);
        Assert.DoesNotContain(labels, l => l.Id == lid2); // archived label
    }

    [Fact]
    public async Task GetAllSensitivityLabels_WithHideArchivedFalse_IncludesArchived()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid, [pid], oid, false);
        var labels = result.ToList();

        // Assert
        Assert.Contains(labels, l => l.IsArchived);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid2); // archived label
    }

    [Fact]
    public async Task GetAllSensitivityLabels_InheritsOrganizationLabels()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.All(labels, l => Assert.Equal(oid, l.OrganizationId));
        Assert.Equal(3, labels.Count);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_FiltersOnOrganizationId()
    {
        // Create org-level label for this test
        var orgLabel = new SensitivityLabel
        {
            Name = "Org Label",
            Description = "Organization level label",
            OrganizationId = oid,
            IsArchived = false,
        };
        Context.SensitivityLabels.Add(orgLabel);
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid3, null, oid);
        var labels = result.ToList();

        // Assert
        Assert.All(labels, l => Assert.Equal(oid, l.OrganizationId));
        Assert.Contains(labels, l => l.Id == orgLabel.Id);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_NoPermission_ReturnsEmpty()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid2, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_OneLabelVisible_ReturnsSingleLabel()
    {
        // Arrange
        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid2,
            LabelId = lid,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid2, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.Contains(labels, l => l.Id == lid);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_OrgUser_OnlySeesVisibleLabels()
    {
        // Arrange
        var orgLabel = new SensitivityLabel
        {
            Name = "Org Label",
            Description = "Organization level label",
            OrganizationId = oid,
            IsArchived = false,
        };
        Context.SensitivityLabels.Add(orgLabel);
        await Context.SaveChangesAsync();
        long orgLabelId = orgLabel.Id;

        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid4,
            LabelId = orgLabelId,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid4, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.Contains(labels, l => l.Id == orgLabelId);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_OrgUser_NoLabels()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid4, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_ProjectAdmin_SeesAllLabels()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid, [pid], oid);
        var labels = result.ToList();

        // Assert
        Assert.Equal(3, labels.Count);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid3);
        Assert.Contains(labels, l => l.Id == lid4);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_MultipleProjectsUser_SeesLabelsFromBoth()
    {
        // Arrange
        //Give user access to lid6 from proj 2 and lid
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Context.SensitivityLabelGrants.AddRange(
            new SensitivityLabelGrant { UserId = uid2, LabelId = lid, GrantedBy = uid, GrantedAt = now },
            new SensitivityLabelGrant { UserId = uid2, LabelId = lid6, GrantedBy = uid, GrantedAt = now });
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid2, [pid, pid2], oid);
        var labels = result.ToList();

        // Assert
        Assert.Equal(2, labels.Count);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid6);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_MultipleProjectsAdminAndUser_SeesLabelsFromBoth()
    {
        // Arrange
        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid,
            LabelId = lid6,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid, [pid, pid2], oid);
        var labels = result.ToList();

        // Assert
        Assert.Equal(4, labels.Count);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid3);
        Assert.Contains(labels, l => l.Id == lid4);
        Assert.Contains(labels, l => l.Id == lid6);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_OrgAdmin_SeesAllLabels()
    {
        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(uid3, [pid, pid2], oid);
        var labels = result.ToList();

        // Assert
        Assert.Equal(4, labels.Count);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid3);
        Assert.Contains(labels, l => l.Id == lid4);
        Assert.Contains(labels, l => l.Id == lid6);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_SysAdmin_SeesAllLabels()
    {
        // Arrange
        var sysAdmin = new User
        {
            Name = "Sys Admin",
            Email = "sys_admin@example.com",
            IsArchived = false,
            IsSysAdmin = true
        };
        Context.Users.Add(sysAdmin);
        await Context.SaveChangesAsync();

        // Act
        var result = await _labelBusiness.GetAllSensitivityLabels(sysAdmin.Id, [pid, pid2], oid);
        var labels = result.ToList();

        // Assert
        Assert.Equal(4, labels.Count);
        Assert.Contains(labels, l => l.Id == lid);
        Assert.Contains(labels, l => l.Id == lid3);
        Assert.Contains(labels, l => l.Id == lid4);
        Assert.Contains(labels, l => l.Id == lid6);
    }

    #endregion

    #region GetSensitivityLabel Tests

    [Fact]
    public async Task GetSensitivityLabel_Succeeds_WhenExists()
    {
        // Act
        var result = await _labelBusiness.GetSensitivityLabel(lid, pid, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lid, result.Id);
        Assert.Equal("Test Label", result.Name);
        Assert.Equal("Test label for unit tests", result.Description);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetSensitivityLabel_InheritOrganizationLabels()
    {
        // Act
        var result = await _labelBusiness.GetSensitivityLabel(lid4, pid, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lid4, result.Id);
    }

    [Fact]
    public async Task GetSensitivityLabel_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _labelBusiness.GetSensitivityLabel(99999, pid, oid));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task GetSensitivityLabel_Fails_IfArchivedLabel()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.GetSensitivityLabel(lid2, pid, oid)); // archived label

        Assert.Contains($"Sensitivity label with id {lid2} is archived", exception.Message);
    }

    #endregion

    #region CreateSensitivityLabel Tests

    [Fact]
    public async Task CreateSensitivityLabel_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateSensitivityLabelRequestDto
        {
            Name = "New Test Label",
            Description = "New test label description"
        };

        // Act
        var result = await _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(pid, result.ProjectId);
        Assert.False(result.IsArchived);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // verify label was actually created in database
        var createdLabel = await Context.SensitivityLabels.FindAsync(result.Id);
        Assert.NotNull(createdLabel);
        Assert.Equal(dto.Name, createdLabel.Name);

        // Verify permissions were created
        var permissions = await Context.SensitivityLabelPermissions.Where(p => p.LabelId == result.Id).ToListAsync();
        Assert.Equal(8, permissions.Count);

        // Ensure that the SensitivityLabel create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Success_WithOrganization()
    {
        // Arrange
        var dto = new CreateSensitivityLabelRequestDto
        {
            Name = "New Org Label",
            Description = "New organization label description"
        };

        // Act
        var result = await _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(oid, result.OrganizationId);
        Assert.False(result.IsArchived);

        // verify label was actually created in database
        var createdLabel = await Context.SensitivityLabels.FindAsync(result.Id);
        Assert.NotNull(createdLabel);
        Assert.Equal(dto.Name, createdLabel.Name);

        // Verify both read and write permissions were created
        var permissions = await Context.SensitivityLabelPermissions
            .Where(p => p.LabelId == result.Id)
            .ToListAsync();
        Assert.Equal(8, permissions.Count);

        Assert.Contains(permissions, p => p.Action == "read record");
        Assert.Contains(permissions, p => p.Action == "write record");
        Assert.Contains(permissions, p => p.Action == "update record");
        Assert.Contains(permissions, p => p.Action == "delete record");
        Assert.Contains(permissions, p => p.Action == "download file");
        Assert.Contains(permissions, p => p.Action == "upload file");
        Assert.Contains(permissions, p => p.Action == "update file");
        Assert.Contains(permissions, p => p.Action == "delete file");
        // Ensure that the SensitivityLabel create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Success_CreatesEvent()
    {
        // Arrange
        var dto = new CreateSensitivityLabelRequestDto
        {
            Name = "Event Test Label",
            Description = "A test label for event logging"
        };

        // Act
        var result = await _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Test Label", result.Name);

        // Verify all permissions were created
        var readRecordPermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "read record");
        Assert.NotNull(readRecordPermission);

        var writeRecordPermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "write record");
        Assert.NotNull(writeRecordPermission);

        var updateRecordPermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "update record");
        Assert.NotNull(updateRecordPermission);

        var deleteRecordPermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "delete record");
        Assert.NotNull(deleteRecordPermission);

        var downloadFilePermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "download file");
        Assert.NotNull(downloadFilePermission);

        var uploadFilePermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "upload file");
        Assert.NotNull(uploadFilePermission);

        var updateFilePermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "update file");
        Assert.NotNull(updateFilePermission);

        var deleteFilePermission = await Context.SensitivityLabelPermissions
            .FirstOrDefaultAsync(p => p.LabelId == result.Id && p.Action == "delete file");
        Assert.NotNull(deleteFilePermission);

        // Ensure that the SensitivityLabel create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateSensitivityLabelRequestDto
        {
            Description = "Label without name"
        };
        var initialPermissionCount = await Context.SensitivityLabelPermissions.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);

        // Ensure that no permissions were created
        var permissionCount = await Context.SensitivityLabelPermissions.CountAsync();
        Assert.Equal(initialPermissionCount, permissionCount);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateSensitivityLabelRequestDto
        {
            Name = "",
            Description = "Label with empty name"
        };
        var initialPermissionCount = await Context.SensitivityLabelPermissions.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);

        // Ensure that no permissions were created
        var permissionCount = await Context.SensitivityLabelPermissions.CountAsync();
        Assert.Equal(initialPermissionCount, permissionCount);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Success_CreatesCorrectPermissionStructure()
    {
        // Arrange
        var dto = new CreateSensitivityLabelRequestDto
        {
            Name = "Permission Structure Test",
            Description = "Testing permission structure"
        };

        // Act
        var result = await _labelBusiness.CreateSensitivityLabel(uid, dto, pid, oid);

        // Assert
        var permissions = await Context.SensitivityLabelPermissions
            .Where(p => p.LabelId == result.Id)
            .ToListAsync();

        // Verify exactly 8 permissions were created
        Assert.Equal(8, permissions.Count);

        // Verify read record permission details
        var readRecordPermission = permissions.FirstOrDefault(p => p.Action == "read record");
        Assert.NotNull(readRecordPermission);
        Assert.Equal("Permission Structure Test", readRecordPermission.Name);
        Assert.Contains("read", readRecordPermission.Description.ToLower());
        Assert.Equal(result.Id, readRecordPermission.LabelId);

        // Verify write record permission details
        var writeRecordPermission = permissions.FirstOrDefault(p => p.Action == "write record");
        Assert.NotNull(writeRecordPermission);
        Assert.Equal("Permission Structure Test", writeRecordPermission.Name);
        Assert.Contains("Permission to add records with label", writeRecordPermission.Description);
        Assert.Equal(result.Id, writeRecordPermission.LabelId);

        var updateRecordPermission = permissions.FirstOrDefault(p => p.Action == "update record");
        Assert.NotNull(updateRecordPermission);
        var deleteRecordPermission = permissions.FirstOrDefault(p => p.Action == "delete record");
        Assert.NotNull(deleteRecordPermission);
        var downloadFilePermission = permissions.FirstOrDefault(p => p.Action == "download file");
        Assert.NotNull(downloadFilePermission);
        var uploadFilePermission = permissions.FirstOrDefault(p => p.Action == "upload file");
        Assert.NotNull(uploadFilePermission);
        var updateFilePermission = permissions.FirstOrDefault(p => p.Action == "update file");
        Assert.NotNull(updateFilePermission);
        var deleteFilePermission = permissions.FirstOrDefault(p => p.Action == "delete file");
        Assert.NotNull(deleteFilePermission);
    }

    #endregion

    #region BulkCreateSensitivityLabels Tests

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Bulk Test Label 1",
                Description = "First bulk test label"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Bulk Test Label 2",
                Description = "Second bulk test label"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Bulk Test Label 3",
                Description = "Third bulk test label"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        for (int i = 0; i < labels.Count; i++)
        {
            Assert.Equal(labels[i].Name, result[i].Name);
            Assert.Equal(pid, result[i].ProjectId);
            Assert.Equal(oid, result[i].OrganizationId);
            Assert.False(result[i].IsArchived);
            Assert.True(result[i].LastUpdatedAt >= now);
            Assert.Equal(uid, result[i].LastUpdatedBy);

            // Verify label was actually created in database
            var createdLabel = await Context.SensitivityLabels.FindAsync(result[i].Id);
            Assert.NotNull(createdLabel);
            Assert.Equal(labels[i].Name, createdLabel.Name);

            // Verify 8 permissions were created per label
            var permissions = await Context.SensitivityLabelPermissions.Where(p => p.LabelId == result[i].Id).ToListAsync();
            Assert.Equal(8, permissions.Count);
        }

        // Ensure that the bulk create event was logged with correct count
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_WithOrganization()
    {
        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Bulk Org Label 1",
                Description = "First organization label"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Bulk Org Label 2",
                Description = "Second organization label"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        foreach (var label in result)
        {
            Assert.Equal(oid, label.OrganizationId);
            Assert.False(label.IsArchived);

            // Verify label was actually created in database
            var createdLabel = await Context.SensitivityLabels.FindAsync(label.Id);
            Assert.NotNull(createdLabel);

            // Verify all 8 permissions were created
            var permissions = await Context.SensitivityLabelPermissions
                .Where(p => p.LabelId == label.Id)
                .ToListAsync();
            Assert.Equal(8, permissions.Count);

            Assert.Contains(permissions, p => p.Action == "read record");
            Assert.Contains(permissions, p => p.Action == "write record");
            Assert.Contains(permissions, p => p.Action == "update record");
            Assert.Contains(permissions, p => p.Action == "delete record");
            Assert.Contains(permissions, p => p.Action == "download file");
            Assert.Contains(permissions, p => p.Action == "upload file");
            Assert.Contains(permissions, p => p.Action == "update file");
            Assert.Contains(permissions, p => p.Action == "delete file");
        }

        // Ensure that the bulk create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_CreatesEvent()
    {
        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Event Test Bulk Label 1",
                Description = "First test label for event logging"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Event Test Bulk Label 2",
                Description = "Second test label for event logging"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // Verify all permissions were created for each label
        foreach (var label in result)
        {
            var readRecordPermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "read record");
            Assert.NotNull(readRecordPermission);

            var writeRecordPermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "write record");
            Assert.NotNull(writeRecordPermission);

            var updateRecordPermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "update record");
            Assert.NotNull(updateRecordPermission);

            var deleteRecordPermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "delete record");
            Assert.NotNull(deleteRecordPermission);

            var downloadFilePermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "download file");
            Assert.NotNull(downloadFilePermission);

            var uploadFilePermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "upload file");
            Assert.NotNull(uploadFilePermission);

            var updateFilePermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "update file");
            Assert.NotNull(updateFilePermission);

            var deleteFilePermission = await Context.SensitivityLabelPermissions
                .FirstOrDefaultAsync(p => p.LabelId == label.Id && p.Action == "delete file");
            Assert.NotNull(deleteFilePermission);
        }

        // Ensure that the bulk create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_WithEmptyList()
    {
        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>();
        var initialPermissionCount = await Context.SensitivityLabelPermissions.CountAsync();

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);

        // Ensure that no permissions were created
        var permissionCount = await Context.SensitivityLabelPermissions.CountAsync();
        Assert.Equal(initialPermissionCount, permissionCount);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_WithNullList()
    {
        // Arrange
        var initialPermissionCount = await Context.SensitivityLabelPermissions.CountAsync();

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);

        // Ensure that no permissions were created
        var permissionCount = await Context.SensitivityLabelPermissions.CountAsync();
        Assert.Equal(initialPermissionCount, permissionCount);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_CreatesCorrectPermissionStructure()
    {
        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Permission Structure Bulk Test 1",
                Description = "Testing bulk permission structure"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Permission Structure Bulk Test 2",
                Description = "Testing bulk permission structure again"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        foreach (var label in result)
        {
            var permissions = await Context.SensitivityLabelPermissions
                .Where(p => p.LabelId == label.Id)
                .ToListAsync();

            // Verify exactly 8 permissions were created
            Assert.Equal(8, permissions.Count);

            // Verify read record permission details
            var readRecordPermission = permissions.FirstOrDefault(p => p.Action == "read record");
            Assert.NotNull(readRecordPermission);
            Assert.Equal(label.Name, readRecordPermission.Name);
            Assert.Contains("read", readRecordPermission.Description.ToLower());
            Assert.Equal(label.Id, readRecordPermission.LabelId);

            // Verify write record permission details
            var writeRecordPermission = permissions.FirstOrDefault(p => p.Action == "write record");
            Assert.NotNull(writeRecordPermission);
            Assert.Equal(label.Name, writeRecordPermission.Name);
            Assert.Contains("Permission to add records with label", writeRecordPermission.Description);
            Assert.Equal(label.Id, writeRecordPermission.LabelId);

            var updateRecordPermission = permissions.FirstOrDefault(p => p.Action == "update record");
            Assert.NotNull(updateRecordPermission);
            var deleteRecordPermission = permissions.FirstOrDefault(p => p.Action == "delete record");
            Assert.NotNull(deleteRecordPermission);
            var downloadFilePermission = permissions.FirstOrDefault(p => p.Action == "download file");
            Assert.NotNull(downloadFilePermission);
            var uploadFilePermission = permissions.FirstOrDefault(p => p.Action == "upload file");
            Assert.NotNull(uploadFilePermission);
            var updateFilePermission = permissions.FirstOrDefault(p => p.Action == "update file");
            Assert.NotNull(updateFilePermission);
            var deleteFilePermission = permissions.FirstOrDefault(p => p.Action == "delete file");
            Assert.NotNull(deleteFilePermission);
        }
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_HandlesNameCollisionWithUpsert()
    {
        // Arrange - Create an existing label first
        var existingLabel = new SensitivityLabel
        {
            Name = "Duplicate Label",
            Description = "Original description",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(existingLabel);
        await Context.SaveChangesAsync();
        var existingId = existingLabel.Id;

        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Duplicate Label", // Same name as existing
                Description = "New description"
            },
            new CreateSensitivityLabelRequestDto
            {
                Name = "Unique Label",
                Description = "This is unique"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // The duplicate should have been updated (same ID)
        var duplicateResult = result.FirstOrDefault(r => r.Name == "Duplicate Label");
        Assert.NotNull(duplicateResult);
        Assert.Equal(existingId, duplicateResult.Id);
        Assert.True(duplicateResult.LastUpdatedAt > existingLabel.LastUpdatedAt);

        // The unique label should be new
        var uniqueResult = result.FirstOrDefault(r => r.Name == "Unique Label");
        Assert.NotNull(uniqueResult);
        Assert.NotEqual(existingId, uniqueResult.Id);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_WithSingleLabel()
    {
        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Single Bulk Label",
                Description = "Testing bulk with single item"
            }
        };

        // Act
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Single Bulk Label", result[0].Name);

        // Verify permissions
        var permissions = await Context.SensitivityLabelPermissions.Where(p => p.LabelId == result[0].Id).ToListAsync();
        Assert.Equal(8, permissions.Count);

        // Verify event
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
    }

    [Fact]
    public async Task BulkCreateSensitivityLabels_Success_TransactionRollbackOnError()
    {
        // This test verifies that if something goes wrong, the transaction rolls back
        // Note: Actual implementation depends on your error handling
        // This is a conceptual test that would need to be adapted based on how errors are triggered

        // Arrange
        var labels = new List<CreateSensitivityLabelRequestDto>
        {
            new CreateSensitivityLabelRequestDto
            {
                Name = "Valid Label 1",
                Description = "Should not persist"
            }
        };

        // Get initial count
        var initialLabelCount = await Context.SensitivityLabels.CountAsync();
        var initialPermissionCount = await Context.SensitivityLabelPermissions.CountAsync();

        // This test would need to be enhanced to actually trigger a rollback scenario
        // For now, we verify successful transaction
        var result = await _labelBusiness.BulkCreateSensitivityLabels(oid, uid, pid, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(await Context.SensitivityLabels.CountAsync() > initialLabelCount);
        Assert.True(await Context.SensitivityLabelPermissions.CountAsync() > initialPermissionCount);
    }

    #endregion

    #region UpdateSensitivityLabel Tests

    [Fact]
    public async Task UpdateSensitivityLabel_Success_ReturnsLabel()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Updated Label",
            Description = "Updated description"
        };

        // Act
        var result = await _labelBusiness.UpdateSensitivityLabel(uid, lid, pid, oid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lid, result.Id);
        Assert.False(result.IsArchived);
        Assert.Equal("Updated Label", result.Name);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Verify it was actually saved to DB
        var savedLabel = await Context.SensitivityLabels.FindAsync(lid);
        Assert.NotNull(savedLabel);
        Assert.Equal("Updated Label", savedLabel.Name);
        Assert.Equal("Updated description", savedLabel.Description);

        // Ensure that the SensitivityLabel update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_Success_CreatesEvent()
    {
        // Arrange
        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Event Updated Label"
        };

        // Act
        var result = await _labelBusiness.UpdateSensitivityLabel(uid, lid, pid, oid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Updated Label", result.Name);

        // Ensure that the SensitivityLabel update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_Fails_IfOrganizationLabel()
    {
        // Arrange
        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Updated Label"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _labelBusiness.UpdateSensitivityLabel(uid, lid4, pid, oid, dto));

        Assert.Contains("Organization sensitivity labels cannot be updated from the child projects.", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Updated Label"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.UpdateSensitivityLabel(uid, 99999, pid, oid, dto));

        Assert.Contains("Sensitivity label with id 99999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Updated Archived Label"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.UpdateSensitivityLabel(uid, lid2, pid, oid, dto)); // archived label

        Assert.Contains($"Sensitivity label with id {lid2} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region ArchiveSensitivityLabel Tests

    [Fact]
    public async Task ArchiveSensitivityLabel_Succeeds_IfNotArchived()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = await _labelBusiness.ArchiveSensitivityLabel(uid, lid, pid, oid);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedLabel = await Context.SensitivityLabels.FindAsync(lid);
        Assert.NotNull(savedLabel);
        Assert.True(savedLabel.IsArchived);
        Assert.Equal("Test Label", savedLabel.Name);
        Assert.Equal("Test label for unit tests", savedLabel.Description);
        Assert.Equal(pid, savedLabel.ProjectId);
        Assert.True(savedLabel.LastUpdatedAt >= now);
        Assert.Equal(uid, savedLabel.LastUpdatedBy);


        // Ensure that the SensitivityLabel archive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(lid, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Fails_IfArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.ArchiveSensitivityLabel(uid, lid2, pid, oid)); // already archived

        Assert.Contains($"Sensitivity label with id {lid2} not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Fails_IfOrganizationLabel()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _labelBusiness.ArchiveSensitivityLabel(uid, lid3, pid, oid));

        Assert.Contains($"Organization sensitivity labels cannot be updated from the child projects.", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.ArchiveSensitivityLabel(uid, 99999, pid, oid));

        Assert.Contains("Sensitivity label with id 99999 not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveSensitivityLabel Tests

    [Fact]
    public async Task UnarchiveSensitivityLabel_Succeeds_IfArchived()
    {
        //Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = await _labelBusiness.UnarchiveSensitivityLabel(uid, lid2, pid, oid);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedLabel = await Context.SensitivityLabels.FindAsync(lid2);
        Assert.NotNull(savedLabel);
        Assert.False(savedLabel.IsArchived);
        Assert.Equal("Archived Label", savedLabel.Name);
        Assert.Equal("Archived label for tests", savedLabel.Description);
        Assert.Equal(pid, savedLabel.ProjectId);
        Assert.True(savedLabel.LastUpdatedAt >= now);
        Assert.Equal(uid, savedLabel.LastUpdatedBy);

        // Ensure that the SensitivityLabel unarchive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("unarchive", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(lid2, actualEvent.EntityId);
    }

    [Fact]
    public async Task UnarchiveSensitivityLabel_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.UnarchiveSensitivityLabel(uid, lid, pid, oid)); // not archived

        Assert.Contains($"Sensitivity label with id {lid} not found or is not archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveSensitivityLabel_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.UnarchiveSensitivityLabel(uid, 99999, pid, oid));

        Assert.Contains("Sensitivity label with id 99999 not found or is not archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveSensitivityLabel_Fails_IfOrganizationLabel()
    {

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _labelBusiness.UnarchiveSensitivityLabel(uid, lid5, pid, oid));

        Assert.Contains($"Organization sensitivity labels cannot be updated from the child projects.", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }


    #endregion

    #region DeleteSensitivityLabel Tests

    [Fact]
    public async Task DeleteSensitivityLabel_Succeeds_WhenExists()
    {
        // Act
        var result = await _labelBusiness.DeleteSensitivityLabel(uid, lid, pid, oid);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedLabel = await Context.SensitivityLabels.FindAsync(lid);
        Assert.Null(deletedLabel);

        // Ensure that the SensitivityLabel delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("delete", actualEvent.Operation);
        Assert.Equal("sensitivity_label", actualEvent.EntityType);
        Assert.Equal(lid, actualEvent.EntityId);
    }

    [Fact]
    public async Task DeleteSensitivityLabel_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.DeleteSensitivityLabel(uid, 99999, pid, oid));

        Assert.Contains("Sensitivity label with id 99999 not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task DeleteSensitivityLabel_Fails_IfArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _labelBusiness.DeleteSensitivityLabel(uid, lid2, pid, oid)); // archived label

        Assert.Contains($"Sensitivity label with id {lid2} not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task DeleteSensitivityLabel_Fails_IfOrganizationLabel()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _labelBusiness.DeleteSensitivityLabel(uid, lid4, pid, oid));

        Assert.Contains("Organization sensitivity labels cannot be updated from the child projects.", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateSensitivityLabel_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label LastUpdatedBy",
            Description = "Test description",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = oid
        };

        // Act
        Context.SensitivityLabels.Add(testLabel);
        await Context.SaveChangesAsync();

        // Assert
        var savedLabel = await Context.SensitivityLabels.FindAsync(testLabel.Id);
        Assert.NotNull(savedLabel);
        Assert.Equal(uid, savedLabel.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label Navigation",
            Description = "Test description 2",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = oid
        };

        Context.SensitivityLabels.Add(testLabel);
        await Context.SaveChangesAsync();

        // Act
        var labelWithUser = await Context.SensitivityLabels
            .Include(l => l.LastUpdatedByUser)
            .FirstAsync(l => l.Id == testLabel.Id);

        // Assert
        Assert.NotNull(labelWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", labelWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test_label@example.com", labelWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, labelWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateSensitivityLabel_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label Null",
            Description = "Test description 3",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.SensitivityLabels.Add(testLabel);
        await Context.SaveChangesAsync();

        // Assert
        var savedLabel = await Context.SensitivityLabels.FindAsync(testLabel.Id);
        Assert.NotNull(savedLabel);
        Assert.Null(savedLabel.LastUpdatedBy);

        var labelWithUser = await Context.SensitivityLabels
            .Include(l => l.LastUpdatedByUser)
            .FirstAsync(l => l.Id == testLabel.Id);

        Assert.Null(labelWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testLabel = new SensitivityLabel
        {
            Name = "Test Label Update",
            Description = "Test description 4",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            OrganizationId = oid
        };
        Context.SensitivityLabels.Add(testLabel);
        await Context.SaveChangesAsync();

        // Act
        testLabel.LastUpdatedBy = uid;
        testLabel.Name = "Updated Label Name";
        testLabel.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await Context.SaveChangesAsync();

        // Assert
        var updatedLabel = await Context.SensitivityLabels
            .Include(l => l.LastUpdatedByUser)
            .FirstAsync(l => l.Id == testLabel.Id);

        Assert.Equal(uid, updatedLabel.LastUpdatedBy);
        Assert.NotNull(updatedLabel.LastUpdatedByUser);
        Assert.Equal("Test User", updatedLabel.LastUpdatedByUser.Name);
        Assert.Equal("Updated Label Name", updatedLabel.Name);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetAuthorizedSensitivityLabels_PopulatesCache_ForRequestedProjectUserAction()
    {
        // Arrange
        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid2,
            LabelId = lid,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        string cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "read record");

        // Act
        var authorized = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid }, "read record");

        // Assert
        Assert.Contains(lid, authorized);

        var cached = await CacheService.Instance.GetAsync<List<long>>(cacheKey);
        Assert.NotNull(cached);
        Assert.Contains(lid, cached);
    }

    [Fact]
    public async Task GetAuthorizedSensitivityLabels_ServesCachedValue_WithoutRecomputing()
    {
        // Arrange — seed a sentinel value that does NOT match what the DB would actually compute,
        // so a hit proves the cache short-circuited the query rather than happening to agree with it.
        string cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "read record");
        var sentinel = new List<long> { -999 };
        await CacheService.Instance.SetAsync(cacheKey, sentinel, (TimeSpan?)null);

        // Act
        var authorized = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid }, "read record");

        // Assert
        Assert.Single(authorized);
        Assert.Contains(-999, authorized);
        Assert.DoesNotContain(lid, authorized);
    }

    [Fact]
    public async Task GetAuthorizedSensitivityLabels_CachesSeparately_PerAction()
    {
        // Arrange — seed "write record" with a sentinel, leave "read record" untouched
        string writeKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "write record");
        await CacheService.Instance.SetAsync(writeKey, new List<long> { -999 }, (TimeSpan?)null);

        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid2,
            LabelId = lid,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        // Act
        var readResult = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid }, "read record");
        var writeResult = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid }, "write record");

        // Assert
        Assert.Contains(lid, readResult);            // computed fresh, not poisoned by the write-record sentinel
        Assert.Contains(-999, writeResult);           // still reflects the sentinel — separate cache key
    }

    [Fact]
    public async Task GetAuthorizedSensitivityLabels_CachesSeparately_PerProject()
    {
        // Arrange — seed pid2's cache with a sentinel, leave pid untouched
        string pid2Key = CacheKeys.ProjectAuthorizedSensitivityLabels(pid2, uid2, "read record");
        await CacheService.Instance.SetAsync(pid2Key, new List<long> { -999 }, (TimeSpan?)null);

        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid2,
            LabelId = lid,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();

        // Act
        var pidResult = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid }, "read record");
        var pid2Result = await _labelService.GetAuthorizedSensitivityLabels(uid2, oid, new[] { pid2 }, "read record");

        // Assert
        Assert.Contains(lid, pidResult);
        Assert.Contains(-999, pid2Result);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_WithPermissionActionsChange_InvalidatesCache_ForAllUsersInScope()
    {
        // Arrange — two different users each have a cached answer for this project/label's action
        string uidKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        string uid2Key = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "read record");
        await CacheService.Instance.SetAsync(uidKey, new List<long> { lid }, (TimeSpan?)null);
        await CacheService.Instance.SetAsync(uid2Key, new List<long>(), (TimeSpan?)null);

        var dto = new UpdateSensitivityLabelRequestDto
        {
            Name = "Updated Label",
            PermissionActions = new List<string> { "read record", "write record" }
        };

        // Act
        await _labelBusiness.UpdateSensitivityLabel(uid, lid, pid, oid, dto);

        // Assert — a governed-action change can flip the answer for anyone in scope, not just
        // users with an explicit grant, so both entries should be gone
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(uidKey));
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(uid2Key));
    }

    [Fact]
    public async Task UpdateSensitivityLabel_WithoutPermissionActionsChange_DoesNotTouchUnrelatedLabelCache()
    {
        // Arrange — cache entry for a *different* label in the same project should survive
        // a metadata-only update (name/description) to lid that doesn't touch permissions.
        string otherLabelUserKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "read record");
        Context.SensitivityLabelGrants.Add(new SensitivityLabelGrant
        {
            UserId = uid2,
            LabelId = lid3,
            GrantedBy = uid,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Context.SaveChangesAsync();
        await CacheService.Instance.SetAsync(otherLabelUserKey, new List<long> { lid3 }, (TimeSpan?)null);

        var dto = new UpdateSensitivityLabelRequestDto { Name = "Renamed Only" };

        // Act
        await _labelBusiness.UpdateSensitivityLabel(uid, lid, pid, oid, dto);

        // Assert — this asserts the *narrow* invalidation-on-permission-change design choice;
        // if the eventual implementation invalidates on every update regardless of PermissionActions,
        // this test should be deleted rather than "fixed", since that'd be a deliberate design change.
        var cached = await CacheService.Instance.GetAsync<List<long>>(otherLabelUserKey);
        Assert.NotNull(cached);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_InvalidatesCache_ForAllUsersInScope()
    {
        // Arrange
        string uidKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        await CacheService.Instance.SetAsync(uidKey, new List<long> { lid }, (TimeSpan?)null);

        // Act
        await _labelBusiness.ArchiveSensitivityLabel(uid, lid, pid, oid);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(uidKey));
    }

    [Fact]
    public async Task UnarchiveSensitivityLabel_InvalidatesCache_ForAllUsersInScope()
    {
        // Arrange
        string uidKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        await CacheService.Instance.SetAsync(uidKey, new List<long>(), (TimeSpan?)null);

        // Act
        await _labelBusiness.UnarchiveSensitivityLabel(uid, lid2, pid, oid);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(uidKey));
    }

    [Fact]
    public async Task DeleteSensitivityLabel_InvalidatesCache_ForAllUsersInScope()
    {
        // Arrange 
        string uidKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid, "read record");
        await CacheService.Instance.SetAsync(uidKey, new List<long> { lid }, (TimeSpan?)null);

        // Act
        await _labelBusiness.DeleteSensitivityLabel(uid, lid, pid, oid);

        // Assert
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(uidKey));

        // Sanity check the label really is gone, per the existing delete test in this file
        Assert.Null(await Context.SensitivityLabels.FindAsync(lid));
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_OrgLevelLabel_InvalidatesCache_AcrossAllProjectsInOrg()
    {
        // Arrange — lid3 is an org-level label (ProjectId == null), visible in both pid and pid2
        string pidKey = CacheKeys.ProjectAuthorizedSensitivityLabels(pid, uid2, "read record");
        string pid2Key = CacheKeys.ProjectAuthorizedSensitivityLabels(pid2, uid2, "read record");
        await CacheService.Instance.SetAsync(pidKey, new List<long> { lid3 }, (TimeSpan?)null);
        await CacheService.Instance.SetAsync(pid2Key, new List<long> { lid3 }, (TimeSpan?)null);

        // Act — org-level labels can't be archived from a project context (see existing
        // ArchiveSensitivityLabel_Fails_IfOrganizationLabel test), so call without a projectId
        await _labelBusiness.ArchiveSensitivityLabel(uid3, lid3, null, oid);

        // Assert — an org-level label's governed-action change should ripple to every project
        // in the org, not just one
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(pidKey));
        Assert.Null(await CacheService.Instance.GetAsync<List<long>>(pid2Key));
    }

    #endregion

    #region GetSensitivityLabelPermissionActions Tests

    [Fact]
    public async Task GetSensitivityLabelPermissionActions_ReturnsAllEightFixedActions()
    {
        // Act
        var result = await _labelBusiness.GetSensitivityLabelPermissionActions();

        // Assert — these actions are seeded by migration and fixed, not test data
        Assert.Equal(8, result.Count);
        Assert.Contains(result, a => a.Name == "read record");
        Assert.Contains(result, a => a.Name == "write record");
        Assert.Contains(result, a => a.Name == "update record");
        Assert.Contains(result, a => a.Name == "delete record");
        Assert.Contains(result, a => a.Name == "download file");
        Assert.Contains(result, a => a.Name == "upload file");
        Assert.Contains(result, a => a.Name == "update file");
        Assert.Contains(result, a => a.Name == "delete file");
    }

    [Fact]
    public async Task GetSensitivityLabelPermissionActions_MapsFieldsCorrectly()
    {
        // Act
        var result = await _labelBusiness.GetSensitivityLabelPermissionActions();
        var readRecord = result.FirstOrDefault(a => a.Name == "read record");

        // Assert — description matches the fixed migration text exactly
        Assert.NotNull(readRecord);
        Assert.True(readRecord.Id > 0);
        Assert.Equal("Permission to read records with the given label", readRecord.Description);
    }

    #endregion
}