using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class SensitivityLabelBusiness : ISensitivityLabelBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly IUserBusiness _userBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for sensitivity label operations</param>
    /// <param name="eventBusiness">Used for logging events during CRUD operations</param>
    /// <param name="userBusiness">Used to get the user's current Admin info</param>
    public SensitivityLabelBusiness(DeeplynxContext context, IEventBusiness eventBusiness,IUserBusiness userBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _userBusiness = userBusiness;
    }
    
     /// <summary>
    ///     Get all sensitivity labels for a given project and/or organization
    /// </summary>
    /// <param name="currentUserId">Id of the user executing this method.</param>
    /// <param name="projectIds">ID of the project across which to search</param>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <param name="hideArchived">Flag indicating whether to search on archived labels</param>
    /// <returns>A list of labels</returns>
    public async Task<IEnumerable<SensitivityLabelResponseDto>> GetAllSensitivityLabels(
        long currentUserId, long[]? projectIds, long organizationId, bool hideArchived = true)
    {
        var labelService = new SensitivityLabelService(_context);
        var query = _context.SensitivityLabels
            .Where(t => t.OrganizationId == organizationId
                        && (!hideArchived || !t.IsArchived));
        
        if (projectIds is { Length: > 0 })
        {
            
            query = query.Where(c =>
                (c.ProjectId.HasValue && projectIds.Contains(c.ProjectId.Value)) || c.ProjectId == null);
        }
        else
        {  
            query = query.Where(c => c.ProjectId == null);
        }

        // User Information
        var user = await _userBusiness.GetUserAdminInfo(currentUserId, organizationId);

        // Org and System admins can see everything
        if (user.IsSysAdmin != true && user.IsOrgAdmin != true) {
            // List of projects user is an admin for
            var adminProjects = new List<long?>(); 
            foreach (var projectId in projectIds ?? Enumerable.Empty<long>())
            {
                var projUser = await _userBusiness.GetUserAdminInfo(currentUserId, organizationId, projectId);
                if (projUser.IsProjectAdmin == true) adminProjects.Add(projectId);
            }

            var authorizedLabels = await labelService.GetAuthorizedSensitivityLabels(currentUserId, organizationId, projectIds, "read record");
            query = query.Where(c => 
                // all labels on a project level that the user admins
                (c.ProjectId.HasValue && adminProjects.Contains(c.ProjectId)) ||
                // org labels, inherited into the project
                (c.ProjectId == null && adminProjects.Count > 0) ||
                // labels the user was granted permission to
                authorizedLabels.Contains(c.Id)
            );
        }

        return await query.Select(l => new SensitivityLabelResponseDto
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                LastUpdatedAt = l.LastUpdatedAt,
                LastUpdatedBy = l.LastUpdatedBy,
                ProjectId = l.ProjectId,
                OrganizationId = l.OrganizationId,
                IsArchived = l.IsArchived
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Get a sensitivity label by ID
    /// </summary>
    /// <param name="labelId">ID of the label to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to search archived labels</param>
    /// <param name="projectId">ID of the project across which to search</param>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <returns>The requested label</returns>
    /// <exception cref="KeyNotFoundException">Thrown if label not found</exception>
    public async Task<SensitivityLabelResponseDto> GetSensitivityLabel(long labelId, long? projectId,
        long organizationId, bool hideArchived = true)
    {
        var query = _context.SensitivityLabels
            .Where(t => t.Id == labelId
                        && t.OrganizationId == organizationId);
        if (projectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == projectId || t.ProjectId == null);
        }
        else
        {   
            query = query.Where(t => t.ProjectId == null);
        }

        var label = await query.FirstOrDefaultAsync();
        
        if (label == null)
            throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found");

        if (hideArchived && label.IsArchived)
            throw new KeyNotFoundException($"Sensitivity label with id {labelId} is archived");

        return new SensitivityLabelResponseDto
        {
            Id = label.Id,
            Name = label.Name,
            Description = label.Description,
            LastUpdatedAt = label.LastUpdatedAt,
            LastUpdatedBy = label.LastUpdatedBy,
            IsArchived = label.IsArchived,
            ProjectId = label.ProjectId,
            OrganizationId = label.OrganizationId
        };
    }
    
    /// <summary>
    ///     Create a new sensitivity label
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="dto">Data Transfer Object containing new label information</param>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <returns>The newly created label</returns>
    /// <exception cref="ArgumentException">Returned if project/org both supplied or no project/org supplied</exception>
    public async Task<SensitivityLabelResponseDto> CreateSensitivityLabel(
    long currentUserId, CreateSensitivityLabelRequestDto dto, long? projectId, long organizationId)
    {
        ValidationHelper.ValidateModel(dto);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            
            var label = new SensitivityLabel
            {
                Name = dto.Name,
                Description = dto.Description,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ProjectId = projectId,
                OrganizationId = organizationId
            };

            _context.SensitivityLabels.Add(label);
            await _context.SaveChangesAsync();

            var permissionActions = new[]
            {
                ("read", "record", "Permission to read {0} labeled records"),
                ("write", "record", "Permission to add records with label {0}"),
                ("update", "record", "Permission to update {0} labeled records"),
                ("delete", "record", "Permission to delete {0} labeled records"),
                ("download", "file", "Permission to download {0} labeled files"),
                ("upload", "file", "Permission to upload {0} labeled files"),
                ("update", "file", "Permission to update {0} labeled files"),
                ("delete", "file", "Permission to delete {0} labeled files")
            };

            var permissions = permissionActions.Select(p => new Permission
            {
                Name = dto.Name,
                Description = string.Format(p.Item3, dto.Name),
                Action = $"{p.Item1} {p.Item2}",
                LabelId = label.Id,
                IsDefault = false,
                ProjectId = projectId,
                OrganizationId = organizationId,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId
            }).ToList();

            await _context.AddRangeAsync(permissions);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Log create SensitivityLabel event (outside transaction)
            var eventLog = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name })
            };

            await _eventBusiness.CreateEvent(currentUserId, organizationId, label.ProjectId, eventLog);

            return new SensitivityLabelResponseDto
            {
                Id = label.Id,
                Name = label.Name,
                Description = label.Description,
                LastUpdatedAt = label.LastUpdatedAt,
                LastUpdatedBy = label.LastUpdatedBy,
                IsArchived = label.IsArchived,
                ProjectId = label.ProjectId,
                OrganizationId = label.OrganizationId
            };
        }
        catch (Exception)
        {
            // Only rollback if transaction hasn't been committed
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Asynchronously creates new Sensitivity Labels for a specified project.
    ///     Note: Will error out with foreign key constraint violation if project is not found.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the label belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the label belongs.</param>
    /// <param name="labels">The label request data transfer object containing label details.</param>
    /// <returns>The created label response DTO with saved details.</returns>
    public async Task<List<SensitivityLabelResponseDto>> BulkCreateSensitivityLabels(
    long organizationId, long currentUserId, long? projectId, List<CreateSensitivityLabelRequestDto> labels)
    {
        if (labels == null || labels.Count == 0)
        {
            return new List<SensitivityLabelResponseDto>();
        }

        var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            
            // Bulk insert into sensitivity_labels; if there is a name collision, update the description and last_updated fields
            var sql = projectId.HasValue
                ? @"
                INSERT INTO deeplynx.sensitivity_labels (
                    organization_id, project_id, name, description,
                    last_updated_at, is_archived, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL
                DO UPDATE SET
                    description = COALESCE(EXCLUDED.description, sensitivity_labels.description),
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy,
                    is_archived = EXCLUDED.is_archived
                RETURNING id, project_id, organization_id, name, description,
                    last_updated_at, last_updated_by, is_archived;"
                : @"
                INSERT INTO deeplynx.sensitivity_labels (
                    organization_id, project_id, name, description,
                    last_updated_at, is_archived, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, name) WHERE project_id IS NULL
                DO UPDATE SET
                    description = COALESCE(EXCLUDED.description, sensitivity_labels.description),
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy,
                    is_archived = EXCLUDED.is_archived
                RETURNING id, project_id, organization_id, name, description,
                    last_updated_at, last_updated_by, is_archived;";

            // establish "constant" parameters
            var parameters = new List<NpgsqlParameter>
            {
                new("@organizationId", organizationId),
                new("@projectId", projectId.HasValue ? projectId.Value : DBNull.Value),
                new("@now", now),
                new("@lastUpdatedBy", currentUserId)
            };

            // establish "dynamic" parameters (new for each dto in the list)
            parameters.AddRange(labels.SelectMany((dto, i) => new[]
            {
                new NpgsqlParameter($"@p{i}_name", dto.Name),
                new NpgsqlParameter($"@p{i}_desc", (object?)dto.Description ?? DBNull.Value)
            }));

            // stringify the params and comma separate them
            var valueTuples = string.Join(", ", labels.Select((dto, i) =>
                $"(@organizationId, @projectId, @p{i}_name, @p{i}_desc, @now, false, @lastUpdatedBy)"));

            // put everything together and execute the query
            sql = string.Format(sql, valueTuples);

            // returns the resulting upserted labels
            var result = await _context.Database
                .SqlQueryRaw<SensitivityLabelResponseDto>(sql, parameters.ToArray())
                .ToListAsync();

            foreach (var label in result)
            {
                var permissionActions = new[]
                {
                    ("read", "record", "Permission to read {0} labeled records"),
                    ("write", "record", "Permission to add records with label {0}"),
                    ("update", "record", "Permission to update {0} labeled records"),
                    ("delete", "record", "Permission to delete {0} labeled records"),
                    ("download", "file", "Permission to download {0} labeled files"),
                    ("upload", "file", "Permission to upload {0} labeled files"),
                    ("update", "file", "Permission to update {0} labeled files"),
                    ("delete", "file", "Permission to delete {0} labeled files")
                };

                var permissions = permissionActions.Select(p => new Permission
                {
                    Name = label.Name,
                    Description = string.Format(p.Item3, label.Name),
                    Action = $"{p.Item1} {p.Item2}",
                    LabelId = label.Id,
                    IsDefault = false,
                    ProjectId = projectId,
                    OrganizationId = organizationId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId
                }).ToList();

                await _context.AddRangeAsync(permissions);
            }

            await _context.SaveChangesAsync();

            // Log create event
            var createEvent = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "sensitivity_label",
            };

            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

            await transaction.CommitAsync();

            return result;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Update sensitivity label information
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="labelId">ID of the label to be updated</param>
    /// <param name="projectId">ID of the project label belongs</param>
    /// <param name="organizationId">ID of the organization the label belongs</param>
    /// <param name="dto">Data Transfer Object containing new label information</param>
    /// <returns>The newly updated label</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<SensitivityLabelResponseDto> UpdateSensitivityLabel(
        long currentUserId, long labelId, long? projectId, long organizationId, UpdateSensitivityLabelRequestDto dto)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var query = _context.SensitivityLabels
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && !l.IsArchived);
            
            if (projectId.HasValue)
            {
                query = query.Where( r => r.ProjectId == projectId.Value || r.ProjectId == null);
            }
            else
            {
                query = query.Where(t => t.ProjectId == null);
            }

            var label = await query.FirstOrDefaultAsync();

            if (label == null)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found");
            
            if (projectId.HasValue && label.ProjectId == null)
            {
                throw new InvalidOperationException("Organization sensitivity labels cannot be updated from the child projects.");
            }

            label.Name = dto.Name ?? label.Name;
            label.Description = dto.Description ?? label.Description;
            label.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            label.LastUpdatedBy = currentUserId;

            _context.SensitivityLabels.Update(label);

            // Update Permissions Associated with Label
            var permissions = await _context.Permissions
                .Where(p => p.LabelId == labelId)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                permission.Name = dto.Name ?? permission.Name;

                if (dto.Description != null)
                {
                    // Update description based on action type
                    permission.Description = permission.Action switch
                    {
                        "read" => "Permission to read " + dto.Name + " labeled records",
                        "write" => "Permission to modify " + dto.Name + " labeled records",
                        _ => permission.Description // fallback
                    };
                }

                permission.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                permission.LastUpdatedBy = currentUserId;
            }

            _context.Permissions.UpdateRange(permissions);

            // Log update SensitivityLabel event
            var eventLog = new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name }),
            };

            if (label.ProjectId != null)
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, label.ProjectId, eventLog);
            }
            else
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return new SensitivityLabelResponseDto
            {
                Id = label.Id,
                Name = label.Name,
                Description = label.Description,
                LastUpdatedAt = label.LastUpdatedAt,
                LastUpdatedBy = label.LastUpdatedBy,
                IsArchived = label.IsArchived,
                ProjectId = label.ProjectId,
                OrganizationId = label.OrganizationId
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    /// <summary>
    ///     Delete a sensitivity label by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="labelId">ID of label to delete</param>
    /// <param name="projectId">ID of the project label belongs</param>
    /// <param name="organizationId">ID of the organization the label belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found</exception>
    public async Task<bool> DeleteSensitivityLabel(long currentUserId, long labelId, long? projectId,
        long organizationId)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var query = _context.SensitivityLabels
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && !l.IsArchived);

            if (projectId.HasValue)
            {
                query = query.Where( r => r.ProjectId == projectId.Value || r.ProjectId == null);
            }
            else
            {
                query = query.Where(t => t.ProjectId == null);
            }

            var label = await query.FirstOrDefaultAsync();

            if (label == null)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");
            
            if (projectId.HasValue && label.ProjectId == null)
            {
                throw new InvalidOperationException("Organization sensitivity labels cannot be updated from the child projects.");
            }

            // Do not delete if there are records currently using this Label
            var recordCount = await _context.Records
                .Where(r => r.Labels.Any(l => l.Id == labelId))
                .CountAsync();

            if (recordCount > 0)
            {
                throw new Exception(
                    $"Cannot delete. Sensitivity label with id {labelId} is used on {recordCount} records.");
            }

            // Remove the permissions associated with the label
            await _context.Permissions
                .Where(p => p.LabelId == labelId)
                .ExecuteDeleteAsync();

            // Remove the label
            _context.SensitivityLabels.Remove(label);
            await _context.SaveChangesAsync();

            // Log delete SensitivityLabel event
            var eventLog = new CreateEventRequestDto
            {
                Operation = "delete",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name }),
            };

            if (label.ProjectId != null)
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, label.ProjectId, eventLog);
            }
            else
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
            }

            await transaction.CommitAsync();

            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Archive a sensitivity label by ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="labelId">ID of label to archive</param>
    /// <param name="projectId">ID of the project label belongs</param>
    /// <param name="organizationId">ID of the organization the label belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or is already archived</exception>
    public async Task<bool> ArchiveSensitivityLabel(long currentUserId, long labelId, long? projectId,
        long organizationId)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var query = _context.SensitivityLabels
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && !l.IsArchived);

            if (projectId.HasValue)
            {
                query = query.Where( r => r.ProjectId == projectId.Value || r.ProjectId == null);
            }
            else
            {
                query = query.Where(t => t.ProjectId == null);
            }

            var label = await query.FirstOrDefaultAsync();

            if (label == null)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");
            
            if (projectId.HasValue && label.ProjectId == null)
            {
                throw new InvalidOperationException("Organization sensitivity labels cannot be updated from the child projects.");
            }

            // If there are records that are currently using this label do not archive
            var recordCount = await _context.Records
                .Where(r => r.Labels.Any(l => l.Id == labelId))
                .CountAsync();

            if (recordCount > 0)
            {
                throw new Exception(
                    $"Cannot archive. Sensitivity label with id {labelId} is used on {recordCount} records.");
            }

            // Archive permissions for this sensitivity label 
            await _context.Permissions
                .Where(p => p.LabelId == labelId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.IsArchived, true)
                    .SetProperty(p => p.LastUpdatedAt, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
                    .SetProperty(p => p.LastUpdatedBy, currentUserId));

            // Archive label by ID
            label.IsArchived = true;
            label.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            label.LastUpdatedBy = currentUserId;

            await _context.SaveChangesAsync();

            // Log archive SensitivityLabel event
            var eventLog = new CreateEventRequestDto
            {
                Operation = "archive",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name }),
            };

            if (label.ProjectId != null)
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, label.ProjectId, eventLog);
            }
            else
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
            }

            await transaction.CommitAsync();

            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Unarchive a sensitivity label by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="labelId">ID of label to unarchive</param>
    /// <param name="projectId">ID of the project label belongs</param>
    /// <param name="organizationId">ID of the organization the label belongs</param>
    /// <returns>Boolean true if executed successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if label not found or is not archived</exception>
    public async Task<bool> UnarchiveSensitivityLabel(long currentUserId, long labelId, long? projectId,
        long organizationId)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var query = _context.SensitivityLabels
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && l.IsArchived == true);

            if (projectId.HasValue)
            {
                query = query.Where( r => r.ProjectId == projectId.Value || r.ProjectId == null);
            }
            else
            {
                query = query.Where(t => t.ProjectId == null);
            }

            var label = await query.FirstOrDefaultAsync();

            if (label == null || !label.IsArchived)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is not archived");
            
            if (projectId.HasValue && label.ProjectId == null)
            {
                throw new InvalidOperationException("Organization sensitivity labels cannot be updated from the child projects.");
            }

            // Unarchive the Label
            label.IsArchived = false;
            label.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            label.LastUpdatedBy = currentUserId;

            // Unarchive Permissions associated with the label
            await _context.Permissions
                .Where(p => p.LabelId == labelId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.IsArchived, false)
                    .SetProperty(p => p.LastUpdatedAt, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
                    .SetProperty(p => p.LastUpdatedBy, currentUserId));

            await _context.SaveChangesAsync();

            // Log unarchive SensitivityLabel event
            var eventLog = new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name }),
            };

            if (label.ProjectId != null)
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, label.ProjectId, eventLog);
            }
            else
            {
                await _eventBusiness.CreateEvent(currentUserId, organizationId, null, eventLog);
            }

            await transaction.CommitAsync();

            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
}