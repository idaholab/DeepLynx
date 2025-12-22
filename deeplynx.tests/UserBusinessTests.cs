using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class UserBusinessTests : IntegrationTestBase
{
    private UserBusiness _userBusiness;
    public long arcrid; // archived record
    public long cid; // class ID
    public long dsid1; // data source IDs
    public long dsid2;
    public long dsid3;
    public long dsid4;
    public long gid1; // group IDs
    public long gid2;
    public long guid1;
    public long guid2;
    public long oid; // organization IDs
    public long oid2;
    public long oid3;
    private long organizationId;
    public long ouid1;
    public long ouid2;
    public long pid; // project IDs
    public long pid2;
    public long pid3;
    public long pid4;
    public long rid1; // record IDs
    public long rid2;
    public long rid3;
    public long rid4;
    public long rid5;
    public long rid6;

    public long uid1; // user IDs
    public long uid2;
    public long uid3;
    public long uid4;
    public long uid5;

    // Additional test data IDs for GetUserAdminInfo scenarios
    public long adminUid; // admin user ID
    public long nonAdminUid; // non-admin user ID
    public long adminOrgId; // org where both users are admins
    public long nonAdminOrgId; // org where neither user is admin
    public long directAdminPid; // project where both users are direct admins
    public long groupAdminPid; // project where both users are admins via group
    public long nonAdminPid; // project where neither user is admin
    public long adminGroupId; // group that grants project admin access

    public UserBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userBusiness = new UserBusiness(Context);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // create test organization
        var org = new Organization
        {
            Name = "Org 1",
            Description = "Org 1 description"
        };
        var org2 = new Organization
        {
            Name = "Org 2",
            Description = "Org 2 description"
        };
        var org3 = new Organization
        {
            Name = "Org 3",
            Description = "Org 3 description"
        };
        Context.Organizations.AddRange(org, org2, org3);
        await Context.SaveChangesAsync();
        oid = org.Id;
        oid2 = org2.Id;
        oid3 = org3.Id;

        // delete org2
        Context.Organizations.Remove(org2);
        await Context.SaveChangesAsync();

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        // create test projects
        var project1 = new Project
        {
            Name = "Test Project 1",
            Description = "First test project",
            Abbreviation = "TST1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Second test project",
            Abbreviation = "TST2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var project3 = new Project
        {
            Name = "Test Project 3",
            Description = "User not a part of this",
            Abbreviation = "TST3",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var project4 = new Project
        {
            Name = "Test Project 4",
            Description = "User not a part of this",
            Abbreviation = "TST4",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Projects.AddRange(project1, project2, project3, project4);
        await Context.SaveChangesAsync();
        pid = project1.Id;
        pid2 = project2.Id;
        pid3 = project3.Id;
        pid4 = project4.Id;

        // delete project 4
        Context.Projects.Remove(project4);
        await Context.SaveChangesAsync();

        // create test users
        var user1 = new User
        {
            Name = "User 1",
            Email = "user1@test.com",
            Username = "user1",
            IsActive = true
        };
        var user2 = new User
        {
            Name = "User 2",
            Email = "user2@test.com",
            Username = "user2",
            IsArchived = true // archived user
        };
        var user3 = new User
        {
            Name = "User 3",
            Email = "user3@test.com",
            Username = "user3"
        };
        var user4 = new User
        {
            Name = "User 4",
            Email = "user4@test.com",
            Username = "user4",
            IsActive = false
        };
        var user5 = new User
        {
            Name = "User 5",
            Email = "user5@test.com",
            Username = "user5",
            IsActive = false
        };
        var organizationUser1 = new User
        {
            Name = "OrgUser 1",
            Email = "org_user1@test.com",
            Username = "ou1",
            IsActive = false
        };
        var organizationUser2 = new User
        {
            Name = "OrgUser 2",
            Email = "org_user2@test.com",
            Username = "ou2",
            IsActive = false
        };
        var groupUser1 = new User
        {
            Name = "GroupUser 1",
            Email = "group_user1@test.com",
            Username = "group_user1"
        };
        var groupUser2 = new User
        {
            Name = "GroupUser 2",
            Email = "group_user2@test.com",
            Username = "group_user2"
        };

        Context.Users.AddRange(
            user1, user2, user3, user4, user5, organizationUser1,
            organizationUser2, groupUser1, groupUser2);
        await Context.SaveChangesAsync();
        uid1 = user1.Id;
        uid2 = user2.Id;
        uid3 = user3.Id;
        uid4 = user4.Id;
        uid5 = user5.Id;
        ouid1 = organizationUser1.Id;
        ouid2 = organizationUser2.Id;
        guid1 = groupUser1.Id;
        guid2 = groupUser2.Id;

        // delete user 3
        Context.Users.Remove(user3);
        await Context.SaveChangesAsync();

        // create test group
        var group1 = new Group
        {
            Name = "Group 1",
            Description = "Group 1 description",
            Users = new List<User> { groupUser1 },
            OrganizationId = oid3
        };
        var group2 = new Group
        {
            Name = "Group 2",
            Description = "Group 2 description",
            OrganizationId = oid,
            Users = new List<User> { groupUser2 }
        };
        Context.Groups.AddRange(group1, group2);
        await Context.SaveChangesAsync();
        gid1 = group1.Id;
        gid2 = group2.Id;

        // add user1 as member of projects 1 and 2
        var projectMember1 = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid1,
        };
        var projectMember2 = new ProjectMember
        {
            ProjectId = pid2,
            UserId = uid1
        };
        // add orgUser2 as a member of project 1
        var projectMember3 = new ProjectMember
        {
            ProjectId = pid,
            UserId = ouid2
        };
        // add user 4 as a member of project 2
        var projectMember4 = new ProjectMember
        {
            ProjectId = pid2,
            UserId = uid4
        };
        // add group 1 to project 1
        var projectMember5 = new ProjectMember
        {
            ProjectId = pid,
            GroupId = gid1
        };
        // add user 2 (archived user) to project 2
        var projectMember6 = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid2
        };
        Context.ProjectMembers.AddRange(projectMember1, projectMember2,
            projectMember3, projectMember4, projectMember5, projectMember6);
        await Context.SaveChangesAsync();

        // add orgUser1 and orgUser2 as members of org
        var orgUser1 = new OrganizationUser
        {
            OrganizationId = oid,
            UserId = ouid1
        };
        var orgUser2 = new OrganizationUser
        {
            OrganizationId = oid,
            UserId = ouid2
        };
        Context.OrganizationUsers.AddRange(orgUser1, orgUser2);
        await Context.SaveChangesAsync();

        // create data sources
        var dataSource1 = new DataSource
        {
            Name = "DataSource 1",
            Description = "First data source",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        var dataSource2 = new DataSource
        {
            Name = "DataSource 2",
            Description = "Second data source",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        var dataSource3 = new DataSource
        {
            Name = "DataSource 3",
            Description = "Third data source",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        var dataSource4 = new DataSource
        {
            Name = "DataSource 4",
            Description = "Fourth data source",
            ProjectId = pid3,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.DataSources.AddRange(dataSource1, dataSource2, dataSource3, dataSource4);
        await Context.SaveChangesAsync();
        dsid1 = dataSource1.Id;
        dsid2 = dataSource2.Id;
        dsid3 = dataSource3.Id;
        dsid4 = dataSource4.Id;

        // create test class
        var testClass = new Class
        {
            Name = "Test Class",
            Description = "Test class for records",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        // create test tag
        var testTag = new Tag
        {
            Name = "Test Tag",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        // create records for project 1
        var record1 = new Record
        {
            Name = "Test Record 1",
            Description = "Original description",
            OriginalId = "orig_1",
            Properties = "{\"test\": \"value1\"}",
            ProjectId = pid,
            DataSourceId = dsid1,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-2),
            Uri = "localhost:8090/record1",
            OrganizationId = oid
        };
        var record2 = new Record
        {
            Name = "Test Record 2",
            Description = "Second record",
            OriginalId = "orig_2",
            Properties = "{\"test\": \"value2\"}",
            ProjectId = pid,
            DataSourceId = dsid2,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-1),
            Uri = "localhost:8090/record2",
            OrganizationId = oid
        };
        var archivedRecord = new Record
        {
            Name = "Archived Record",
            Description = "This is archived",
            OriginalId = "archived_1",
            Properties = "{\"test\": \"archived\"}",
            ProjectId = pid,
            DataSourceId = dsid1,
            ClassId = cid,
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-5),
            Uri = "localhost:8090/archived",
            OrganizationId = oid
        };
        // create records for project 2
        var record3 = new Record
        {
            Name = "Test Record 3",
            Description = "Third record",
            OriginalId = "orig_3",
            Properties = "{\"test\": \"value3\"}",
            ProjectId = pid2,
            DataSourceId = dsid3,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Uri = "localhost:8090/record3",
            OrganizationId = oid
        };
        var record4 = new Record
        {
            Name = "Test Record 4",
            Description = "Fourth record",
            OriginalId = "orig_4",
            Properties = "{\"test\": \"value4\"}",
            ProjectId = pid2,
            DataSourceId = dsid3,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Uri = "localhost:8090/record4",
            OrganizationId = oid
        };
        var record5 = new Record
        {
            Name = "Test Record 5",
            Description = "Test record for unit tests",
            OriginalId = "orig_5",
            Properties = "{\"test\": \"test value\"}",
            ProjectId = pid2,
            DataSourceId = dsid3,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090/record6",
            OrganizationId = oid
        };
        // create records for project 3
        var record6 = new Record
        {
            Name = "Test Record 6",
            Description = "Unseen record",
            OriginalId = "orig_6",
            Properties = "{\"test\": \"value6\"}",
            ProjectId = pid3,
            DataSourceId = dsid4,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Uri = "localhost:8090/record6", OrganizationId = oid
        };

        Context.Records.AddRange(record1, record2, record3, record4, record5, record6, archivedRecord);
        await Context.SaveChangesAsync();
        rid1 = record1.Id;
        rid2 = record2.Id;
        rid3 = record3.Id;
        rid4 = record4.Id;
        rid5 = record5.Id;
        rid6 = record6.Id;
        arcrid = archivedRecord.Id;

        // create historical records for tracking changes
        var historicalRecord1_v1 = new HistoricalRecord
        {
            RecordId = rid1,
            Name = "Test Record 1",
            Description = "Original description",
            OriginalId = "orig_1",
            Properties = "{\"test\": \"value1\"}",
            ProjectId = pid,
            ProjectName = "Test Project 1",
            DataSourceId = dsid1,
            DataSourceName = "DataSource 1",
            ClassId = cid,
            ClassName = "Test Class",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-2),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/record1",
            OrganizationId = oid
        };
        var historicalRecord1_v2 = new HistoricalRecord
        {
            RecordId = rid1,
            Name = "Test Record 1",
            Description = "Updated description",
            OriginalId = "orig_1",
            Properties = "{\"test\": \"value1_updated\"}",
            ProjectId = pid,
            ProjectName = "Test Project 1",
            DataSourceId = dsid1,
            DataSourceName = "DataSource 1",
            ClassId = cid,
            ClassName = "Test Class",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddHours(-1),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/record1",
            OrganizationId = oid
        };
        var historicalRecord2 = new HistoricalRecord
        {
            RecordId = rid2,
            Name = "Test Record 2",
            Description = "Second record",
            OriginalId = "orig_2",
            Properties = "{\"test\": \"value2\"}",
            ProjectId = pid,
            ProjectName = "Test Project 1",
            DataSourceId = dsid2,
            DataSourceName = "DataSource 2",
            ClassId = cid,
            ClassName = "Test Class",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-1),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/record2",
            OrganizationId = oid
        };
        var historicalRecord3 = new HistoricalRecord
        {
            RecordId = rid3,
            Name = "Test Record 3",
            Description = "Third record",
            OriginalId = "orig_3",
            Properties = "{\"test\": \"value3\"}",
            ProjectId = pid2,
            ProjectName = "Test Project 2",
            DataSourceId = dsid3,
            DataSourceName = "DataSource 3",
            ClassId = cid,
            ClassName = "Test Class",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/record3",
            OrganizationId = oid
        };
        var historicalRecord4 = new HistoricalRecord
        {
            RecordId = rid4,
            Name = "Test Record 4",
            Description = "Fourth record",
            OriginalId = "orig_4",
            Properties = "{\"test\": \"value4\"}",
            ProjectId = pid2,
            ProjectName = "Test Project 2",
            DataSourceId = dsid3,
            DataSourceName = "DataSource 3",
            ClassId = cid,
            ClassName = "Test Class",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/record4",
            OrganizationId = oid
        };
        var historicalArchivedRecord = new HistoricalRecord
        {
            RecordId = arcrid,
            Name = "Archived Record",
            Description = "This is archived",
            OriginalId = "archived_1",
            Properties = "{\"test\": \"archived\"}",
            ProjectId = pid,
            ProjectName = "Test Project 1",
            DataSourceId = dsid1,
            DataSourceName = "DataSource 1",
            ClassId = cid,
            ClassName = "Test Class",
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddDays(-5),
            LastUpdatedBy = "user1@test.com",
            Uri = "localhost:8090/archived",
            OrganizationId = oid
        };

        Context.HistoricalRecords.AddRange(
            historicalRecord1_v1,
            historicalRecord1_v2,
            historicalRecord2,
            historicalRecord3,
            historicalRecord4,
            historicalArchivedRecord
        );
        await Context.SaveChangesAsync();

        // Create admin users for GetUserAdminInfo tests
        var sysAdminUser = new User
        {
            Name = "SysAdmin User",
            Email = "sysadmin@test.com",
            Username = "sysadmin",
            IsActive = true,
            IsSysAdmin = true
        };
        var regularUser = new User
        {
            Name = "Regular User",
            Email = "regular@test.com",
            Username = "regular",
            IsActive = true,
            IsSysAdmin = false
        };
        Context.Users.AddRange(sysAdminUser, regularUser);
        await Context.SaveChangesAsync();
        adminUid = sysAdminUser.Id;
        nonAdminUid = regularUser.Id;

        // Create organizations for admin testing
        var adminOrg = new Organization
        {
            Name = "Admin Org",
            Description = "Org where users are admins"
        };
        var regularOrg = new Organization
        {
            Name = "Regular Org",
            Description = "Org where users are not admins"
        };
        Context.Organizations.AddRange(adminOrg, regularOrg);
        await Context.SaveChangesAsync();
        adminOrgId = adminOrg.Id;
        nonAdminOrgId = regularOrg.Id;

        // Make both users org admins in adminOrg
        var orgUser3 = new OrganizationUser
        {
            OrganizationId = adminOrgId,
            UserId = adminUid,
            IsOrgAdmin = true
        };
        var orgUser4 = new OrganizationUser
        {
            OrganizationId = adminOrgId,
            UserId = nonAdminUid,
            IsOrgAdmin = true
        };
        // Make users non-admin members of regularOrg
        var orgUser5 = new OrganizationUser
        {
            OrganizationId = nonAdminOrgId,
            UserId = adminUid,
            IsOrgAdmin = false
        };
        var orgUser6 = new OrganizationUser
        {
            OrganizationId = nonAdminOrgId,
            UserId = nonAdminUid,
            IsOrgAdmin = false
        };
        Context.OrganizationUsers.AddRange(orgUser3, orgUser4, orgUser5, orgUser6);
        await Context.SaveChangesAsync();

        // Create projects for admin testing
        var directAdminProject = new Project
        {
            Name = "Direct Admin Project",
            Description = "Project where users are direct admins",
            Abbreviation = "DAP",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = adminOrgId
        };
        var groupAdminProject = new Project
        {
            Name = "Group Admin Project",
            Description = "Project where users are admins via group",
            Abbreviation = "GAP",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = adminOrgId
        };
        var regularProject = new Project
        {
            Name = "Regular Project",
            Description = "Project where users are not admins",
            Abbreviation = "REG",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = adminOrgId
        };
        Context.Projects.AddRange(directAdminProject, groupAdminProject, regularProject);
        await Context.SaveChangesAsync();
        directAdminPid = directAdminProject.Id;
        groupAdminPid = groupAdminProject.Id;
        nonAdminPid = regularProject.Id;

        // Create Admin roles for each project where admins are relevant
        var dapAdminRole = new Role
        {
            Name = "Admin",
            OrganizationId = adminOrgId,
            ProjectId = directAdminPid
        };
        var gapAdminRole = new Role
        {
            Name = "Admin",
            OrganizationId = adminOrgId,
            ProjectId = groupAdminPid
        };
        Context.Roles.AddRange(dapAdminRole, gapAdminRole);
        await Context.SaveChangesAsync();

        // Create group for group-based admin access
        var adminGroup = new Group
        {
            Name = "Admin Group",
            Description = "Group that grants admin access",
            OrganizationId = adminOrgId,
            Users = new List<User> { sysAdminUser, regularUser }
        };
        Context.Groups.Add(adminGroup);
        await Context.SaveChangesAsync();
        adminGroupId = adminGroup.Id;

        // Make both users direct project admins in directAdminProject
        var directMember1 = new ProjectMember
        {
            ProjectId = directAdminPid,
            UserId = adminUid,
            RoleId = dapAdminRole.Id
        };
        var directMember2 = new ProjectMember
        {
            ProjectId = directAdminPid,
            UserId = nonAdminUid,
            RoleId = dapAdminRole.Id
        };
        // Make group a project admin in groupAdminProject
        var groupMember = new ProjectMember
        {
            ProjectId = groupAdminPid,
            GroupId = adminGroupId,
            RoleId = gapAdminRole.Id
        };
        // Make users non-admin members of regularProject
        var regularMember1 = new ProjectMember
        {
            ProjectId = nonAdminPid,
            UserId = adminUid
        };
        var regularMember2 = new ProjectMember
        {
            ProjectId = nonAdminPid,
            UserId = nonAdminUid
        };
        Context.ProjectMembers.AddRange(
            directMember1, directMember2, groupMember, 
            regularMember1, regularMember2);
        await Context.SaveChangesAsync();
    }

    #region CreateUser Tests

    [Fact]
    public async Task CreateUser_Succeeds_WithValidData()
    {
        // Arrange
        var dto = new CreateUserRequestDto
        {
            Name = "New User",
            Email = "newuser@test.com",
            Username = "newuser",
            IsActive = true
        };

        // Act
        var result = await _userBusiness.CreateUser(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New User", result.Name);
        Assert.Equal("newuser@test.com", result.Email);
        Assert.Equal("newuser", result.Username);
        Assert.True(result.IsActive);
        Assert.False(result.IsArchived);

        // Verify it was actually saved to DB
        var savedUser = await Context.Users.FindAsync(result.Id);
        Assert.NotNull(savedUser);
        Assert.Equal("New User", savedUser.Name);
        Assert.Equal("newuser@test.com", savedUser.Email);
    }

    [Fact]
    public async Task CreateUser_Succeeds_WithMinimalData()
    {
        // Arrange
        var dto = new CreateUserRequestDto
        {
            Name = "Minimal User",
            Email = "minimal@test.com"
        };

        // Act
        var result = await _userBusiness.CreateUser(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Minimal User", result.Name);
        Assert.Equal("minimal@test.com", result.Email);
        Assert.False(result.IsActive);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task CreateUser_Fails_IfEmailAlreadyExists()
    {
        // Arrange
        var dto = new CreateUserRequestDto
        {
            Name = "Duplicate Email User",
            Email = "user1@test.com" // User 1 already has this email
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _userBusiness.CreateUser(dto));

        Assert.Contains("User with email already exists", exception.Message);
    }

    #endregion

    #region GetAllUsers Tests

    [Fact]
    public async Task GetAllUsers_NoFilters_ReturnsAllNonArchivedUsers()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(null, null);
        var users = result.ToList();

        // Assert
        Assert.Equal(9, users.Count);
        Assert.All(users, u => Assert.False(u.IsArchived));
        Assert.Contains(users, u => u.Id == uid5);
        Assert.DoesNotContain(users, u => u.Id == uid2); // archived
        Assert.DoesNotContain(users, u => u.Id == uid3); // archived
    }

    [Fact]
    public async Task GetAllUsers_FilterByProjectId_ReturnsOnlyProjectMembers()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(pid, null);
        var users = result.ToList();

        // Assert
        Assert.NotEmpty(users);
        Assert.Equal(3, users.Count);
        Assert.All(users, u => Assert.False(u.IsArchived));
        Assert.Contains(users, u => u.Id == uid1);
        Assert.Contains(users, u => u.Id == ouid2);
        Assert.Contains(users, u => u.Id == guid1); // group is project member
    }

    [Fact]
    public async Task GetAllUsers_FilterByOrganizationId_ReturnsOnlyOrgMembers()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(null, oid);
        var users = result.ToList();

        // Assert
        Assert.NotEmpty(users);
        Assert.Equal(3, users.Count);
        Assert.All(users, u => Assert.False(u.IsArchived));
        Assert.Contains(users, u => u.Id == ouid1);
        Assert.Contains(users, u => u.Id == ouid2);
        Assert.Contains(users, u => u.Id == guid2); // group is in organization
    }

    [Fact]
    public async Task GetAllUsers_FilterByBothProjectAndOrg_ReturnsUsersInBoth()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(pid, oid);
        var users = result.ToList();

        // Assert
        Assert.NotEmpty(users);
        Assert.Single(users);
        Assert.Contains(users, u => u.Id == ouid2); // ou2 is in both org and proj;
        Assert.DoesNotContain(users, u => u.Id == ouid1); // ou1 is not in proj
        Assert.DoesNotContain(users, u => u.Id == uid4); // u4 is not in org
    }

    [Fact]
    public async Task GetAllUsers_FilterByNonExistentProject_ReturnsEmpty()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(pid4, null);
        var users = result.ToList();

        // Assert
        Assert.Empty(users);
    }

    [Fact]
    public async Task GetAllUsers_FilterByNonExistentOrg_ReturnsEmpty()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(null, oid2);
        var users = result.ToList();

        // Assert
        Assert.Empty(users);
    }

    [Fact]
    public async Task GetAllUsers_FilterByProject_ExcludesArchivedMembers()
    {
        // Act
        var result = await _userBusiness.GetAllUsers(pid2, null);
        var users = result.ToList();

        // Assert
        Assert.All(users, u => Assert.False(u.IsArchived));
        Assert.DoesNotContain(users, u => u.Id == uid2); // archived user excluded
    }

    #endregion

    #region GetUser Tests

    [Fact]
    public async Task GetUser_Succeeds_WhenExists()
    {
        // Act
        var result = await _userBusiness.GetUser(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uid1, result.Id);
        Assert.Equal("User 1", result.Name);
        Assert.Equal("user1@test.com", result.Email);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetUser_Fails_IfArchived()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.GetUser(uid2));

        // Assert
        Assert.Contains($"User with id {uid2} not found", exception.Message);
    }

    [Fact]
    public async Task GetUser_Fails_IfDeleted()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.GetUser(uid3));

        // Assert
        Assert.Contains($"User with id {uid3} not found", exception.Message);
    }

    #endregion

    #region GetUserAdminInfo Tests

    [Fact]
    public async Task GetUserAdminInfo_Succeeds_WhenExists()
    {
        // Act
        var result = await _userBusiness.GetUserAdminInfo(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uid1, result.Id);
        Assert.Equal("User 1", result.Name);
        Assert.Equal("user1@test.com", result.Email);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetUserAdminInfo_Fails_IfArchived()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.GetUserAdminInfo(uid2));

        // Assert
        Assert.Contains($"User with id {uid2} not found", exception.Message);
    }

    [Fact]
    public async Task GetUserAdminInfo_Fails_IfDeleted()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.GetUserAdminInfo(uid3));

        // Assert
        Assert.Contains($"User with id {uid3} not found", exception.Message);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithUserIdOnly_ReturnsSysAdminOnly()
    {
        // Act - SysAdmin user
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.Null(adminResult.IsOrgAdmin);
        Assert.Null(adminResult.IsProjectAdmin);

        // Act - Non-admin user
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.Null(regularResult.IsOrgAdmin);
        Assert.Null(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithOrgId_ReturnsIsOrgAdmin_True()
    {
        // Act - SysAdmin user in admin org
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid, adminOrgId);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.True(adminResult.IsOrgAdmin);
        Assert.Null(adminResult.IsProjectAdmin);

        // Act - Regular user in admin org
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid, adminOrgId);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.True(regularResult.IsOrgAdmin);
        Assert.Null(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithOrgId_ReturnsIsOrgAdmin_False()
    {
        // Act - SysAdmin user in non-admin org
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid, nonAdminOrgId);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.False(adminResult.IsOrgAdmin);
        Assert.Null(adminResult.IsProjectAdmin);

        // Act - Regular user in non-admin org
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid, nonAdminOrgId);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.False(regularResult.IsOrgAdmin);
        Assert.Null(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithProjectId_ReturnsIsProjectAdmin_TrueForDirectMembership()
    {
        // Act - SysAdmin user with direct admin membership
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid, projectId: directAdminPid);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.Null(adminResult.IsOrgAdmin);
        Assert.True(adminResult.IsProjectAdmin);

        // Act - Regular user with direct admin membership
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid, projectId: directAdminPid);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.Null(regularResult.IsOrgAdmin);
        Assert.True(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithProjectId_ReturnsIsProjectAdmin_TrueForGroupMembership()
    {
        // Act - SysAdmin user with group-based admin membership
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid, projectId: groupAdminPid);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.Null(adminResult.IsOrgAdmin);
        Assert.True(adminResult.IsProjectAdmin);

        // Act - Regular user with group-based admin membership
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid, projectId: groupAdminPid);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.Null(regularResult.IsOrgAdmin);
        Assert.True(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithProjectId_ReturnsIsProjectAdmin_False()
    {
        // Act - SysAdmin user in non-admin project
        var adminResult = await _userBusiness.GetUserAdminInfo(adminUid, projectId: nonAdminPid);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.Null(adminResult.IsOrgAdmin);
        Assert.False(adminResult.IsProjectAdmin);

        // Act - Regular user in non-admin project
        var regularResult = await _userBusiness.GetUserAdminInfo(nonAdminUid, projectId: nonAdminPid);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.Null(regularResult.IsOrgAdmin);
        Assert.False(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithAllIds_ReturnsAllBooleans()
    {
        // Act - SysAdmin user with all admin privileges
        var adminResult = await _userBusiness.GetUserAdminInfo(
            adminUid, 
            adminOrgId, 
            directAdminPid);

        // Assert
        Assert.NotNull(adminResult);
        Assert.Equal(adminUid, adminResult.Id);
        Assert.True(adminResult.IsSysAdmin);
        Assert.True(adminResult.IsOrgAdmin);
        Assert.True(adminResult.IsProjectAdmin);

        // Act - Regular user with org and project admin privileges
        var regularResult = await _userBusiness.GetUserAdminInfo(
            nonAdminUid, 
            adminOrgId, 
            directAdminPid);

        // Assert
        Assert.NotNull(regularResult);
        Assert.Equal(nonAdminUid, regularResult.Id);
        Assert.False(regularResult.IsSysAdmin);
        Assert.True(regularResult.IsOrgAdmin);
        Assert.True(regularResult.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithAllIds_ReturnsAllBooleans_MixedPermissions()
    {
        // Act - SysAdmin user with mixed permissions (sys admin, org admin, not project admin)
        var result1 = await _userBusiness.GetUserAdminInfo(
            adminUid, 
            adminOrgId, 
            nonAdminPid);

        // Assert
        Assert.NotNull(result1);
        Assert.True(result1.IsSysAdmin);
        Assert.True(result1.IsOrgAdmin);
        Assert.False(result1.IsProjectAdmin);

        // Act - Regular user with mixed permissions (not sys admin, not org admin, is project admin via group)
        var result2 = await _userBusiness.GetUserAdminInfo(
            nonAdminUid, 
            nonAdminOrgId, 
            groupAdminPid);

        // Assert
        Assert.NotNull(result2);
        Assert.False(result2.IsSysAdmin);
        Assert.False(result2.IsOrgAdmin);
        Assert.True(result2.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithNonExistentOrgId_ReturnsIsOrgAdminFalse()
    {
        // Act
        var result = await _userBusiness.GetUserAdminInfo(adminUid, 99999);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(adminUid, result.Id);
        Assert.True(result.IsSysAdmin);
        Assert.False(result.IsOrgAdmin);
        Assert.Null(result.IsProjectAdmin);
    }

    [Fact]
    public async Task GetUserAdminInfo_WithNonExistentProjectId_ReturnsIsProjectAdminFalse()
    {
        // Act
        var result = await _userBusiness.GetUserAdminInfo(adminUid, projectId: 99999);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(adminUid, result.Id);
        Assert.True(result.IsSysAdmin);
        Assert.Null(result.IsOrgAdmin);
        Assert.False(result.IsProjectAdmin);
    }

    #endregion

    # region GetLocalDevUser Tests

    [Fact]
    public async Task GetLocalDevUser_Succeeds_IfExists()
    {
        // Arrange - Create local dev user and set environment variable
        var localDevUser = new User
        {
            Name = "Local Developer",
            Email = "developer@localhost",
            Username = "localdev",
            IsActive = true,
            IsSysAdmin = true
        };
        Context.Users.Add(localDevUser);
        await Context.SaveChangesAsync();

        Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");

        try
        {
            // Act
            var result = await _userBusiness.GetLocalDevUser();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("developer@localhost", result.Email);
            Assert.Equal("Local Developer", result.Name);
            Assert.Equal("localdev", result.Username);
            Assert.True(result.IsSysAdmin);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }
    }

    [Fact]
    public async Task GetLocalDevUser_Fails_IfEnvNullOrFalse()
    {
        // Arrange - Ensure environment variable is not set
        Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);

        // Act & Assert
        var nullException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userBusiness.GetLocalDevUser());

        Assert.Contains("Local Dev User cannot be used unless backend authentication is disabled",
            nullException.Message);

        // Arrange - Ensure environment variable is set to false
        Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");

        // Act & Assert
        var falseException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userBusiness.GetLocalDevUser());

        Assert.Contains("Local Dev User cannot be used unless backend authentication is disabled",
            falseException.Message);
    }

    [Fact]
    public async Task GetLocalDevUser_Fails_IfNotExists()
    {
        // Arrange - Set environment variable but don't create the user
        Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _userBusiness.GetLocalDevUser());

            Assert.Contains("Local Dev User not found", exception.Message);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }
    }

    # endregion

    #region UpdateUser Tests

    [Fact]
    public async Task UpdateUser_Success_ReturnsUser()
    {
        // Arrange
        var dto = new UpdateUserRequestDto
        {
            Name = "Updated User Name",
            Username = "updatedusername",
            IsActive = true
        };

        // Act
        var result = await _userBusiness.UpdateUser(uid1, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uid1, result.Id);
        Assert.Equal("Updated User Name", result.Name);
        Assert.Equal("updatedusername", result.Username);
        Assert.True(result.IsActive);

        // Verify it was actually saved to DB
        var savedUser = await Context.Users.FindAsync(uid1);
        Assert.NotNull(savedUser);
        Assert.Equal("Updated User Name", savedUser.Name);
        Assert.Equal("updatedusername", savedUser.Username);
    }

    [Fact]
    public async Task UpdateUser_Success_WithPartialUpdate()
    {
        // Arrange - only update name
        var dto = new UpdateUserRequestDto
        {
            Name = "Only Name Updated"
        };

        // Get original user
        var originalUser = await Context.Users.FindAsync(uid1);
        var originalUsername = originalUser.Username;

        // Act
        var result = await _userBusiness.UpdateUser(uid1, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uid1, result.Id);
        Assert.Equal("Only Name Updated", result.Name);
        Assert.Equal(originalUsername, result.Username); // Should be unchanged
    }

    [Fact]
    public async Task UpdateUser_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateUserRequestDto
        {
            Name = "Updated User"
        };

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.UpdateUser(uid3, dto));

        // Assert
        Assert.Contains("User not found", exception.Message);
    }

    [Fact]
    public async Task UpdateUser_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateUserRequestDto
        {
            Name = "Updated User"
        };

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.UpdateUser(uid2, dto));

        // Assert
        Assert.Contains("User not found", exception.Message);
    }

    #endregion

    #region DeleteUser Tests

    [Fact]
    public async Task DeleteUser_Succeeds_WhenExists()
    {
        // Act
        var result = await _userBusiness.DeleteUser(uid1);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedUser = await Context.Users.FindAsync(uid1);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task DeleteUser_Fails_IfNotFound()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.DeleteUser(uid3));

        // Assert
        Assert.Contains($"User with id {uid3} not found", exception.Message);
    }

    #endregion

    #region ArchiveUser Tests

    [Fact]
    public async Task ArchiveUser_Succeeds_IfNotArchived()
    {
        // Act
        var result = await _userBusiness.ArchiveUser(uid1);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedUser = await Context.Users.FindAsync(uid1);
        Assert.NotNull(savedUser);
        Assert.True(savedUser.IsArchived);
    }

    [Fact]
    public async Task ArchiveUser_Fails_IfArchived()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.ArchiveUser(uid2));

        // Assert
        Assert.Contains("User not found", exception.Message);
    }

    [Fact]
    public async Task ArchiveUser_Fails_IfNotFound()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.ArchiveUser(uid3));

        // Assert
        Assert.Contains("User not found", exception.Message);
    }

    #endregion

    #region UnarchiveUser Tests

    [Fact]
    public async Task UnarchiveUser_Succeeds_IfArchived()
    {
        // Act
        var result = await _userBusiness.UnarchiveUser(uid2);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedUser = await Context.Users.FindAsync(uid2);
        Assert.NotNull(savedUser);
        Assert.False(savedUser.IsArchived);
    }

    [Fact]
    public async Task UnarchiveUser_Fails_IfNotArchived()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.UnarchiveUser(uid1));

        // Assert
        Assert.Contains("Archived user not found", exception.Message);
    }

    [Fact]
    public async Task UnarchiveUser_Fails_IfNotFound()
    {
        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.UnarchiveUser(uid3));

        // Assert
        Assert.Contains("Archived user not found", exception.Message);
    }

    #endregion

    #region GetUserOverview Tests

    [Fact]
    public async Task GetUserOverview_ReturnsCorrectCounts()
    {
        // Act
        var result = await _userBusiness.GetUserOverview(uid1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Projects); // uid1 is member of pid and pid2
        Assert.Equal(3, result.Connections); // 3 data sources across user's projects
        Assert.Equal(5, result.Records); // 4 records across user's projects
        Assert.Equal(1, result.Tags); // 2 tags across user's projects
    }

    [Fact]
    public async Task GetUserOverview_ReturnsZeroCounts_ForUserWithNoProjects()
    {
        // Act
        var result = await _userBusiness.GetUserOverview(uid5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Projects);
        Assert.Equal(0, result.Connections);
        Assert.Equal(0, result.Records);
        Assert.Equal(0, result.Tags);
    }
    
    [Fact]
    public async Task GetUserOverview_Fails_WhenUserDoesNotExist()
    {
        // Act
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _userBusiness.GetUserOverview(uid5 + 999999));
    }

    #endregion

    #region SetSysAdmin Tests

    [Fact]
    public async Task SetSysAdmin_Succeeds_WhenAuthorizerIsSysAdmin()
    {
        // Arrange - Create a sysadmin authorizer and a regular candidate user
        var authorizer = new User
        {
            Name = "Admin Alan",
            Email = "adminalan@test.com",
            Username = "admin",
            IsActive = true,
            IsSysAdmin = true
        };
        var candidate = new User
        {
            Name = "Regular Joe",
            Email = "regularjoe@test.com",
            Username = "regular",
            IsActive = true,
            IsSysAdmin = false
        };
        Context.Users.AddRange(authorizer, candidate);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userBusiness.SetSysAdmin(authorizer.Id, candidate.Id);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var updatedCandidate = await Context.Users.FindAsync(candidate.Id);
        Assert.NotNull(updatedCandidate);
        Assert.True(updatedCandidate.IsSysAdmin);
    }

    [Fact]
    public async Task SetSysAdmin_Fails_WhenAuthorizerIsNotSysAdmin()
    {
        // Arrange - Create a non-admin authorizer and a candidate user
        var authorizer = new User
        {
            Name = "Regular Joe",
            Email = "regularjoe@test.com",
            Username = "regular",
            IsActive = true,
            IsSysAdmin = false
        };
        var candidate = new User
        {
            Name = "Candidate User",
            Email = "candidate@test.com",
            Username = "candidate",
            IsActive = true,
            IsSysAdmin = false
        };
        Context.Users.AddRange(authorizer, candidate);
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.SetSysAdmin(authorizer.Id, candidate.Id));

        Assert.Contains($"User with ID {authorizer.Id} not found or cannot grant admin privileges", exception.Message);

        // Verify candidate was NOT made admin
        var updatedCandidate = await Context.Users.FindAsync(candidate.Id);
        Assert.NotNull(updatedCandidate);
        Assert.False(updatedCandidate.IsSysAdmin);
    }

    [Fact]
    public async Task SetSysAdmin_Fails_WhenAuthorizerIsArchived()
    {
        // Arrange - Create an archived sysadmin authorizer and a candidate user
        var authorizer = new User
        {
            Name = "Archived Admin",
            Email = "archived_admin@test.com",
            Username = "archived_admin",
            IsActive = true,
            IsSysAdmin = true,
            IsArchived = true
        };
        var candidate = new User
        {
            Name = "Candidate User",
            Email = "candidate@test.com",
            Username = "candidate",
            IsActive = true,
            IsSysAdmin = false
        };
        Context.Users.AddRange(authorizer, candidate);
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.SetSysAdmin(authorizer.Id, candidate.Id));

        Assert.Contains($"User with ID {authorizer.Id} not found or cannot grant admin privileges", exception.Message);
    }

    [Fact]
    public async Task SetSysAdmin_Fails_WhenAuthorizerNotFound()
    {
        // Arrange - Create only a candidate user
        var candidate = new User
        {
            Name = "Candidate User",
            Email = "candidate@test.com",
            Username = "candidate",
            IsActive = true,
            IsSysAdmin = false
        };
        Context.Users.Add(candidate);
        await Context.SaveChangesAsync();

        long nonExistentAuthorizerId = 99999;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.SetSysAdmin(nonExistentAuthorizerId, candidate.Id));

        Assert.Contains($"User with ID {nonExistentAuthorizerId} not found or cannot grant admin privileges",
            exception.Message);
    }

    [Fact]
    public async Task SetSysAdmin_Fails_WhenCandidateNotFound()
    {
        // Arrange - Create only an authorizer
        var authorizer = new User
        {
            Name = "Admin User",
            Email = "admin@test.com",
            Username = "admin",
            IsActive = true,
            IsSysAdmin = true
        };
        Context.Users.Add(authorizer);
        await Context.SaveChangesAsync();

        long nonExistentCandidateId = 99999;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.SetSysAdmin(authorizer.Id, nonExistentCandidateId));

        Assert.Contains($"User with ID {nonExistentCandidateId} not found", exception.Message);
    }

    [Fact]
    public async Task SetSysAdmin_Fails_WhenCandidateIsArchived()
    {
        // Arrange - Create a sysadmin authorizer and an archived candidate
        var authorizer = new User
        {
            Name = "Admin User",
            Email = "admin@test.com",
            Username = "admin",
            IsActive = true,
            IsSysAdmin = true
        };
        var candidate = new User
        {
            Name = "Archived User",
            Email = "archived@test.com",
            Username = "archived",
            IsActive = true,
            IsSysAdmin = false,
            IsArchived = true
        };
        Context.Users.AddRange(authorizer, candidate);
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _userBusiness.SetSysAdmin(authorizer.Id, candidate.Id));

        Assert.Contains($"User with ID {candidate.Id} not found", exception.Message);
    }

    [Fact]
    public async Task SetSysAdmin_Succeeds_WhenCandidateAlreadySysAdmin()
    {
        // Arrange - Create a sysadmin authorizer and a candidate who is already sysadmin
        var authorizer = new User
        {
            Name = "Admin User",
            Email = "admin@test.com",
            Username = "admin",
            IsActive = true,
            IsSysAdmin = true
        };
        var candidate = new User
        {
            Name = "Already Admin",
            Email = "already_admin@test.com",
            Username = "already_admin",
            IsActive = true,
            IsSysAdmin = true
        };
        Context.Users.AddRange(authorizer, candidate);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userBusiness.SetSysAdmin(authorizer.Id, candidate.Id);

        // Assert
        Assert.True(result);

        // Verify candidate is still admin
        var updatedCandidate = await Context.Users.FindAsync(candidate.Id);
        Assert.NotNull(updatedCandidate);
        Assert.True(updatedCandidate.IsSysAdmin);
    }

    [Fact]
    public async Task SetSysAdmin_Succeeds_WhenAuthorizerGrantsToSelf()
    {
        // Arrange - Create a sysadmin who will grant to themselves (edge case)
        var authorizer = new User
        {
            Name = "Admin User",
            Email = "admin@test.com",
            Username = "admin",
            IsActive = true,
            IsSysAdmin = true
        };
        Context.Users.Add(authorizer);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userBusiness.SetSysAdmin(authorizer.Id, authorizer.Id);

        // Assert
        Assert.True(result);

        // Verify authorizer is still admin
        var updatedAuthorizer = await Context.Users.FindAsync(authorizer.Id);
        Assert.NotNull(updatedAuthorizer);
        Assert.True(updatedAuthorizer.IsSysAdmin);
    }

    #endregion
}