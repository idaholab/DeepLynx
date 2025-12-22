using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

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
        var query = _context.SensitivityLabels
            .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(l => l.ProjectId == projectId);

        var label = await query.FirstOrDefaultAsync();

        if (label == null || label.IsArchived)
            throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");

        label.IsArchived = true;
        label.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        label.LastUpdatedBy = currentUserId;

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
        
        await _context.SaveChangesAsync();
        
        return true;
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
        var query = _context.SensitivityLabels
            .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(l => l.ProjectId == projectId);

        var label = await query.FirstOrDefaultAsync();

        if (label == null || !label.IsArchived)
            throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is not archived");

        label.IsArchived = false;
        label.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        label.LastUpdatedBy = currentUserId;

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
        
        await _context.SaveChangesAsync();
        
        return true;
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
        var query = _context.SensitivityLabels
            .Where(l => l.Id == labelId && l.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(l => l.ProjectId == projectId);

        var label = await query.FirstOrDefaultAsync();

        if (label == null || label.IsArchived)
            throw new KeyNotFoundException($"Sensitivity label with id {labelId} not found or is archived");

        _context.SensitivityLabels.Remove(label);
        
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
        
        await _context.SaveChangesAsync();
        
        return true;
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
    ///     Get all sensitivity labels for a given project and/or organization
    /// </summary>
    /// <param name="projectId">ID of the project across which to search</param>
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
            query = query.Where(l => l.ProjectId.HasValue && projectIds.Contains(l.ProjectId.Value));

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
            query = query.Where(l => l.ProjectId == projectId);

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