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
        .AnyAsync(pm => pm.IsProjectAdmin
            && pm.ProjectId == projectId
            && (pm.UserId == userId
                || pm.Group.Users.Any(gu => gu.Id == userId)));
  }

  public static async Task<List<long>> GetAdminProjectIds(
  DeeplynxContext context, long userId, long organizationId, List<long> projectIds)
  {
    if (projectIds.Count == 0)
      return new List<long>();

    return await context.ProjectMembers
        .Where(pm =>
            pm.IsProjectAdmin &&
            pm.Project.OrganizationId == organizationId &&
            projectIds.Contains(pm.ProjectId) &&
            (
                // Direct membership
                pm.UserId == userId ||
                // Group membership
                pm.Group.Users.Any(gu => gu.Id == userId)
            ))
        .Select(pm => pm.ProjectId)
        .Distinct()
        .ToListAsync();
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
              SELECT 1 FROM deeplynx.users
                WHERE id = {0} AND is_sys_admin = true
              UNION ALL
              SELECT 1 FROM deeplynx.organization_users
                WHERE user_id = {0} AND organization_id = {1} AND is_org_admin = true
              UNION ALL
              SELECT 1 FROM deeplynx.project_members pm
              JOIN deeplynx.projects p ON p.id = pm.project_id
                WHERE pm.project_id = {2}
                  AND pm.is_project_admin = true
                  AND p.organization_id = {1}
                  AND (
                    pm.user_id = {0}
                    OR EXISTS (
                      SELECT 1 FROM deeplynx.group_users gu
                        WHERE gu.group_id = pm.group_id AND gu.user_id = {0}
                    )
                  )
              LIMIT 1
              """
        : """
              SELECT 1 FROM deeplynx.users
                WHERE id = {0} AND is_sys_admin = true
              UNION ALL
              SELECT 1 FROM deeplynx.organization_users
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