using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class GroupBusiness : IGroupBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupBusiness" /> class.
    /// </summary>
    /// <param name="context">Database context used for group CRUD operations</param>
    /// <param name="eventBusiness">Used for logging events during CRUD operations</param>
    public GroupBusiness(DeeplynxContext context, IEventBusiness eventBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     Get all groups within an organization
    /// </summary>
    /// <param name="organizationId">ID of the organization from which to list groups</param>
    /// <param name="hideArchived">Boolean indicating whether to hide archived groups from results</param>
    /// <returns>An array of groups within the given organization</returns>
    public async Task<IEnumerable<GroupResponseDto>> GetAllGroups(long organizationId, bool hideArchived = true)
    {
        var groupQuery = _context.Groups.Where(g => g.OrganizationId == organizationId);

        if (hideArchived) groupQuery = groupQuery.Where(g => !g.IsArchived);

        return await groupQuery
            .Select(g => new GroupResponseDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                LastUpdatedAt = g.LastUpdatedAt,
                LastUpdatedBy = g.LastUpdatedBy,
                IsArchived = g.IsArchived,
                OrganizationId = g.OrganizationId,
                MemberCount = g.Users.Count(u => !u.IsArchived)
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Create a group
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID to which the group will belong</param>
    /// <param name="dto">The data from the user on how group should be configured</param>
    /// <returns>The newly created group</returns>
    public async Task<GroupResponseDto> CreateGroup(long currentUserId, long organizationId, CreateGroupRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var group = new Group
        {
            Name = dto.Name,
            Description = dto.Description,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            OrganizationId = organizationId
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // Log create Group event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "group",
                EntityId = group.Id,
                EntityName = group.Name,
                Properties = JsonSerializer.Serialize(new { group.Name }),
            });

        return new GroupResponseDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            LastUpdatedAt = group.LastUpdatedAt,
            LastUpdatedBy = group.LastUpdatedBy,
            IsArchived = group.IsArchived,
            OrganizationId = group.OrganizationId
        };
    }

    /// <summary>
    ///     Retrieves a specific group by ID and organization
    /// </summary>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">The ID by which to retrieve the group</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived groups from the result</param>
    /// <returns>The given group to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if the group is not found or is archived</exception>
    public async Task<GroupResponseDto> GetGroup(long organizationId, long groupId, bool hideArchived = true)
    {
        var group = await _context.Groups
            .Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

        if (group == null)
            throw new KeyNotFoundException($"Group with id {groupId} does not exist");

        if (hideArchived && group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} is archived");

        return new GroupResponseDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            LastUpdatedAt = group.LastUpdatedAt,
            LastUpdatedBy = group.LastUpdatedBy,
            IsArchived = group.IsArchived,
            OrganizationId = group.OrganizationId
        };
    }

    /// <summary>
    ///     Update a group with new information
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">The ID of the group to be updated</param>
    /// <param name="dto">The data transfer object holding information </param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<GroupResponseDto> UpdateGroup(long currentUserId, long organizationId, long groupId,
        UpdateGroupRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var group = await _context.Groups.Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        group.Name = dto.Name ?? group.Name;
        group.Description = dto.Description ?? group.Description;
        group.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        group.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log update Group event
        await _eventBusiness.CreateEvent(
            currentUserId,
            group.OrganizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "group",
                EntityId = group.Id,
                EntityName = group.Name,
                Properties = JsonSerializer.Serialize(new { group.Name }),
            });

        return new GroupResponseDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            LastUpdatedAt = group.LastUpdatedAt,
            LastUpdatedBy = group.LastUpdatedBy,
            IsArchived = group.IsArchived,
            OrganizationId = group.OrganizationId
        };
    }

    /// <summary>
    ///     Archive a specific group by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of the group to archive</param>
    /// <returns>Boolean true on successful archive</returns>
    /// <exception cref="KeyNotFoundException">Returned if group not found</exception>
    public async Task<bool> ArchiveGroup(long currentUserId, long organizationId, long groupId)
    {
        var group = await _context.Groups.Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found or is archived");

        group.IsArchived = true;
        group.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        group.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log archive Group event
        await _eventBusiness.CreateEvent(
            currentUserId,
            group.OrganizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "archive",
                EntityType = "group",
                EntityId = group.Id,
                EntityName = group.Name,
                Properties = JsonSerializer.Serialize(new { group.Name }),
            });

        return true;
    }

    /// <summary>
    ///     Unarchive a specific group by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of the group to unarchive</param>
    /// <returns>Boolean true on successful unarchive</returns>
    /// <exception cref="KeyNotFoundException">Returned if group not found</exception>
    public async Task<bool> UnarchiveGroup(long currentUserId, long organizationId, long groupId)
    {
        var group = await _context.Groups.Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null || !group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found or is not archived");

        group.IsArchived = false;
        group.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        group.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log unarchive Group event
        await _eventBusiness.CreateEvent(
            currentUserId,
            group.OrganizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "group",
                EntityId = group.Id,
                EntityName = group.Name,
                Properties = JsonSerializer.Serialize(new { group.Name }),
            });

        return true;
    }

    /// <summary>
    ///     Delete a specific group by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of the group to delete</param>
    /// <returns>Boolean true on successful delete</returns>
    /// <exception cref="KeyNotFoundException">Returned if group not found</exception>
    public async Task<bool> DeleteGroup(long currentUserId, long organizationId, long groupId)
    {
        var group = await _context.Groups.Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        var groupName = group.Name;

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();

        // Log delete Group event
        await _eventBusiness.CreateEvent(
            currentUserId,
            group.OrganizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "delete",
                EntityType = "group",
                EntityId = groupId,
                EntityName = groupName,
                Properties = JsonSerializer.Serialize(new { groupName })
            });

        return true;
    }

    /// <summary>
    ///     Add a user to a group
    /// </summary>
    /// <param name="userId">ID of user to add to group</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of group to add user to</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if group or user not found</exception>
    public async Task<bool> AddUserToGroup(long userId, long organizationId, long groupId)
    {
        var group = await _context.Groups.Where(g => g.Id == groupId && g.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null || user.IsArchived)
            throw new KeyNotFoundException($"User with id {userId} not found");

        // Service accounts are scoped to the project they are added to and never participate in
        // groups, which span projects and carry their own project memberships.
        if (user.AccountType == AccountType.Service)
            throw new UnauthorizedAccessException("Service accounts cannot be added to a group.");

        group.Users.Add(user);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Remove a user from a group
    /// </summary>
    /// <param name="userId">ID of user to remove from group</param>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of group to remove user from</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if group or user not found</exception>
    public async Task<bool> RemoveUserFromGroup(long userId, long organizationId, long groupId)
    {
        var group = await _context.Groups
            .Include(g => g.Users) // Loads only users in THIS group
            .FirstOrDefaultAsync(g => g.Id == groupId && organizationId == g.OrganizationId);

        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null || user.IsArchived)
            throw new KeyNotFoundException($"User with id {userId} does not exist");

        // Check if user is in the group
        if (!group.Users.Any(u => u.Id == userId))
            return false; // User exists in DB but not in this group

        group.Users.Remove(user);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Get all members of a group
    /// </summary>
    /// <param name="organizationId">The organization ID to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <returns>List of users who are members of the group</returns>
    /// <exception cref="KeyNotFoundException">Returned if group not found</exception>
    public async Task<IEnumerable<UserResponseDto>> GetGroupMembers(long organizationId, long groupId)
    {
        var group = await _context.Groups
            .Include(g => g.Users)
            .FirstOrDefaultAsync(g => g.Id == groupId && organizationId == g.OrganizationId);

        if (group == null || group.IsArchived)
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        return group.Users
            .Where(u => !u.IsArchived)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                AccountType = u.AccountType,
                IsSysAdmin = u.IsSysAdmin,
                IsArchived = u.IsArchived,
                IsActive = u.IsActive
            })
            .ToList();
    }
}