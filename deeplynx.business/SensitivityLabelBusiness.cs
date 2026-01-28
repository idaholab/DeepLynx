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

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for sensitivity label operations</param>
    /// <param name="eventBusiness">Used for logging events during CRUD operations</param>
    public SensitivityLabelBusiness(DeeplynxContext context, IEventBusiness eventBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
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
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

            if (projectId.HasValue)
                query = query.Where(l => l.ProjectId == projectId);

            var label = await query.FirstOrDefaultAsync();

            if (label == null || label.IsArchived)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found");

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
                if (dto.Name != null)
                {
                    // Update name based on action type
                    permission.Name = permission.Action switch
                    {
                        "read" => "Read " + dto.Name,
                        "write" => "Write " + dto.Name,
                        _ => permission.Name // fallback
                    };
                }
    
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
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && l.IsArchived == false);

            if (projectId.HasValue)
                query = query.Where(l => l.ProjectId == projectId);

            var label = await query.FirstOrDefaultAsync();

            if (label == null || label.IsArchived)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");

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
                query = query.Where(l => l.ProjectId == projectId);

            var label = await query.FirstOrDefaultAsync();

            if (label == null || !label.IsArchived)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is not archived");

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
                .Where(l => l.Id == labelId && l.OrganizationId == organizationId && l.IsArchived == false);

            if (projectId.HasValue)
                query = query.Where(l => l.ProjectId == projectId);

            var label = await query.FirstOrDefaultAsync();

            if (label == null || label.IsArchived)
                throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");

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

        var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var label = new SensitivityLabel
            {
                Name = dto.Name,
                Description = dto.Description,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId,
                ProjectId = projectId,
                OrganizationId = organizationId
            };

            _context.SensitivityLabels.Add(label);
            await _context.SaveChangesAsync();

            // Create permissions associated with the new Sensitivity Label
            var readPermission = new Permission
            {
                Name = "Read " + dto.Name,
                Description = "Permission to read " + dto.Name + "labeled records",
                Action = "read",
                LabelId = label.Id,
                IsDefault = false,
                ProjectId = projectId.HasValue ? projectId : null,
                OrganizationId = organizationId,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId,
            };
            _context.Permissions.Add(readPermission);

            var writePermission = new Permission
            {
                Name = "Write " + dto.Name,
                Description = "Permission to modify " + dto.Name + "labeled records",
                Action = "write",
                LabelId = label.Id,
                IsDefault = false,
                ProjectId = projectId.HasValue ? projectId : null,
                OrganizationId = organizationId,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId,
            };
            _context.Permissions.Add(writePermission);
            await _context.SaveChangesAsync();

            // Log create SensitivityLabel event
            var eventLog = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "sensitivity_label",
                EntityId = label.Id,
                EntityName = label.Name,
                Properties = JsonSerializer.Serialize(new { label.Name })
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
    ///     Asynchronously creates new Sensitivity Labels for a specified project.
    ///     Note: Will error out with foreign key constraint violation if project is not found.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the label belongs.</param>
    /// <param name="organizationId">The ID of the organization to which the label belongs.</param>
    /// <param name="labels">The label request data transfer object containing label details.</param>
    /// <returns>The created label response DTO with saved details.</returns>
    public async Task<List<SensitivityLabelResponseDto>> BulkCreateSensitivityLabels(
        long organizationId,
        long currentUserId,
        long? projectId,
        List<CreateSensitivityLabelRequestDto> labels)
    {
        if (labels == null || labels.Count == 0)
        {
            return new List<SensitivityLabelResponseDto>();
        }

        // Bulk insert into classes; if there is a name collision, update the description and uuid if present
        var sql = projectId.HasValue ? @"
            INSERT INTO sensitivity_labels (project_id, organization_id, name, last_updated_at, is_archived, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL
                DO UPDATE SET
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy
                RETURNING id, project_id, organization_id, name, last_updated_at, is_archived, last_updated_by;"
                    : @"
            INSERT INTO deeplynx.sensitivity_labels (project_id, organization_id, name, last_updated_at, is_archived, last_updated_by)
                VALUES {0}
                ON CONFLICT (organization_id, name) WHERE project_id IS NULL
                DO UPDATE SET
                    last_updated_at = @now,
                    last_updated_by = @lastUpdatedBy
            RETURNING id, project_id, organization_id, name, last_updated_at, is_archived, last_updated_by;";

        // establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new NpgsqlParameter("@projectId", projectId.HasValue ? (object)projectId.Value : DBNull.Value),
            new NpgsqlParameter("@organizationId", organizationId),
            new NpgsqlParameter("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new NpgsqlParameter("@lastUpdatedBy", currentUserId)
        };

        // establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(labels.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_name", dto.Name)
        }));

        // stringify the params and comma separate them
        var valueTuples = string.Join(", ", labels.Select((dto, i) =>
            $"(@projectId, @organizationId, @p{i}_name, @now, false, @lastUpdatedBy)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // returns the resulting upserted classes
        var result = await _context.Database
            .SqlQueryRaw<SensitivityLabelResponseDto>(sql, parameters.ToArray())
            .ToListAsync();
        
        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "sensitivity_label",
        };

        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

        return result;
    }

    /// <summary>
    ///     Get all sensitivity labels for a given project and/or organization
    /// </summary>
    /// <param name="projectIds">ID of the project across which to search</param>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <param name="hideArchived">Flag indicating whether to search on archived labels</param>
    /// <returns>A list of labels</returns>
    public async Task<IEnumerable<SensitivityLabelResponseDto>> GetAllSensitivityLabels(
        long[]? projectIds, long organizationId, bool hideArchived = true)
    {
        // Start with base query
        var query = _context.SensitivityLabels
            .Where(l => l.OrganizationId == organizationId)
            .AsQueryable();

        // Filter by projectIds if provided and not empty
        if (projectIds is { Length: > 0 })
            query = query.Where(l => !l.ProjectId.HasValue || projectIds.AsEnumerable().Contains(l.ProjectId.Value));

        // Optionally hide archived classes
        if (hideArchived)
            query = query.Where(l => !l.IsArchived);

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
            .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(l => !l.ProjectId.HasValue || l.ProjectId == projectId);

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
}