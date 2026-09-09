using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class GroupSensitivityLabelBusiness : IGroupSensitivityLabelBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupSensitivityLabelBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for group sensitivity label operations</param>
    public GroupSensitivityLabelBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     List all groups explicitly granted access to a given label
    /// </summary>
    /// <param name="labelId">ID of the label to list grants for</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>A list of group grants</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<IEnumerable<GroupSensitivityLabelResponseDto>> GetGroupsWithAccessToLabel(long labelId,
        long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var grants = await _context.GroupSensitivityLabels
            .Where(g => g.LabelId == labelId)
            .Include(g => g.Group)
            .Include(g => g.GrantedByUser)
            .ToListAsync();

        return grants.Select(g => new GroupSensitivityLabelResponseDto
        {
            Id = g.Id,
            GroupId = g.GroupId,
            GroupName = g.Group.Name,
            LabelId = g.LabelId,
            GrantedBy = g.GrantedBy,
            GrantedByName = g.GrantedByUser?.Name,
            GrantedAt = g.GrantedAt
        });
    }

    /// <summary>
    ///     Grant a group explicit access to a label
    /// </summary>
    /// <param name="currentUserId">ID of the user granting access</param>
    /// <param name="labelId">ID of the label to grant access to</param>
    /// <param name="groupId">ID of the group to grant access to</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>The created grant</returns>
    /// <exception cref="KeyNotFoundException">Returned if label or group not found</exception>
    /// <exception cref="ArgumentException">Returned if the group already has access to the label</exception>
    public async Task<GroupSensitivityLabelResponseDto> GrantLabelAccessToGroup(long currentUserId, long labelId,
        long groupId, long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var group = await _context.Groups
            .Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        var exists = await _context.GroupSensitivityLabels
            .AnyAsync(g => g.LabelId == labelId && g.GroupId == groupId);
        if (exists)
            throw new ArgumentException($"Group with id {groupId} already has access to label {labelId}");

        var grant = new GroupSensitivityLabel
        {
            GroupId = groupId,
            LabelId = labelId,
            GrantedBy = currentUserId,
            GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.GroupSensitivityLabels.Add(grant);
        await _context.SaveChangesAsync();

        return new GroupSensitivityLabelResponseDto
        {
            Id = grant.Id,
            GroupId = grant.GroupId,
            GroupName = group.Name,
            LabelId = grant.LabelId,
            GrantedBy = grant.GrantedBy,
            GrantedByName = null,
            GrantedAt = grant.GrantedAt
        };
    }

    /// <summary>
    ///     Revoke a group's explicit access to a label
    /// </summary>
    /// <param name="labelId">ID of the label to revoke access from</param>
    /// <param name="groupId">ID of the group to revoke access from</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or group does not have access</exception>
    public async Task<bool> RevokeLabelAccessFromGroup(long labelId, long groupId, long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var grant = await _context.GroupSensitivityLabels
            .FirstOrDefaultAsync(g => g.LabelId == labelId && g.GroupId == groupId);

        if (grant == null)
            throw new KeyNotFoundException($"Group with id {groupId} does not have access to label {labelId}");

        _context.GroupSensitivityLabels.Remove(grant);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Set all groups with access to a label (replaces existing grants)
    /// </summary>
    /// <param name="currentUserId">ID of the user updating the grants</param>
    /// <param name="labelId">ID of the label to update grants for</param>
    /// <param name="groupIds">Array of group IDs to grant access to</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or any group ID is invalid</exception>
    public async Task<bool> SetGroupsForLabel(long currentUserId, long labelId, long[] groupIds, long organizationId,
        long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var groups = await _context.Groups
            .Where(g => groupIds.Contains(g.Id) && g.OrganizationId == organizationId)
            .ToListAsync();

        if (groups.Count != groupIds.Length)
        {
            var missingIds = groupIds.Except(groups.Select(g => g.Id).ToList());
            throw new KeyNotFoundException($"Groups not found: {string.Join(", ", missingIds)}");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var existingGrants = await _context.GroupSensitivityLabels
            .Where(g => g.LabelId == labelId)
            .ToListAsync();
        _context.GroupSensitivityLabels.RemoveRange(existingGrants);

        foreach (var groupId in groupIds)
        {
            _context.GroupSensitivityLabels.Add(new GroupSensitivityLabel
            {
                GroupId = groupId,
                LabelId = labelId,
                GrantedBy = currentUserId,
                GrantedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private async Task<SensitivityLabel> GetScopedLabel(long labelId, long organizationId, long? projectId)
    {
        var query = _context.SensitivityLabels
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
