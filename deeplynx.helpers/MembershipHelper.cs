using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers
{
    /// <summary>
    ///     Batch validation helpers confirming that sets of users and/or groups exist and belong
    ///     to a given organization (and optionally project) scope. Unlike ExistenceHelper, these
    ///     methods validate relationship membership for arrays of IDs in a single query each,
    ///     rather than a single cached existence/archived check per ID — batch-friendly by design
    ///     for bulk operations like setting grants across many members at once.
    /// </summary>
    public static class MembershipHelper
    {
        /// <summary>
        ///     Ensure every supplied user and group ID exists and is a member of the given
        ///     organization, and if projectId is supplied, additionally a member of that project.
        /// </summary>
        /// <param name="context">DB context</param>
        /// <param name="userIds">User IDs to validate</param>
        /// <param name="groupIds">Group IDs to validate</param>
        /// <param name="organizationId">(Required) Organization the members must belong to</param>
        /// <param name="projectId">(Optional) Project the members must additionally belong to</param>
        /// <exception cref="KeyNotFoundException">
        ///     Returned if any user or group is missing from the organization or (if supplied) project
        /// </exception>
        public static async Task ValidateMembers(
            DeeplynxContext context,
            long[] userIds,
            long[] groupIds,
            long organizationId,
            long? projectId)
        {
            if (userIds.Length > 0)
            {
                await ValidateUsers(context, userIds, organizationId, projectId);
            }

            if (groupIds.Length > 0)
            {
                await ValidateGroups(context, groupIds, organizationId, projectId);
            }
        }

        /// <summary>
        ///     Ensure every supplied user ID exists and is a member of the given organization,
        ///     and if projectId is supplied, additionally a direct member of that project.
        /// </summary>
        /// <exception cref="KeyNotFoundException">
        ///     Returned if any user is missing from the organization or (if supplied) project
        /// </exception>
        public static async Task ValidateUsers(
            DeeplynxContext context,
            long[] userIds,
            long organizationId,
            long? projectId)
        {
            var orgMemberUserIds = await context.OrganizationUsers
                .Where(ou => ou.OrganizationId == organizationId && userIds.Contains(ou.UserId))
                .Select(ou => ou.UserId)
                .ToListAsync();

            var missingFromOrg = userIds.Except(orgMemberUserIds).ToList();
            if (missingFromOrg.Count > 0)
                throw new KeyNotFoundException(
                    $"Users not found or not members of organization {organizationId}: {string.Join(", ", missingFromOrg)}");

            if (projectId.HasValue)
            {
                var projectMemberUserIds = await context.ProjectMembers
                    .Where(pm => pm.ProjectId == projectId && pm.UserId != null && userIds.Contains(pm.UserId.Value))
                    .Select(pm => pm.UserId!.Value)
                    .ToListAsync();

                var missingFromProject = userIds.Except(projectMemberUserIds).ToList();
                if (missingFromProject.Count > 0)
                    throw new KeyNotFoundException(
                        $"Users not members of project {projectId}: {string.Join(", ", missingFromProject)}");
            }
        }

        /// <summary>
        ///     Ensure every supplied group ID exists and belongs to the given organization,
        ///     and if projectId is supplied, additionally a direct member of that project.
        /// </summary>
        /// <exception cref="KeyNotFoundException">
        ///     Returned if any group is missing from the organization or (if supplied) project
        /// </exception>
        public static async Task ValidateGroups(
            DeeplynxContext context,
            long[] groupIds,
            long organizationId,
            long? projectId)
        {
            var orgGroupIds = await context.Groups
                .Where(g => groupIds.Contains(g.Id) && g.OrganizationId == organizationId)
                .Select(g => g.Id)
                .ToListAsync();

            var missingFromOrg = groupIds.Except(orgGroupIds).ToList();
            if (missingFromOrg.Count > 0)
                throw new KeyNotFoundException(
                    $"Groups not found or not members of organization {organizationId}: {string.Join(", ", missingFromOrg)}");

            if (projectId.HasValue)
            {
                var projectMemberGroupIds = await context.ProjectMembers
                    .Where(pm => pm.ProjectId == projectId && pm.GroupId != null && groupIds.Contains(pm.GroupId.Value))
                    .Select(pm => pm.GroupId!.Value)
                    .ToListAsync();

                var missingFromProject = groupIds.Except(projectMemberGroupIds).ToList();
                if (missingFromProject.Count > 0)
                    throw new KeyNotFoundException(
                        $"Groups not members of project {projectId}: {string.Join(", ", missingFromProject)}");
            }
        }
    }
}