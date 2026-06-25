using deeplynx.datalayer.Models;
using deeplynx.helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Middleware;

/// <summary>
/// Integration tests for <see cref="OrgRolePermissionService.PermissionInOrg"/>.
///
/// Org-level membership in the schema is binary (organization_users only carries is_org_admin) —
/// there is no org-level user->role assignment. The intended authorization model is therefore:
///   - org admins have full permission to every action/resource in the org;
///   - non-admin members get read-only access to org-scoped resources;
///   - any create/update/delete (write/update) action requires org admin;
///   - non-members get nothing.
///
/// These tests run against the real Postgres test container so the raw SQL in the service is exercised.
/// </summary>
[Collection("Test Suite Collection")]
public class OrgRolePermissionServiceTests : IntegrationTestBase
{
    private OrgRolePermissionService _service = null!;

    public OrgRolePermissionServiceTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _service = new OrgRolePermissionService(Context, new Mock<ILogger<OrgRolePermissionService>>().Object);
    }

    /// <summary>
    /// Seeds an organization with an org admin, a non-admin member, and a non-member. The org also
    /// contains a role that grants update + write on the "organization" resource — but, per the schema,
    /// that role is NOT (and cannot be) assigned to any specific member.
    /// </summary>
    private async Task<(long orgId, long adminUserId, long memberUserId, long outsiderUserId)> SeedOrgScenarioAsync()
    {
        var org = new Organization
        {
            Name = $"Perm Test Org {Guid.NewGuid()}",
            Description = "PermissionInOrg test org"
        };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();

        var adminUser = new User { Name = "Org Admin", Email = $"admin-{Guid.NewGuid()}@test.com", Username = $"admin-{Guid.NewGuid()}", IsActive = true };
        var memberUser = new User { Name = "Org Member", Email = $"member-{Guid.NewGuid()}@test.com", Username = $"member-{Guid.NewGuid()}", IsActive = true };
        var outsiderUser = new User { Name = "Outsider", Email = $"outsider-{Guid.NewGuid()}@test.com", Username = $"outsider-{Guid.NewGuid()}", IsActive = true };
        Context.Users.AddRange(adminUser, memberUser, outsiderUser);
        await Context.SaveChangesAsync();

        Context.Set<OrganizationUser>().AddRange(
            new OrganizationUser { OrganizationId = org.Id, UserId = adminUser.Id, IsOrgAdmin = true },
            new OrganizationUser { OrganizationId = org.Id, UserId = memberUser.Id, IsOrgAdmin = false });
        await Context.SaveChangesAsync();

        // A role in the org that DOES carry elevated permissions. The whole point of the fix is that
        // the mere existence of such a role must NOT leak its permissions to non-admin members.
        var elevatedRole = new Role
        {
            Name = "Elevated",
            OrganizationId = org.Id,
            ProjectId = null,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Roles.Add(elevatedRole);

        var updateOrg = new Permission { Name = "update org", Action = "update", Resource = "organization", OrganizationId = org.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
        var writeUser = new Permission { Name = "write user", Action = "write", Resource = "user", OrganizationId = org.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
        var readOrg = new Permission { Name = "read org", Action = "read", Resource = "organization", OrganizationId = org.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };
        Context.Permissions.AddRange(updateOrg, writeUser, readOrg);
        await Context.SaveChangesAsync();

        elevatedRole.Permissions.Add(updateOrg);
        elevatedRole.Permissions.Add(writeUser);
        elevatedRole.Permissions.Add(readOrg);
        await Context.SaveChangesAsync();

        return (org.Id, adminUser.Id, memberUser.Id, outsiderUser.Id);
    }

    [Fact]
    public async Task OrgAdmin_HasPermission_ForReadUpdateAndWrite()
    {
        var (orgId, adminUserId, _, _) = await SeedOrgScenarioAsync();

        Assert.True(await _service.PermissionInOrg(adminUserId, orgId, "read", "organization"));
        Assert.True(await _service.PermissionInOrg(adminUserId, orgId, "update", "organization"));
        Assert.True(await _service.PermissionInOrg(adminUserId, orgId, "write", "user"));
    }

    [Fact]
    public async Task NonAdminMember_CanRead()
    {
        var (orgId, _, memberUserId, _) = await SeedOrgScenarioAsync();

        Assert.True(await _service.PermissionInOrg(memberUserId, orgId, "read", "organization"));
    }

    /// <summary>
    /// Regression test for the privilege-escalation bug: a non-admin member must NOT inherit
    /// write/update permissions just because some role in the org defines them. Previously the
    /// permission query joined roles purely on organization_id, granting every member every
    /// permission any org role held — which let a regular user promote themselves to org admin.
    /// </summary>
    [Fact]
    public async Task NonAdminMember_CannotUpdateOrWrite_EvenWhenOrgHasARoleGrantingIt()
    {
        var (orgId, _, memberUserId, _) = await SeedOrgScenarioAsync();

        Assert.False(await _service.PermissionInOrg(memberUserId, orgId, "update", "organization"));
        Assert.False(await _service.PermissionInOrg(memberUserId, orgId, "write", "user"));
    }

    [Fact]
    public async Task NonMember_HasNoPermission_ForAnyAction()
    {
        var (orgId, _, _, outsiderUserId) = await SeedOrgScenarioAsync();

        Assert.False(await _service.PermissionInOrg(outsiderUserId, orgId, "read", "organization"));
        Assert.False(await _service.PermissionInOrg(outsiderUserId, orgId, "update", "organization"));
        Assert.False(await _service.PermissionInOrg(outsiderUserId, orgId, "write", "user"));
    }
}
