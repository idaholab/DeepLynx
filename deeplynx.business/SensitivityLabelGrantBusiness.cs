using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class SensitivityLabelGrantBusiness : ISensitivityLabelGrantBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ISensitivityLabelService _sensitivityLabelService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelGrantBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for user and group sensitivity label operations</param>
    /// <param name="sensitivityLabelService">Used for sensitivity label record authorization.</param>
    public SensitivityLabelGrantBusiness(
        DeeplynxContext context,
        ISensitivityLabelService sensitivityLabelService)
    {
        _context = context;
        _sensitivityLabelService = sensitivityLabelService;
    }
    
    /// <summary>
    ///     List all users and groups explicitly granted access to a given label, along
    ///     with each member's set of granted permissions. Groups include member users.
    /// </summary>
    /// <param name="labelId">ID of the label to list grants for</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>A list of members (users and groups) and their permissions on the label</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<IEnumerable<SensitivityLabelMemberAccessDto>> GetMembersWithLabelAccess(
        long labelId, long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var grants = await _context.SensitivityLabelGrants
            .Where(g => g.LabelId == labelId)
            .Include(g => g.User)
            .Include(g => g.Group).ThenInclude(gr => gr.Users)
            .Include(g => g.LabelPermission)
            .Include(g => g.GrantedByUser)
            .ToListAsync();

        // group by user and group for readability when listing
        var byUser = grants
            .Where(g => g.UserId != null)
            .GroupBy(g => g.UserId!.Value)
            .Select(group => ToMemberAccessDto(group.ToList()));

        var byGroup = grants
            .Where(g => g.GroupId != null)
            .GroupBy(g => g.GroupId!.Value)
            .Select(group => ToMemberAccessDto(group.ToList()));

        // combine user and group results
        return byUser.Concat(byGroup);
    }
    
    /// <summary>
    ///     List all permissions a user or group possesses on a given label.
    ///     Exactly one of userId/groupId must be provided.
    /// </summary>
    /// <param name="labelId">ID of the label for which to list available user permissions</param>
    /// <param name="userId">ID of the user for which to list available label permissions</param>
    /// <param name="groupId">ID of the group for which to list available label permissions</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>A list of governed permissions for the member (empty if no grants on this label)</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    /// <exception cref="ArgumentException">Returned if both or neither of userId/groupId are provided</exception>
    public async Task<IEnumerable<SensitivityLabelMemberAccessDto>> GetMemberPermissionsForLabel(
        long labelId, long organizationId, long? projectId,
        long? userId = null, long? groupId = null)
    {
        ValidateSingleMember(userId, groupId);
        await GetScopedLabel(labelId, organizationId, projectId);

        var query = _context.SensitivityLabelGrants
            .AsNoTracking()
            .Where(g => g.LabelId == labelId)
            .Include(g => g.LabelPermission)
            .Include(g => g.GrantedByUser)
            .AsQueryable();

        query = userId.HasValue
            ? query.Include(g => g.User).Where(g => g.UserId == userId)
            : query.Include(g => g.Group).ThenInclude(gr => gr.Users).Where(g => g.GroupId == groupId);

        var grants = await query.ToListAsync();
        if (grants.Count == 0)
            return Enumerable.Empty<SensitivityLabelMemberAccessDto>();

        return new[] { ToMemberAccessDto(grants) };
    }

    /// <summary>
    ///     Grant a mixed set of users and/or groups a set of permissions on a label.
    ///     Replace any existing grants belonging to the specified members on the label.
    ///     Other users/groups on this label are left untouched.
    /// </summary>
    /// <param name="currentUserId">ID of the user granting access</param>
    /// <param name="labelId">ID of the label to grant access to</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <param name="dto">The users/groups to grant and the permissions to give them</param>
    /// <returns>A list of members (users and groups) and their new permissions on the label</returns>
    /// <exception cref="ArgumentException">Returned if no members or no permissions are provided</exception>
    /// <exception cref="KeyNotFoundException">Returned if label, a member, or a permission is not found</exception>
    public async Task<IEnumerable<SensitivityLabelMemberAccessDto>> SetAccessForLabel(
        long currentUserId, long labelId, 
        long organizationId, long? projectId,
        GrantLabelAccessDto dto)
    {
        var userIds = dto.UserIds ?? Array.Empty<long>();
        var groupIds = dto.GroupIds ?? Array.Empty<long>();
        var labelPermissionIds = dto.LabelPermissionIds;

        if (userIds.Length == 0 && groupIds.Length == 0)
            throw new ArgumentException("At least one userId or groupId must be provided");
        if (labelPermissionIds.Length == 0)
            throw new ArgumentException("At least one labelPermissionId must be provided");

        await GetScopedLabel(labelId, organizationId, projectId);
        // ensure the user or group exists and is a member of the given org (and project, if scoped)
        await MembershipHelper.ValidateMembers(_context, userIds, groupIds, organizationId, projectId);
        
        // hydrate users, groups and permissions so we can return them without an additional lookup
        var users = userIds.Length > 0
            ? await _context.Users.Where(u => userIds.Contains(u.Id)).ToListAsync()
            : new List<User>(); 

        var groups = groupIds.Length > 0
            ? await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync()
            : new List<Group>(); 
        
        var permissions = await ValidateAndTrackPermissions(labelPermissionIds);

        // to avoid failures somewhere in the batch, wrap everything in a transaction to ensure completeness
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // remove only grants belonging to members and labels being set
        var deletedCount = await _context.SensitivityLabelGrants
            .Where(g => g.LabelId == labelId &&
                    ((g.UserId != null && userIds.Contains(g.UserId.Value)) ||
                    (g.GroupId != null && groupIds.Contains(g.GroupId.Value))))
            .ExecuteDeleteAsync();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var newGrants = new List<SensitivityLabelGrant>();

        foreach (var permission in permissions)
        {
            // apply permissions to each user
            foreach (var user in users)
            {
                var grant = new SensitivityLabelGrant
                {
                    UserId = user.Id,
                    User = user,
                    LabelId = labelId,
                    LabelPermissionId = permission.Id,
                    LabelPermission = permission,
                    GrantedBy = currentUserId,
                    GrantedAt = now
                };
                _context.SensitivityLabelGrants.Add(grant);
                newGrants.Add(grant);
            }

            // apply permissions to each group
            foreach (var group in groups)
            {
                var grant = new SensitivityLabelGrant
                {
                    GroupId = group.Id,
                    Group = group,
                    LabelId = labelId,
                    LabelPermissionId = permission.Id,
                    LabelPermission = permission,
                    GrantedBy = currentUserId,
                    GrantedAt = now
                };
                _context.SensitivityLabelGrants.Add(grant);
                newGrants.Add(grant);
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // invalidate cached sensitivity labels for all affected users
        await InvalidateForMembers(labelId, userIds, groupIds);

        var byUser = newGrants
            .Where(g => g.UserId != null)
            .GroupBy(g => g.UserId!.Value)
            .Select(group => ToMemberAccessDto(group.ToList()));

        var byGroup = newGrants
            .Where(g => g.GroupId != null)
            .GroupBy(g => g.GroupId!.Value)
            .Select(group => ToMemberAccessDto(group.ToList()));

        return byUser.Concat(byGroup);
    }

    /// <summary>
    ///     Revoke all of a mixed set of users' and/or groups' access to a label.
    /// </summary>
    /// <param name="labelId">ID of the label to revoke access from</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <param name="userIds">Array of user IDs to revoke access from</param>
    /// <param name="groupIds">Array of group IDs to revoke access from</param>
    /// <returns>True if successful</returns>
    /// <exception cref="ArgumentException">Returned if no members are provided</exception>
    /// <exception cref="KeyNotFoundException">Returned if label not found or no matching grants exist</exception>
    public async Task<bool> RevokeAccessForLabel(
        long labelId, long organizationId, long? projectId,
        long[]? userIds, long[]? groupIds)
    {
        userIds ??= Array.Empty<long>();
        groupIds ??= Array.Empty<long>();

        if (userIds.Length == 0 && groupIds.Length == 0)
            throw new ArgumentException("At least one userId or groupId must be provided");

        await GetScopedLabel(labelId, organizationId, projectId);

        var deletedCount = await _context.SensitivityLabelGrants
            .Where(g => g.LabelId == labelId &&
                        ((g.UserId != null && userIds.Contains(g.UserId.Value)) ||
                        (g.GroupId != null && groupIds.Contains(g.GroupId.Value))))
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            throw new KeyNotFoundException("No matching grants found to revoke");

        await InvalidateForMembers(labelId, userIds, groupIds);
        return true;
    }

    // --------------------- Helpers ---------------------
    
    // make sure only one of group ID or user ID is supplied
    private static void ValidateSingleMember(long? userId, long? groupId)
    {
        if (userId.HasValue == groupId.HasValue)
            throw new ArgumentException("Exactly one of userId or groupId must be provided");
    }

    // make sure the supplied permission IDs correspond with values from the DB
    // make sure the supplied permission action IDs correspond with values from the DB
    private async Task<List<SensitivityLabelPermissionAction>> ValidateAndTrackPermissions(long[] labelPermissionIds)
    {
        var found = await _context.SensitivityLabelPermissionActions
            .Where(p => labelPermissionIds.Contains(p.Id))
            .ToListAsync();
        var missing = labelPermissionIds.Except(found.Select(p => p.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException(
                $"Invalid label permission actions supplied: {string.Join(", ", missing)}");

        return found;
    }

    // invalidate cached permissions for the given individual users and group members
    private async Task InvalidateForMembers(long labelId, long[] userIds, long[] groupIds)
    {
        var affectedUserIds = new HashSet<long>(userIds);
        var allGroupIds = new HashSet<long>(groupIds);

        // flatten group members into the user hashset
        if (allGroupIds.Count > 0)
        {
            var groupMemberIds = await _context.Groups
                .Where(g => allGroupIds.Contains(g.Id))
                .SelectMany(g => g.Users.Select(u => u.Id))
                .ToListAsync();
            foreach (var id in groupMemberIds)
                affectedUserIds.Add(id);
        }

        // for each affected user, invalidate the cache
        foreach (var uid in affectedUserIds)
            await _sensitivityLabelService.InvalidateAuthorizedLabelsCache(labelId, uid);
    }

    // builds a list of a member's grant rows. All grants must belong to the same user or the same group.
    private static SensitivityLabelMemberAccessDto ToMemberAccessDto(List<SensitivityLabelGrant> grants)
    {
        // get the user info from the first item in the list
        var first = grants[0];

        return new SensitivityLabelMemberAccessDto
        {
            UserId = first.UserId,
            UserName = first.User?.Name,
            UserEmail = first.User?.Email,
            GroupId = first.GroupId,
            GroupName = first.Group?.Name,
            GroupMembers = first.Group?.Users?.Select(u => new GroupMemberDto
            {
                UserId = u.Id,
                UserName = u.Name,
                UserEmail = u.Email
            }).ToList(),
            // roll up permissions for the user to avoid redundancy of user info
            Permissions = grants.Select(g => new LabelPermissionGrantDto
            {
                GrantId = g.Id,
                LabelPermissionId = g.LabelPermissionId,
                LabelPermissionName = g.LabelPermission?.Name,
                LabelPermissionDescription = g.LabelPermission?.Description,
                GrantedAt = g.GrantedAt,
                GrantedBy = g.GrantedBy
            }).ToList()
        };
    }
    
    private async Task<SensitivityLabel> GetScopedLabel(long labelId, long organizationId, long? projectId)
    {
        var query = _context.SensitivityLabels
            .AsNoTracking()
            .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

        if (projectId.HasValue)
        {
            query = query.Where(l => l.ProjectId == projectId || l.ProjectId == null);
        }

        var label = await query.FirstOrDefaultAsync();

        if (label == null)
            throw new KeyNotFoundException(
                $"Sensitivity label with id {labelId} not found or does not belong to the specified organization/project context");

        return label;
    }

}
