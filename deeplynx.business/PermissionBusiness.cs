using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

/// <summary>
///     PermissionBusiness is unique from other business classes in the sense that it
///     is partially protected. Default permissions (marked with "isDefault")
///     should not be tampered with via standard CRUD operations via the API.
///     As such, special checks are in place to ensure that
///     permissions being edited by the user are only those which were originally
///     user-defined.
/// </summary>
public class PermissionBusiness : IPermissionBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for permission operations</param>
    public PermissionBusiness(DeeplynxContext context, IEventBusiness eventBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     List all permissions
    /// </summary>
    /// <param name="labelId">(Optional)ID of a sensitivity label to filter by</param>
    /// <param name="projectId">(Optional)ID of a project to filter by</param>
    /// <param name="organizationId">(Optional)ID of an organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to search on archived permissions</param>
    /// <returns>A list of permissions</returns>
    public async Task<IEnumerable<PermissionResponseDto>> GetAllPermissions(
        long? labelId, long? projectId, long? organizationId,
        bool hideArchived = true)
    {
        // Always returns default permissions alongside those from the supplied org ID or project ID
        // This allows the user to see all permissions available for use in the given context (defaults being global)
        var permissionQuery = _context.Permissions.Where(p =>
            p.IsDefault || (!p.IsDefault &&
            (!projectId.HasValue || p.ProjectId == projectId) && 
            p.OrganizationId == organizationId &&
            (!labelId.HasValue || p.LabelId == labelId)));

        if (hideArchived)
            permissionQuery = permissionQuery.Where(p => !p.IsArchived);

        return await permissionQuery.Select(p => new PermissionResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Action = p.Action,
                Resource = p.Resource,
                LastUpdatedAt = p.LastUpdatedAt,
                LastUpdatedBy = p.LastUpdatedBy,
                IsArchived = p.IsArchived,
                LabelId = p.LabelId,
                ProjectId = p.ProjectId,
                OrganizationId = p.OrganizationId,
                IsDefault = p.IsDefault
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Get a permission by ID
    /// </summary>
    /// <param name="permissionId">ID of the permission to retrieve</param>
    /// <param name="hideArchived"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<PermissionResponseDto> GetPermission(long? organizationId, long? projectId, long permissionId, bool hideArchived = true)
    {
        var permission = await _context.Permissions
            .Where(p => p.Id == permissionId && 
                        (p.IsDefault || 
                         (!p.IsDefault && // For non-default permissions, check scope matches
                          (!projectId.HasValue || p.ProjectId == projectId) && 
                          (!organizationId.HasValue || p.OrganizationId == organizationId))))
            .FirstOrDefaultAsync();
    
        if (permission == null)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found");

        if (hideArchived && permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} is archived");

        return new PermissionResponseDto
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Action = permission.Action,
            Resource = permission.Resource,
            LastUpdatedAt = permission.LastUpdatedAt,
            LastUpdatedBy = permission.LastUpdatedBy,
            IsArchived = permission.IsArchived,
            LabelId = permission.LabelId,
            ProjectId = permission.ProjectId,
            OrganizationId = permission.OrganizationId,
            IsDefault = permission.IsDefault
        };
    }

    /// <summary>
    ///     Create a new user-defined permission
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="dto">The permission to be created</param>
    /// <param name="projectId">ID of the project to which the permission belongs</param>
    /// <param name="organizationId">ID of the organization to which the permission belongs</param>
    /// <returns>The newly created permission</returns>
    /// <exception cref="ArgumentException">Returned if project/org both supplied or no project/org supplied</exception>
    public async Task<PermissionResponseDto> CreatePermission(
        long currentUserId,
        CreatePermissionRequestDto dto,
        long? projectId, long organizationId)
    {
        ValidationHelper.ValidateModel(dto);
        
        // Note that the CreatePermission dto only allows for the creation of permissions
        // using labelId. Any Default permissions such as "write projects" should not
        // be manipulated by users.
        var permission = new Permission
        {
            Name = dto.Name,
            Description = dto.Description,
            Action = dto.Action,
            LabelId = dto.LabelId,
            IsDefault = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            ProjectId = projectId.HasValue ? projectId : null,
            OrganizationId = organizationId
        };

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        // Log create Permission event
        await _eventBusiness.CreateEvent(
            currentUserId, 
            organizationId, 
            projectId, 
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "permission",
                EntityId = permission.Id,
                EntityName = permission.Name,
                Properties = JsonSerializer.Serialize(new { permission.Name })
            }
        );

        return new PermissionResponseDto
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Action = permission.Action,
            Resource = permission.Resource,
            LastUpdatedAt = permission.LastUpdatedAt,
            LastUpdatedBy = permission.LastUpdatedBy,
            IsArchived = permission.IsArchived,
            LabelId = permission.LabelId,
            ProjectId = permission.ProjectId,
            OrganizationId = permission.OrganizationId,
            IsDefault = permission.IsDefault
        };
    }

    /// <summary>
    ///     Update an existing user-defined permission
    /// </summary>
    /// <param name="organizationId">ID of the Organization to which the permission resides.</param>
    /// <param name="projectId">ID of the Project to which the permission resides.</param>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="permissionId">ID of the permission to be updated</param>
    /// <param name="dto">New information on the permission</param>
    /// <returns>The newly updated permission</returns>
    /// <exception cref="KeyNotFoundException">Returned if the permission is not found or is uneditable</exception>
    public async Task<PermissionResponseDto> UpdatePermission(long organizationId, long? projectId, long currentUserId, long permissionId,
        UpdatePermissionRequestDto dto)
    {
        var permission = await _context.Permissions
            .Where(p => 
                p.Id == permissionId && 
                (!projectId.HasValue || p.ProjectId == projectId) && 
                p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        
        // ensure that default permissions cannot be edited
        if (permission == null || permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found");
        if (permission.IsDefault)
            throw new KeyNotFoundException($"Permission with id {permissionId} cannot be updated");

        permission.Name = dto.Name ?? permission.Name;
        permission.Description = dto.Description ?? permission.Description;
        permission.LabelId = dto.LabelId ?? permission.LabelId;
        permission.Action = dto.Action ?? permission.Action;
        permission.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        permission.LastUpdatedBy = currentUserId;

        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync();

        // Log update Permission event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "permission",
                EntityId = permission.Id,
                EntityName = permission.Name,
                Properties = JsonSerializer.Serialize(new { permission.Name })
            });

        return new PermissionResponseDto
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Action = permission.Action,
            Resource = permission.Resource,
            LastUpdatedAt = permission.LastUpdatedAt,
            LastUpdatedBy = permission.LastUpdatedBy,
            IsArchived = permission.IsArchived,
            LabelId = permission.LabelId,
            ProjectId = permission.ProjectId,
            OrganizationId = permission.OrganizationId,
            IsDefault = permission.IsDefault
        };
    }

    /// <summary>
    ///     Archive a permission
    /// </summary>
    /// <param name="organizationId">ID of the Organization to which the permission resides.</param>
    /// <param name="projectId">ID of the Project to which the permission resides.</param>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="permissionId">The ID of the permission to be archived</param>
    /// <returns>Boolean true upon success</returns>
    /// <exception cref="KeyNotFoundException">Returned if the permission is not found or is uneditable</exception>
    public async Task<bool> ArchivePermission(long organizationId, long? projectId, long currentUserId, long permissionId)
    {
        var permission = await _context.Permissions
            .Where(p => 
                p.Id == permissionId && 
                (!projectId.HasValue || p.ProjectId == projectId) && 
                p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
      
        if (permission == null || permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found or is already archived");
        if (permission.IsDefault)
            throw new KeyNotFoundException($"Permission with id {permissionId} cannot be updated");

        permission.IsArchived = true;
        permission.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        permission.LastUpdatedBy = currentUserId;
        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync();

        // Log archive Permission event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "permission",
            EntityId = permission.Id,
            EntityName = permission.Name,
            Properties = JsonSerializer.Serialize(new { permission.Name })
        });

        return true;
    }

    /// <summary>
    ///     Unarchive a permission
    /// </summary>
    /// <param name="organizationId">ID of the Organization to which the permission resides.</param>
    /// <param name="projectId">ID of the Project to which the permission resides.</param>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="permissionId">The ID of the permission to be unarchived</param>
    /// <returns>Boolean true upon success</returns>
    /// <exception cref="KeyNotFoundException">Returned if the permission is not found or is uneditable</exception>
    public async Task<bool> UnarchivePermission(long organizationId, long? projectId, long currentUserId, long permissionId)
    {
        var permission = await _context.Permissions
            .Where(p => 
                p.Id == permissionId && 
                (!projectId.HasValue || p.ProjectId == projectId) && 
                p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        
        if (permission != null && permission.IsDefault)
            throw new KeyNotFoundException($"Permission with id {permissionId} cannot be updated");
        if (permission == null || !permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found or is not archived");

        permission.IsArchived = false;
        permission.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        permission.LastUpdatedBy = currentUserId;
        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync();

        // Log unarchive Permission event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "unarchive",
            EntityType = "permission",
            EntityId = permission.Id,
            EntityName = permission.Name,
            Properties = JsonSerializer.Serialize(new { permission.Name })
        });

        return true;
    }

    /// <summary>
    ///     Delete a permission
    /// </summary>
    /// <param name="organizationId">ID of the Organization to which the permission resides.</param>
    /// <param name="projectId">ID of the Project to which the permission resides.</param>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="permissionId">The ID of the permission to be deleted</param>
    /// <returns>Boolean true upon success</returns>
    /// <exception cref="KeyNotFoundException">Returned if the permission is not found or is uneditable</exception>
    public async Task<bool> DeletePermission(long organizationId, long? projectId, long currentUserId, long permissionId)
    {
        var permission = await _context.Permissions
            .Where(p => 
                p.Id == permissionId && 
                (!projectId.HasValue || p.ProjectId == projectId) && 
                p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        
        if (permission == null || permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found");
        if (permission.IsDefault)
            throw new KeyNotFoundException($"Permission with id {permissionId} cannot be deleted");

        _context.Permissions.Remove(permission);
        await _context.SaveChangesAsync();

        // Log delete Permission event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "permission",
            EntityId = permission.Id,
            EntityName = permission.Name,
            Properties = JsonSerializer.Serialize(new { permission.Name })
        });

        return true;
    }
}