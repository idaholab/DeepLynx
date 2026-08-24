using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class UserSensitivityLabelBusiness : IUserSensitivityLabelBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserSensitivityLabelBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for user sensitivity label operations</param>
    public UserSensitivityLabelBusiness(DeeplynxContext context)
    {
        _context = context;
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

    /// <summary>
    ///     List all users explicitly granted access to a given label
    /// </summary>
    /// <param name="labelId">ID of the label to list grants for</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>A list of user grants</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<IEnumerable<UserSensitivityLabelResponseDto>> GetUsersWithAccessToLabel(long labelId,
        long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var grants = await _context.UserSensitivityLabels
            .Where(g => g.LabelId == labelId)
            .Include(g => g.User)
            .Include(g => g.GrantedByUser)
            .ToListAsync();

        return grants.Select(g => new UserSensitivityLabelResponseDto
        {
            Id = g.Id,
            UserId = g.UserId,
            UserName = g.User.Name,
            UserEmail = g.User.Email,
            LabelId = g.LabelId,
            GrantedBy = g.GrantedBy,
            GrantedByName = g.GrantedByUser?.Name,
            GrantedAt = g.GrantedAt
        });
    }

    /// <summary>
    ///     Grant a user explicit access to a label
    /// </summary>
    /// <param name="currentUserId">ID of the user granting access</param>
    /// <param name="labelId">ID of the label to grant access to</param>
    /// <param name="userId">ID of the user to grant access to</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>The created grant</returns>
    /// <exception cref="KeyNotFoundException">Returned if label or user not found</exception>
    /// <exception cref="ArgumentException">Returned if the user already has access to the label</exception>
    public async Task<UserSensitivityLabelResponseDto> GrantLabelAccess(long currentUserId, long labelId,
        long userId, long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with id {userId} not found");

        var exists = await _context.UserSensitivityLabels
            .AnyAsync(g => g.LabelId == labelId && g.UserId == userId);
        if (exists)
            throw new ArgumentException($"User with id {userId} already has access to label {labelId}");

        var grant = new UserSensitivityLabel
        {
            UserId = userId,
            LabelId = labelId,
            GrantedBy = currentUserId,
            GrantedAt = DateTime.UtcNow
        };

        _context.UserSensitivityLabels.Add(grant);
        await _context.SaveChangesAsync();

        return new UserSensitivityLabelResponseDto
        {
            Id = grant.Id,
            UserId = grant.UserId,
            UserName = user.Name,
            UserEmail = user.Email,
            LabelId = grant.LabelId,
            GrantedBy = grant.GrantedBy,
            GrantedByName = null,
            GrantedAt = grant.GrantedAt
        };
    }

    /// <summary>
    ///     Revoke a user's explicit access to a label
    /// </summary>
    /// <param name="labelId">ID of the label to revoke access from</param>
    /// <param name="userId">ID of the user to revoke access from</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or user does not have access</exception>
    public async Task<bool> RevokeLabelAccess(long labelId, long userId, long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var grant = await _context.UserSensitivityLabels
            .FirstOrDefaultAsync(g => g.LabelId == labelId && g.UserId == userId);

        if (grant == null)
            throw new KeyNotFoundException($"User with id {userId} does not have access to label {labelId}");

        _context.UserSensitivityLabels.Remove(grant);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Set all users with access to a label (replaces existing grants)
    /// </summary>
    /// <param name="currentUserId">ID of the user updating the grants</param>
    /// <param name="labelId">ID of the label to update grants for</param>
    /// <param name="userIds">Array of user IDs to grant access to</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or any user ID is invalid</exception>
    public async Task<bool> SetUsersForLabel(long currentUserId, long labelId, long[] userIds, long organizationId,
        long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        if (users.Count != userIds.Length)
        {
            var missingIds = userIds.Except(users.Select(u => u.Id).ToList());
            throw new KeyNotFoundException($"Users not found: {string.Join(", ", missingIds)}");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var existingGrants = await _context.UserSensitivityLabels
            .Where(g => g.LabelId == labelId)
            .ToListAsync();
        _context.UserSensitivityLabels.RemoveRange(existingGrants);

        foreach (var userId in userIds)
        {
            _context.UserSensitivityLabels.Add(new UserSensitivityLabel
            {
                UserId = userId,
                LabelId = labelId,
                GrantedBy = currentUserId,
                GrantedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    /// <summary>
    ///     List all actions governed by a given label
    /// </summary>
    /// <param name="labelId">ID of the label to list governed actions for</param>
    /// <param name="organizationId">(Required) ID of the organization to which the label belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the label belongs</param>
    /// <returns>A list of governed permissions</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<IEnumerable<SensitivityLabelPermissionResponseDto>> GetPermissionsForLabel(long labelId,
        long organizationId, long? projectId)
    {
        await GetScopedLabel(labelId, organizationId, projectId);

        var permissions = await _context.SensitivityLabelPermissions
            .Where(p => p.LabelId == labelId)
            .ToListAsync();

        return permissions.Select(p => new SensitivityLabelPermissionResponseDto
        {
            Id = p.Id,
            LabelId = p.LabelId,
            Action = p.Action,
            Name = p.Name,
            Description = p.Description,
            LastUpdatedAt = p.LastUpdatedAt,
            LastUpdatedBy = p.LastUpdatedBy,
            IsArchived = p.IsArchived
        });
    }
}
