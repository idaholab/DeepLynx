using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore;

public static class AdminHelper
{
    public static async Task<bool> IsSysAdmin(DeeplynxContext context, long userId)
    {
        return await context.Users
            .AnyAsync(u => u.Id == userId && u.IsSysAdmin);
    }

    public static async Task<bool> IsOrgAdmin(DeeplynxContext context, long userId, long organizationId)
    {
        return await context.OrganizationUsers
            .AnyAsync(ou => ou.UserId == userId && ou.OrganizationId == organizationId && ou.IsOrgAdmin);
    }

    public static async Task<bool> IsProjectAdmin(DeeplynxContext context, long userId, long projectId)
    {
        return await context.ProjectMembers
            .AnyAsync(pm => pm.UserId == userId
                && pm.ProjectId == projectId
                && pm.RoleId != null
                && pm.Role.Name == "Admin");
    }

    /// <summary>
    /// Returns true if the user is a sys admin, an org admin within the given organization,
    /// or (optionally) a project admin within the given project — in a single database round-trip.
    /// </summary>
    public static async Task<bool> IsAnyAdmin(
        DeeplynxContext context,
        long userId,
        long organizationId,
        long? projectId = null)
    {
        var sql = projectId.HasValue
            ? """
              SELECT 1 FROM users
                WHERE id = {0} AND is_sys_admin = true
              UNION ALL
              SELECT 1 FROM organization_users
                WHERE user_id = {0} AND organization_id = {1} AND is_org_admin = true
              UNION ALL
              SELECT 1 FROM project_members pm
              JOIN roles r ON r.id = pm.role_id
                WHERE pm.user_id = {0} AND pm.project_id = {2} AND r.name = 'Admin'
              LIMIT 1
              """
            : """
              SELECT 1 FROM users
                WHERE id = {0} AND is_sys_admin = true
              UNION ALL
              SELECT 1 FROM organization_users
                WHERE user_id = {0} AND organization_id = {1} AND is_org_admin = true
              LIMIT 1
              """;

        var args = projectId.HasValue
            ? new object[] { userId, organizationId, projectId.Value }
            : new object[] { userId, organizationId };

        return await context.Database
            .SqlQueryRaw<int>(sql, args)
            .AnyAsync();
    }
}