using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Middleware;

[Collection("Test Suite Collection")]
public class ProjectRolePermissionServiceCachingTests : IntegrationTestBase
{
    private ProjectRolePermissionService _service = null!;

    public ProjectRolePermissionServiceCachingTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _service = new ProjectRolePermissionService(Context, new Mock<ILogger<ProjectRolePermissionService>>().Object);
    }

    private async Task<(long projectId, long userId)> SeedGrantedPermissionAsync(string action, string resource)
    {
        var org = new Organization { Name = $"Cache Test Org {Guid.NewGuid()}" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();

        var project = new Project { Name = "Cache Test Project", OrganizationId = org.Id };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();

        var user = new User { Name = "Cache Test User", Email = $"cache-{Guid.NewGuid()}@test.com", Username = $"cache-{Guid.NewGuid()}", IsActive = true };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var role = new Role { Name = "Granting Role", OrganizationId = org.Id, ProjectId = project.Id };
        Context.Roles.Add(role);
        await Context.SaveChangesAsync();

        var permission = new Permission { Name = "Grant", Action = action, Resource = resource, OrganizationId = org.Id };
        Context.Permissions.Add(permission);
        await Context.SaveChangesAsync();

        role.Permissions.Add(permission);
        Context.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = user.Id, RoleId = role.Id });
        await Context.SaveChangesAsync();

        return (project.Id, user.Id);
    }

    [Fact]
    public async Task PermissionInProject_CachesResult_SoSubsequentCallsReflectCacheNotLiveState()
    {
        var (projectId, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        // First call: cache miss, hits DB, caches "true"
        Assert.True(await _service.PermissionInProject(userId, projectId, "read", "widgets"));

        // Mutate the underlying grant directly in the DB, bypassing any business-layer invalidation
        var member = await Context.ProjectMembers.FirstAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        member.RoleId = null;
        await Context.SaveChangesAsync();

        // Second call: still reads the (now stale) cached "true", proving the cache is authoritative
        // once populated — this is the behavior that makes correct invalidation elsewhere essential.
        Assert.True(await _service.PermissionInProject(userId, projectId, "read", "widgets"));
    }

    [Fact]
    public async Task PermissionsInProjects_ReturnsBothCachedAndFreshlyQueriedAuthorizedIds()
    {
        var (projectId1, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        var org2 = new Organization { Name = $"Cache Test Org 2 {Guid.NewGuid()}" };
        Context.Organizations.Add(org2);
        await Context.SaveChangesAsync();

        var project2 = new Project { Name = "Cache Test Project 2", OrganizationId = org2.Id };
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();

        var role2 = new Role { Name = "Granting Role 2", OrganizationId = org2.Id, ProjectId = project2.Id };
        Context.Roles.Add(role2);
        await Context.SaveChangesAsync();

        var permission2 = new Permission { Name = "Grant 2", Action = "read", Resource = "widgets", OrganizationId = org2.Id };
        Context.Permissions.Add(permission2);
        await Context.SaveChangesAsync();

        role2.Permissions.Add(permission2);
        Context.ProjectMembers.Add(new ProjectMember { ProjectId = project2.Id, UserId = userId, RoleId = role2.Id });
        await Context.SaveChangesAsync();

        // Pre-warm the cache for project1 only, so project2 will be a genuine cache miss
        // when PermissionsInProjects runs below.
        await _service.PermissionInProject(userId, projectId1, "read", "widgets");

        var result = await _service.PermissionsInProjects(userId, new[] { projectId1, project2.Id }, "read", "widgets");

        // Before the fix, this would return only [project2.Id] — project1 (served from cache)
        // was being silently dropped from the final result.
        Assert.Contains(projectId1, result);
        Assert.Contains(project2.Id, result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task PermissionsInProjects_UnauthorizedProject_NotIncludedInResult()
    {
        var (projectId, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        var result = await _service.PermissionsInProjects(userId, new[] { projectId }, "write", "widgets");

        Assert.DoesNotContain(projectId, result);
    }

    [Fact]
    public async Task GetPermittedProjectIdsAsync_CacheMiss_QueriesDbAndPopulatesCache()
    {
        var (projectId, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        var cacheKey = CacheKeys.ProjectPermittedIds(userId, "read", "widgets");
        await CacheService.Instance.DeleteAsync(cacheKey);

        var result = await _service.GetPermittedProjectIdsAsync(userId, "read", "widgets");

        Assert.Contains(projectId, result);

        var cached = await CacheService.Instance.GetAsync<List<long>>(cacheKey);
        Assert.NotNull(cached);
        Assert.Contains(projectId, cached);
    }

    [Fact]
    public async Task GetPermittedProjectIdsAsync_CachesResult_SoSubsequentCallsReflectCacheNotLiveState()
    {
        var (projectId, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        var cacheKey = CacheKeys.ProjectPermittedIds(userId, "read", "widgets");
        await CacheService.Instance.DeleteAsync(cacheKey);

        // First call: cache miss, hits DB, caches [projectId]
        var firstResult = await _service.GetPermittedProjectIdsAsync(userId, "read", "widgets");
        Assert.Contains(projectId, firstResult);

        // Mutate the underlying grant directly in the DB, bypassing any business-layer
        // invalidation, exactly as the PermissionInProject staleness test does above.
        var member = await Context.ProjectMembers.FirstAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        member.RoleId = null;
        await Context.SaveChangesAsync();

        // Second call: still reflects the (now stale) cached list rather than the live,
        // now-empty grant — proving the cache is authoritative once populated.
        var secondResult = await _service.GetPermittedProjectIdsAsync(userId, "read", "widgets");
        Assert.Contains(projectId, secondResult);
    }

    [Fact]
    public async Task GetPermittedProjectIdsAsync_CacheHit_DoesNotReturnFreshlyRevokedProject_UntilInvalidated()
    {
        var (projectId, userId) = await SeedGrantedPermissionAsync("read", "widgets");

        var cacheKey = CacheKeys.ProjectPermittedIds(userId, "read", "widgets");
        await CacheService.Instance.DeleteAsync(cacheKey);

        await _service.GetPermittedProjectIdsAsync(userId, "read", "widgets"); // populate cache

        var member = await Context.ProjectMembers.FirstAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        member.RoleId = null;
        await Context.SaveChangesAsync();

        // Explicitly invalidate, simulating what a business-layer mutation would do
        await CacheService.Instance.DeleteAsync(cacheKey);

        var result = await _service.GetPermittedProjectIdsAsync(userId, "read", "widgets");

        Assert.DoesNotContain(projectId, result);
    }

    [Fact]
    public async Task GetPermittedProjectIdsAsync_EmptyResult_IsCachedAndNotReQueried()
    {
        var org = new Organization { Name = $"Cache Test Org {Guid.NewGuid()}" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();

        var user = new User { Name = "No Access User", Email = $"noaccess-{Guid.NewGuid()}@test.com", Username = $"noaccess-{Guid.NewGuid()}", IsActive = true };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var cacheKey = CacheKeys.ProjectPermittedIds(user.Id, "read", "widgets");
        await CacheService.Instance.DeleteAsync(cacheKey);

        // First call: no grants exist anywhere for this user — DB query legitimately
        // returns an empty list, which must still be cached (not treated as a miss forever).
        var firstResult = await _service.GetPermittedProjectIdsAsync(user.Id, "read", "widgets");
        Assert.Empty(firstResult);

        var cached = await CacheService.Instance.GetAsync<List<long>>(cacheKey);
        Assert.NotNull(cached);
        Assert.Empty(cached);

        // Now grant access directly in the DB without going through business-layer
        // invalidation. If the empty list weren't being served from cache, this call would
        // pick up the new grant; since it IS served from cache, it must still be empty.
        var org2 = new Organization { Name = $"Cache Test Org 2 {Guid.NewGuid()}" };
        Context.Organizations.Add(org2);
        await Context.SaveChangesAsync();

        var project = new Project { Name = "Newly Granted Project", OrganizationId = org2.Id };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();

        var role = new Role { Name = "New Role", OrganizationId = org2.Id, ProjectId = project.Id };
        Context.Roles.Add(role);
        await Context.SaveChangesAsync();

        var permission = new Permission { Name = "New Grant", Action = "read", Resource = "widgets", OrganizationId = org2.Id };
        Context.Permissions.Add(permission);
        await Context.SaveChangesAsync();

        role.Permissions.Add(permission);
        Context.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = user.Id, RoleId = role.Id });
        await Context.SaveChangesAsync();

        var secondResult = await _service.GetPermittedProjectIdsAsync(user.Id, "read", "widgets");
        Assert.Empty(secondResult); // still the stale cached empty list
    }
}