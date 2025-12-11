using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class RoleBusiness : IRoleBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for role operations</param>
    /// <param name="eventBusiness">Used for logging events during CRUD operations</param>
    public RoleBusiness(DeeplynxContext context, IEventBusiness eventBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     Get all roles for a given organization and optionally filter by project
    /// </summary>
    /// <param name="organizationId">(Required) ID of the organization</param>
    /// <param name="projectId">(Optional) ID of the project to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived roles</param>
    /// <returns>A list of roles</returns>
    public async Task<IEnumerable<RoleResponseDto>> GetAllRoles(
        long organizationId, long? projectId, bool hideArchived = true)
    {
        var roleQuery = _context.Roles
            .Where(r => r.OrganizationId == organizationId
                        && (!hideArchived || !r.IsArchived)
                        && (!projectId.HasValue || r.ProjectId == projectId.Value));

        if (!projectId.HasValue)
            roleQuery = roleQuery.Where(r => r.ProjectId == null);

        if (roleQuery == null)
            throw new KeyNotFoundException(
                "Roles not found or do not belong to the specified organization/project context");

        return await roleQuery.Select(r => new RoleResponseDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                LastUpdatedAt = r.LastUpdatedAt,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
                ProjectId = r.ProjectId,
                OrganizationId = r.OrganizationId
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Get a role by ID
    /// </summary>
    /// <param name="roleId">ID of the role to retrieve</param>
    /// <param name="organizationId">(Required) ID of the organization</param>
    /// <param name="projectId">(Optional) ID of the project to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to search archived roles</param>
    /// <returns>The requested role</returns>
    /// <exception cref="KeyNotFoundException">Thrown if role not found</exception>
    public async Task<RoleResponseDto> GetRole(long roleId, long organizationId, long? projectId,
        bool hideArchived = true)
    {
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && (!hideArchived || !r.IsArchived)
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        return new RoleResponseDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            LastUpdatedAt = role.LastUpdatedAt,
            LastUpdatedBy = role.LastUpdatedBy,
            IsArchived = role.IsArchived,
            ProjectId = role.ProjectId,
            OrganizationId = role.OrganizationId
        };
    }

    /// <summary>
    ///     Create a new role
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="dto">Data Transfer Object containing new role information</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>The newly created role</returns>
    /// <exception cref="ArgumentException">Returned for invalid project/org pair</exception>
    public async Task<RoleResponseDto> CreateRole(
        long currentUserId, CreateRoleRequestDto dto, long organizationId, long? projectId = null)
    {
        ValidationHelper.ValidateModel(dto);

        var role = new Role
        {
            Name = dto.Name,
            Description = dto.Description,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            ProjectId = projectId,
            OrganizationId = organizationId
        };
        
        try
        {
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Unique constraint violation - determine which scope based on projectId
            var scope = projectId.HasValue ? "project" : "organization";
            throw new InvalidOperationException($"A role with the name '{dto.Name}' already exists in this {scope}");
        }
        catch (Exception ex)
        {
            // Catch-all for any other errors during role creation
            throw new InvalidOperationException($"An error occurred while creating the role: {ex.Message}", ex);
        }

        // Log create Role event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "role",
            EntityId = role.Id,
            EntityName = role.Name,
            Properties = JsonSerializer.Serialize(new { role.Name }),
        };


        if (role.ProjectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, eventLog);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
        }
        
        await _context.SaveChangesAsync();
        
        return new RoleResponseDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            LastUpdatedAt = role.LastUpdatedAt,
            LastUpdatedBy = role.LastUpdatedBy,
            IsArchived = role.IsArchived,
            ProjectId = role.ProjectId,
            OrganizationId = role.OrganizationId
        };
    }

    /// <summary>
    ///     Upsert multiple roles at a time
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">(Required) Role organization</param>
    /// <param name="projectId">(Optional) Role project</param>
    /// <param name="roles"></param>
    /// <returns>List of created roles</returns>
    public async Task<List<RoleResponseDto>> BulkCreateRoles(long currentUserId, long organizationId, long? projectId,
        List<CreateRoleRequestDto> roles)
    {
        // There may be a better way to handle this, but let's avoid touching DB unless we have roles supplied
        if (roles == null || roles.Count == 0) return new List<RoleResponseDto>();

        // These checks aren't handled in middleware as Bulk operation is not accessible via routing
        await ExistenceHelper.EnsureOrganizationExistsAsync(_context, organizationId);

        // If projectId is provided, ensure it exists and belongs to the organization
        if (projectId.HasValue)
        {
            await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId.Value);

            var project = await _context.Projects.FindAsync(projectId.Value);
            if (project?.OrganizationId != organizationId)
                throw new ArgumentException(
                    $"Project {projectId.Value} does not belong to organization {organizationId}");
        }

        // Use different SQL based on whether it's org-level or project-level role
        var sql = projectId.HasValue
            ? @"
                INSERT INTO deeplynx.roles (
                    project_id, organization_id, name, description, last_updated_at, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL
                DO UPDATE SET
                    description = COALESCE(EXCLUDED.description, roles.description),
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy
                RETURNING id, project_id, organization_id, name, description, 
                    last_updated_at, is_archived, last_updated_by;"
            : @"
                INSERT INTO deeplynx.roles (
                    project_id, organization_id, name, description, last_updated_at, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, name) WHERE project_id IS NULL
                DO UPDATE SET
                    description = COALESCE(EXCLUDED.description, roles.description),
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy
                RETURNING id, project_id, organization_id, name, description, 
                    last_updated_at, is_archived, last_updated_by;";

        // establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@projectId", projectId.HasValue ? projectId.Value : DBNull.Value),
            new("@organizationId", organizationId),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new("@lastUpdatedBy", currentUserId)
        };

        // establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(roles.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_name", dto.Name),
            new NpgsqlParameter($"@p{i}_desc", (object?)dto.Description ?? DBNull.Value)
        }));

        // stringify the params and comma separate them
        var valueTuples = string.Join(", ", roles.Select((dto, i) =>
            $"(@projectId, @organizationId, @p{i}_name, @p{i}_desc, @now, @lastUpdatedBy)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // returns the resulting upserted classes
        var result = await _context.Database
            .SqlQueryRaw<RoleResponseDto>(sql, parameters.ToArray())
            .ToListAsync();
        
        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "role"
        };
        
        if (projectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, createEvent, result.Count);
        }
        
        return result;
    }

    /// <summary>
    ///     Update role information
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="roleId">ID of the role to be updated</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <param name="dto">Data Transfer Object containing new role information</param>
    /// <returns>The newly updated role</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found</exception>
    public async Task<RoleResponseDto> UpdateRole(long currentUserId, long roleId, long organizationId, long? projectId,
        UpdateRoleRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // Update fields
        role.Name = dto.Name ?? role.Name;
        role.Description = dto.Description ?? role.Description;
        role.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        role.LastUpdatedBy = currentUserId;

        try
        {
            _context.Roles.Update(role);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Unique constraint violation - name conflict
            var scope = role.ProjectId.HasValue ? "project" : "organization";
            throw new InvalidOperationException(
                $"A role with the name '{dto.Name ?? role.Name}' already exists in this {scope}");
        }
        catch (Exception ex)
        {
            // Catch-all for any other errors during role update
            throw new InvalidOperationException($"An error occurred while updating the role: {ex.Message}", ex);
        }

        // Log update Role event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "update",
            EntityType = "role",
            EntityName = role.Name,
            EntityId = role.Id,
            Properties = JsonSerializer.Serialize(new { role.Name }),
        };

        if (projectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId.Value, eventLog);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
        }
        
        return new RoleResponseDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            LastUpdatedAt = role.LastUpdatedAt,
            LastUpdatedBy = role.LastUpdatedBy,
            IsArchived = role.IsArchived,
            ProjectId = role.ProjectId,
            OrganizationId = role.OrganizationId
        };
    }

    /// <summary>
    ///     Archive a role by ID. Remove role from downstream project members
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="roleId">ID of role to archive</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found or is already archived</exception>
    /// <exception cref="DependencyDeletionException">Returned if role removal from project members fails</exception>
    public async Task<bool> ArchiveRole(long currentUserId, long roleId, long organizationId, long? projectId)
    {
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run archive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the archive role procedure, which archives this role and
                // removes this role from anyone holding it in any projects
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.archive_role({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE, {2}::INTEGER)",
                    roleId, lastUpdatedAt, currentUserId
                );

                if (archived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"Unable to archive role {roleId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"Unable to archive role {roleId} or its downstream dependents: {exc}");
            }
        }

        // Log archive Role event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "role",
            EntityId = role.Id,
            EntityName = role.Name,
            Properties = JsonSerializer.Serialize(new { role.Name }),
        };
        
        if (projectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId.Value, eventLog);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
        }

        return true;
    }

    /// <summary>
    ///     Unarchive a role by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="roleId">ID of role to unarchive</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found or is not archived</exception>
    public async Task<bool> UnarchiveRole(long currentUserId, long roleId, long organizationId, long? projectId)
    {
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        role.IsArchived = false;
        role.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        role.LastUpdatedBy = currentUserId;
        _context.Roles.Update(role);
        await _context.SaveChangesAsync();

        // Log unarchive Role event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "unarchive",
            EntityType = "role",
            EntityId = role.Id,
            EntityName = role.Name,
            Properties = JsonSerializer.Serialize(new { role.Name }),
        };
        
        if (projectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId.Value, eventLog);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
        }

        return true;
    }

    /// <summary>
    ///     Delete a role by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="roleId">ID of role to delete</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found</exception>
    public async Task<bool> DeleteRole(long currentUserId, long roleId, long organizationId, long? projectId)
    {
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        var roleName = role.Name;

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        // Log archive Role event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "role",
            EntityName = role.Name,
            EntityId = role.Id,
            Properties = JsonSerializer.Serialize(new { role.Name }),
        };
        
        if (projectId.HasValue)
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId.Value, eventLog);
        }
        else
        {
            await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
        }

        return true;
    }

    /// <summary>
    ///     List all permissions for a given role
    /// </summary>
    /// <param name="roleId">ID of the role across which to search permissions</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>A list of permissions</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found</exception>
    public async Task<IEnumerable<PermissionResponseDto>> GetPermissionsByRole(long roleId, long organizationId,
        long? projectId)
    {
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        return role.Permissions.Select(p => new PermissionResponseDto
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
        });
    }

    /// <summary>
    ///     Add a permission to a role
    /// </summary>
    /// <param name="roleId">ID of the role to add permission to</param>
    /// <param name="permissionId">ID of the permission to add</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if role or permission not found</exception>
    /// <exception cref="InvalidOperationException">Returned if permission already exists for role</exception>
    public async Task<bool> AddPermissionToRole(long roleId, long permissionId, long organizationId, long? projectId)
    {
        // check if role exists
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // check if permission exists
        var permission = await _context.Permissions.FindAsync(permissionId);
        if (permission == null || permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} not found");

        // check if permission is already assigned to the role
        if (role.Permissions.Any(p => p.Id == permission.Id))
            throw new ArgumentException($"Permission with id {permissionId} already exists as part of role {roleId}");

        role.Permissions.Add(permission);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Remove a permission from a role
    /// </summary>
    /// <param name="roleId">ID of the role to remove permission from</param>
    /// <param name="permissionId">ID of the permission to remove</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found or permission not assigned to role</exception>
    public async Task<bool> RemovePermissionFromRole(long roleId, long permissionId, long organizationId,
        long? projectId)
    {
        // check if role exists
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // check if permission exists on role
        var permission = role.Permissions.FirstOrDefault(p => p.Id == permissionId);
        if (permission == null || permission.IsArchived)
            throw new KeyNotFoundException($"Permission with id {permissionId} is not assigned to role {roleId}");

        role.Permissions.Remove(permission);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Set all permissions for a role (replaces existing permissions)
    /// </summary>
    /// <param name="roleId">ID of the role to update permissions for</param>
    /// <param name="permissionIds">Array of permission IDs to assign to the role</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found or any permission ID is invalid</exception>
    public async Task<bool> SetPermissionsForRole(long roleId, long[] permissionIds, long organizationId,
        long? projectId)
    {
        // check if role exists
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // validate that all permissions IDs exist
        var permissions = await _context.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .ToListAsync();

        if (permissions.Count != permissionIds.Length)
        {
            var missingIds = permissionIds.Except(permissions.Select(p => p.Id).ToList());
            throw new KeyNotFoundException($"Permissions not found: {string.Join(", ", missingIds)}");
        }

        // clear existing permissions and add new ones
        role.Permissions.Clear();
        foreach (var permission in permissions)
            role.Permissions.Add(permission);

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Set all permissions for a role by pattern
    /// </summary>
    /// <param name="roleId">ID of the role to update permissions for</param>
    /// <param name="permissionPatterns">Dictionary of resource: action[] permission patterns</param>
    /// <param name="organizationId">(Required) ID of the organization to which the role belongs</param>
    /// <param name="projectId">(Optional) ID of the project to which the role belongs</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if role not found</exception>
    public async Task<bool> SetPermissionsByPattern(long roleId, Dictionary<string, string[]> permissionPatterns,
        long organizationId, long? projectId)
    {
        // check if role exists
        var role = await _context.Roles
            .Where(r => r.Id == roleId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && (!projectId.HasValue || r.ProjectId == projectId.Value))
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync();

        if (role == null)
            throw new KeyNotFoundException(
                $"Role with id {roleId} not found or does not belong to the specified organization/project context");

        // get the list of resources we're interested in
        var resources = permissionPatterns.Keys.ToList();

        // fetch all permissions for these resources
        var allPermissions = await _context.Permissions
            .Where(p => resources.Contains(p.Resource))
            .ToListAsync();

        // filter in memory to match the exact actions
        var matchingPermissions = allPermissions
            .Where(p => p.IsDefault &&
                        permissionPatterns.ContainsKey(p.Resource) &&
                        permissionPatterns[p.Resource].Contains(p.Action))
            .ToList();

        // clear existing permissions and add new ones
        role.Permissions.Clear();
        foreach (var permission in matchingPermissions)
            role.Permissions.Add(permission);

        await _context.SaveChangesAsync();
        return true;
    }
}