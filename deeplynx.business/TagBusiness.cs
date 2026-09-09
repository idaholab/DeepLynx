using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class TagBusiness : ITagBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly IProjectRolePermissionService _projectRolePermissionService;
    private readonly IAdminService _adminService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TagBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for tag operations.</param>
    /// <param name="eventBusiness">Used to access event operations</param>
    /// <param name="projectRolePermissionService">Used to get permissions allowed for a user</param>
    /// <param name="adminService">Used to check level the user is</param>
    public TagBusiness(DeeplynxContext context, IEventBusiness eventBusiness, IProjectRolePermissionService projectRolePermissionService,
        IAdminService adminService)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _projectRolePermissionService = projectRolePermissionService;
        _adminService = adminService;

    }

    /// <summary>
    ///     [DEPRECATED - V1 ONLY] Retrieves all tags without pagination.
    ///     Superseded by <see cref="GetAllTagsPaginated"/>. Do not call this from new controller versions;
    ///     it exists solely to back the deprecated v1 tags controllers and should be deleted once
    ///     those v1 endpoints are sunset.
    /// </summary>
    /// <param name="currentUserId">The ID of the current user for which the data source belongs to</param>
    /// <param name="projectIds">The IDs of the project whose tags are to be retrieved.</param>
    /// <param name="organizationId">The ID of the organization whose tags are to be retrieved.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result</param>
    /// <param name="isSysAdmin">Flag indicating whether you are a system admin or not</param>
    /// <param name="isOrgAdmin">Flag indicating whether you are an organization admin or not</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [Obsolete("V1-only. Used by deprecated v1 tag endpoints. Superseded by GetAllTagsPaginated. " +
              "Remove once v1 tag endpoints are sunset.", error: false)]
    public async Task<List<TagResponseDto>> GetAllTags(
        long currentUserId,
        long organizationId,
        long[]? projectIds,
        bool hideArchived,
        bool isSysAdmin = false,
        bool isOrgAdmin = false)
    {
        var userProjectAdminStatus = new Dictionary<long, bool>();

        if (projectIds?.Length > 0)
        {
            var adminProjectIds = await _context.ProjectMembers
                .Where(pm =>
                    pm.IsProjectAdmin &&
                    projectIds.Contains(pm.ProjectId) &&
                    (
                    (
                        (pm.UserId != null && pm.UserId == currentUserId) ||
                        pm.Group!.Users.Any(u => u.Id == currentUserId)
                    )
                    ))
                    .Select(pm => pm.ProjectId)
                    .Distinct()
                    .ToHashSetAsync();

            foreach (var projectId in projectIds)
            {
                userProjectAdminStatus[projectId] = adminProjectIds.Contains(projectId);
            }
        }

        var authorizedProjectIds = new List<long>();
        foreach (var projectId in projectIds ?? [])
        {
            if (isSysAdmin || isOrgAdmin || userProjectAdminStatus.GetValueOrDefault(projectId, false))
            {
                authorizedProjectIds.Add(projectId);
                continue;
            }

            var hasPermission = await _projectRolePermissionService.PermissionInProject(
                currentUserId, projectId, "read", "tag");

            if (hasPermission)
            {
                authorizedProjectIds.Add(projectId);
            }
        }

        if (projectIds != null && authorizedProjectIds.Count == 0)
        {
            return [];
        }

        var tagQuery = _context.Tags
            .Where(t => t.OrganizationId == organizationId && (!hideArchived || !t.IsArchived));

        if (authorizedProjectIds.Count > 0)
        {
            tagQuery = tagQuery.Where(t =>
                (t.ProjectId.HasValue && authorizedProjectIds.Contains(t.ProjectId.Value)) || t.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        return await tagQuery.Select(t => new TagResponseDto
        {
            Id = t.Id,
            Name = t.Name,
            ProjectId = t.ProjectId,
            LastUpdatedBy = t.LastUpdatedBy,
            LastUpdatedAt = t.LastUpdatedAt,
            OrganizationId = t.OrganizationId,
            IsArchived = t.IsArchived
        }).ToListAsync();
    }

    /// <summary>
    ///     Retrieves all tags for a specified project with pagination
    /// </summary>
    /// <param name="currentUserId">The ID of the current user for which the data source belongs to</param>
    /// <param name="paginatedRequestDto">Pagination parameters; if PageSize == -1, returns all matching organizations</param>
    /// <param name="projectIds">The IDs of the project whose tags are to be retrieved.</param>
    /// <param name="organizationId">The ID of the organization whose tags are to be retrieved.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result</param>
    /// <param name="isSysAdmin">Flag indicating whether you are a system admin or not</param>
    /// <param name="isOrgAdmin">Flag indicating whether you are an organization admin or not</param>
    /// <returns>A paginated list of organizations</returns>
    public async Task<PaginatedResponse<TagResponseDto>> GetAllTagsPaginated(
        long currentUserId,
        long organizationId,
        long[]? projectIds,
        PaginatedRequestDto paginatedRequestDto,
        bool hideArchived = true,
        bool isSysAdmin = false,
        bool isOrgAdmin = false)
    {
        var userProjectAdminStatus = new Dictionary<long, bool>();

        if (projectIds?.Length > 0)
        {
            var adminProjectIds = await _context.ProjectMembers
                .Where(pm =>
                    pm.IsProjectAdmin &&
                    projectIds.Contains(pm.ProjectId) &&
                    (
                        (pm.UserId != null && pm.UserId == currentUserId) ||
                        pm.Group!.Users.Any(u => u.Id == currentUserId)
                    ))
                .Select(pm => pm.ProjectId)
                .Distinct()
                .ToHashSetAsync();

            foreach (var projectId in projectIds)
            {
                userProjectAdminStatus[projectId] = adminProjectIds.Contains(projectId);
            }
        }

        var authorizedProjectIds = new List<long>();
        foreach (var projectId in projectIds ?? [])
        {
            if (isSysAdmin || isOrgAdmin || userProjectAdminStatus.GetValueOrDefault(projectId, false))
            {
                authorizedProjectIds.Add(projectId);
                continue;
            }

            var hasPermission = await _projectRolePermissionService.PermissionInProject(
                currentUserId, projectId, "read", "tag");

            if (hasPermission)
            {
                authorizedProjectIds.Add(projectId);
            }
        }

        if (projectIds != null && projectIds.Length > 0 && authorizedProjectIds.Count == 0)
        {
            return new PaginatedResponse<TagResponseDto>
            {
                Items = [],
                PageNumber = paginatedRequestDto.PageNumber,
                PageSize = paginatedRequestDto.PageSize,
                TotalCount = 0
            };
        }

        var tagQuery = _context.Tags
            .Where(t => t.OrganizationId == organizationId && (!hideArchived || !t.IsArchived));

        if (authorizedProjectIds.Count > 0)
        {
            tagQuery = tagQuery.Where(t =>
                (t.ProjectId.HasValue && authorizedProjectIds.Contains(t.ProjectId.Value)) || t.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        tagQuery = tagQuery.OrderBy(t => t.Id);

        return await tagQuery.Select(g => TagsToResponse(g)).ToPaginatedAsync(paginatedRequestDto);
    }

    /// <summary>
    ///     Retrieves a specific tag by its ID for a specified project.
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagId">The ID of the tag to retrieve.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result</param>
    /// <returns>The tag with its details.</returns>
    /// <exception cref="KeyNotFoundException">Returned if tag not found or is archived</exception>
    public async Task<TagResponseDto> GetTag(long organizationId, long? projectId, long tagId, bool hideArchived)
    {
        var tagQuery = _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId);
        if (projectId.HasValue)
        {
            // project tags and org tags
            tagQuery = tagQuery.Where(t => t.ProjectId == projectId || t.ProjectId == null);
        }
        else
        {   // just org tags
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        var tag = await tagQuery.FirstOrDefaultAsync();

        if (tag == null) throw new KeyNotFoundException($"Tag with id {tagId} not found");

        if (hideArchived && tag.IsArchived)
            throw new KeyNotFoundException($"Tag with id {tagId} is archived");

        return new TagResponseDto
        {
            Id = tag.Id,
            Name = tag.Name,
            ProjectId = tag.ProjectId,
            LastUpdatedBy = tag.LastUpdatedBy,
            LastUpdatedAt = tag.LastUpdatedAt,
            IsArchived = tag.IsArchived,
            OrganizationId = tag.OrganizationId,
        };
    }

    /// <summary>
    ///     Asynchronously creates a new tag for a specified project.
    ///     Note: Will error out with foreign key constraint violation if project is not found.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="dto">The tag request data transfer object containing tag details.</param>
    /// <returns>The created tag response DTO with saved details.</returns>
    public async Task<TagResponseDto> CreateTag(long organizationId, long currentUserId, long? projectId,
        CreateTagRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Name == dto.Name);
        if (existingTag != null)
            throw new Exception($"Tag for project {projectId} with name {dto.Name} already exists");

        // Validate 'Name' field
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("Name is required and cannot be empty or whitespace");

        var tag = new Tag
        {
            Name = dto.Name,
            ProjectId = projectId,
            LastUpdatedBy = currentUserId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId,
        };

        try
        {
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Unique constraint violation - determine which scope based on projectId
            var scope = projectId.HasValue ? "project" : "organization";
            throw new InvalidOperationException($"A tag with the name '{dto.Name}' already exists in this {scope}");
        }
        catch (Exception ex)
        {
            // Catch-all for any other errors during tag creation
            throw new InvalidOperationException($"An error occurred while creating the tag: {ex.Message}", ex);
        }

        // Log Tag Create Event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "tag",
                EntityId = tag.Id,
                EntityName = tag.Name,
                Properties = JsonSerializer.Serialize(new { tag.Name }),
                DataSourceId = null,
            }
        );

        return new TagResponseDto // Return validated response DTO back to user.
        {
            Id = tag.Id,
            Name = tag.Name,
            ProjectId = tag.ProjectId,
            LastUpdatedBy = tag.LastUpdatedBy,
            LastUpdatedAt = tag.LastUpdatedAt,
            OrganizationId = organizationId,
        };
    }

    /// <summary>
    ///     Asynchronously creates new tags for a specified project.
    ///     Note: Will error out with foreign key constraint violation if project is not found.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tags">The tag request data transfer object containing tag details.</param>
    /// <returns>The created tag response DTO with saved details.</returns>
    public async Task<List<TagResponseDto>> BulkCreateTags(
    long organizationId,
    long currentUserId,
    long? projectId,
    List<CreateTagRequestDto> tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return new List<TagResponseDto>();
        }

        // Deduplicate tags to prevent ON CONFLICT errors
        var distinctTags = tags
            .GroupBy(tag => new { tag.Name, ProjectId = projectId, OrganizationId = organizationId })
            .Select(group => group.First())
            .ToList();

        var sql = projectId.HasValue
            ? @"
        INSERT INTO deeplynx.tags (project_id, organization_id, name, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL
            DO UPDATE SET
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
            RETURNING id, project_id, organization_id, name, last_updated_at, is_archived, last_updated_by;"
            : @"
        INSERT INTO deeplynx.tags (project_id, organization_id, name, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, name) WHERE project_id IS NULL
            DO UPDATE SET
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
        RETURNING id, project_id, organization_id, name, last_updated_at, is_archived, last_updated_by;";

        var parameters = new List<NpgsqlParameter>
    {
        new NpgsqlParameter("@projectId", projectId.HasValue ? (object)projectId.Value : DBNull.Value),
        new NpgsqlParameter("@organizationId", organizationId),
        new NpgsqlParameter("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
        new NpgsqlParameter("@lastUpdatedBy", currentUserId)
    };

        parameters.AddRange(distinctTags.SelectMany((dto, i) => new[]
        {
        new NpgsqlParameter($"@p{i}_name", dto.Name)
    }));

        var valueTuples = string.Join(", ", distinctTags.Select((dto, i) =>
            $"(@projectId, @organizationId, @p{i}_name, @now, false, @lastUpdatedBy)"));

        sql = string.Format(sql, valueTuples);

        var result = await _context.Database
            .SqlQueryRaw<TagResponseDto>(sql, parameters.ToArray())
            .ToListAsync();

        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "tag",
        };

        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

        return result;
    }

    /// <summary>
    ///     Updates an existing tag for a specified project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagId">The ID of the tag to update.</param>
    /// <param name="tagRequestDto">The tag request data transfer object containing updated tag details.</param>
    /// <returns>The updated tag response DTO with its details.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the tag is not found.</exception>
    public async Task<TagResponseDto> UpdateTag(long organizationId, long currentUserId, long? projectId, long tagId,
        UpdateTagRequestDto tagRequestDto)
    {
        ValidationHelper.ValidateModel(tagRequestDto);

        var tagQuery = _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId
                        && !t.IsArchived);

        if (projectId.HasValue)
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == projectId.Value || r.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        var tag = await tagQuery.FirstOrDefaultAsync();

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} not found or does not belong to the specified organization/project context");

        // Organization roles cannot be updated from a project level
        if (projectId.HasValue && tag.ProjectId == null)
        {
            throw new InvalidOperationException("Organization tags cannot be updated from the child projects.");
        }

        // Validate 'Name' field
        if (string.IsNullOrWhiteSpace(tagRequestDto.Name))
            throw new ArgumentException("Name is required and cannot be empty.");

        tag.Name = tagRequestDto.Name ?? tag.Name;
        tag.LastUpdatedBy = currentUserId;
        tag.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        try
        {
            _context.Tags.Update(tag);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Unique constraint violation - name conflict
            var scope = tag.ProjectId.HasValue ? "project" : "organization";
            throw new InvalidOperationException(
                $"A tag with the name '{tagRequestDto.Name ?? tagRequestDto.Name}' already exists in this {scope}");
        }
        catch (Exception ex)
        {
            // Catch-all for any other errors during tag update
            throw new InvalidOperationException($"An error occurred while updating the tag: {ex.Message}", ex);
        }

        // Log tag update event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "update",
            EntityType = "tag",
            EntityId = tag.Id,
            EntityName = tag.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { tag.Name }),
        });

        return new TagResponseDto
        {
            Id = tag.Id,
            Name = tag.Name,
            ProjectId = tag.ProjectId,
            LastUpdatedBy = tag.LastUpdatedBy,
            LastUpdatedAt = tag.LastUpdatedAt,
            IsArchived = tag.IsArchived,
            OrganizationId = organizationId,
        };
    }

    /// <summary>
    ///     Deletes a specific tag by its ID for a specified project.
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagId">The ID of the tag to delete.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tag is not found.</exception>
    public async Task<bool> DeleteTag(long organizationId, long? projectId, long tagId)
    {
        var tagQuery = _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId
                        && !t.IsArchived);

        //if project id supplied, inherit org level roles 
        if (projectId.HasValue)
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == projectId.Value || r.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        var tag = await tagQuery.FirstOrDefaultAsync();

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} not found or does not belong to the specified organization/project context");

        // Organization tags cannot be updated from a project level
        if (projectId.HasValue && tag.ProjectId == null)
        {
            throw new InvalidOperationException("Organization tags cannot be updated from the child projects.");
        }

        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Archives a specific tag by its ID for a specified project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagId">The ID of the tag to archive.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tag is not found.</exception>
    public async Task<bool> ArchiveTag(long organizationId, long currentUserId, long? projectId, long tagId)
    {
        var tagQuery = _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId
                        && !t.IsArchived);

        //if project id supplied, inherit org level tags 
        if (projectId.HasValue)
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == projectId.Value || r.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == null);
        }

        var tag = await tagQuery.FirstOrDefaultAsync();

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} not found or does not belong to the specified organization/project context");

        // Organization tags cannot be updated from a project level
        if (projectId.HasValue && tag.ProjectId == null)
        {
            throw new InvalidOperationException("Organization tags cannot be updated from the child projects.");
        }

        tag.IsArchived = true;
        tag.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        tag.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log tag soft delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "tag",
            EntityId = tag.Id,
            EntityName = tag.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { tag.Name }),
        });

        return true;
    }

    /// <summary>
    ///     Unarchives a specific tag by its ID for a specified project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the tag belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagId">The ID of the tag to unarchive.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the tag is not found.</exception>
    public async Task<bool> UnarchiveTag(
        long organizationId,
        long currentUserId,
        long? projectId,
        long tagId)
    {
        var tagQuery = _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId
                        && t.IsArchived);
        //if project id supplied, inherit org level roles 
        if (projectId.HasValue)
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == projectId.Value || r.ProjectId == null);
        }
        else
        {
            tagQuery = tagQuery.Where(r => r.ProjectId == null);
        }

        var tag = await tagQuery.FirstOrDefaultAsync();

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} not found or does not belong to the specified organization/project context");

        // Organization tags cannot be updated from a project level
        if (projectId.HasValue && tag.ProjectId == null)
        {
            throw new InvalidOperationException("Organization tags cannot be updated from the child projects.");
        }

        tag.IsArchived = false;
        tag.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        tag.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "unarchive",
            EntityType = "tag",
            EntityId = tag.Id,
            EntityName = tag.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { tag.Name }),
        });

        return true;
    }

    /// <summary>
    ///     Validates that all provided tag names exist in the database for the specified project.
    ///     Used by MetadataBusiness to enforce tagsMutable settings.
    /// </summary>
    /// <param name="projectId">The project ID to search within</param>
    /// <param name="organizationId">The ID of the organization to which the tag belongs.</param>
    /// <param name="tagNames">List of tag names to validate</param>
    /// <returns>List of tags that were found</returns>
    /// <exception cref="KeyNotFoundException">Thrown if one or more tag names not found</exception>
    /// <exception cref="ArgumentException">Thrown if tagNames list is null or empty</exception>
    public async Task<List<TagResponseDto>> GetTagsByName(long organizationId, long? projectId, List<string> tagNames, bool hideArchived)
    {
        if (tagNames == null || !tagNames.Any())
            throw new ArgumentException("Tag names list cannot be null or empty", nameof(tagNames));

        // Remove duplicates and filter out null/empty values
        var cleanTagNames = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        if (!cleanTagNames.Any())
            throw new ArgumentException("No valid tag names provided", nameof(tagNames));

        // Query for existing tags (excluding archived)
        var tagQuery = _context.Tags.Where(t =>
            t.OrganizationId == organizationId &&
            (!hideArchived || !t.IsArchived) &&
            cleanTagNames.Contains(t.Name));

        if (projectId.HasValue)
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == projectId.Value);
        }
        else
        {
            tagQuery = tagQuery.Where(t => t.ProjectId == null);
        }

        var existingTags = await tagQuery.ToListAsync();
        var foundTagNames = existingTags.Select(t => t.Name).ToHashSet();
        var missingTagNames = cleanTagNames.Where(name => !foundTagNames.Contains(name)).ToList();

        if (missingTagNames.Any())
            throw new KeyNotFoundException(
                $"Tags not found with names: {string.Join(", ", missingTagNames)}");
        return existingTags.Select(t => new TagResponseDto
        {
            Id = t.Id,
            Name = t.Name,
            ProjectId = t.ProjectId,
            LastUpdatedBy = t.LastUpdatedBy,
            LastUpdatedAt = t.LastUpdatedAt,
            IsArchived = t.IsArchived,
            OrganizationId = organizationId,
        }).ToList();
    }

    private static TagResponseDto TagsToResponse(Tag t)
    {
        return new TagResponseDto
        {
            Id = t.Id,
            Name = t.Name,
            ProjectId = t.ProjectId,
            LastUpdatedBy = t.LastUpdatedBy,
            LastUpdatedAt = t.LastUpdatedAt,
            IsArchived = t.IsArchived,
            OrganizationId = t.OrganizationId,
        };
    }
}