using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class RelationshipBusiness : IRelationshipBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEdgeBusiness _edgeBusiness;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RelationshipBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the relationship operations.</param>
    /// <param name="edgeBusiness">Passed in context of edge objects.</param>
    /// <param name="eventBusiness">Used to access event operations</param>
    public RelationshipBusiness(DeeplynxContext context, IEdgeBusiness edgeBusiness,
        IEventBusiness eventBusiness)
    {
        _edgeBusiness = edgeBusiness;
        _context = context;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     Retrieves all relationships
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectIds">(optional) The ID(s) of the project(s) to filter classes by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived relationships from the result</param>
    /// <returns>A list of relationships</returns>
    public async Task<List<RelationshipResponseDto>> GetAllRelationships(long organizationId, long[]? projectIds,
        bool hideArchived)
    {
        // Start with base query
        var query = _context.Relationships
            .Where(r => r.OrganizationId == organizationId)
            .AsQueryable();

        // Filter by projectIds if provided and not empty
        if (projectIds is { Length: > 0 })
            query = query.Where(r => r.ProjectId.HasValue && projectIds.Contains(r.ProjectId.Value));

        // Optionally hide archived relationships
        if (hideArchived) query = query.Where(r => !r.IsArchived);

        var relationships = await query
            .Include(r => r.Origin)
            .Include(r => r.Destination)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.Uuid,
                r.ProjectId,
                r.OrganizationId,
                r.LastUpdatedBy,
                r.LastUpdatedAt,
                r.IsArchived,
                r.OriginId,
                r.DestinationId
            })
            .ToListAsync();


        // Manual mapping to Relationship objects to match return type without getting infinite loop on Origin or Destination
        return relationships.Select(r => new RelationshipResponseDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Uuid = r.Uuid,
            ProjectId = r.ProjectId,
            OrganizationId = r.OrganizationId,
            OriginId = r.OriginId,
            DestinationId = r.DestinationId,
            LastUpdatedBy = r.LastUpdatedBy,
            LastUpdatedAt = r.LastUpdatedAt,
            IsArchived = r.IsArchived
        }).ToList();
    }

    /// <summary>
    ///     Retrieves a specific relationship by ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID by which to retrieve the class</param>
    /// <param name="relationshipId">The ID of the relationship to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result</param>
    /// <returns>The given relationship to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship not found or is archived</exception>
    public async Task<RelationshipResponseDto> GetRelationship(long organizationId, long? projectId,
        long relationshipId,
        bool hideArchived)
    {
        var query = _context.Relationships
            .Where(r => r.Id == relationshipId && r.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId is not null)
            query = query.Where(r => r.ProjectId == projectId);

        var relationship = await query
            .Include(r => r.Origin)
            .Include(r => r.Destination)
            .FirstOrDefaultAsync();

        if (relationship == null) throw new KeyNotFoundException($"Relationship with ID {relationshipId} not found.");

        if (hideArchived && relationship.IsArchived)
            throw new KeyNotFoundException($"Relationship with id {relationshipId} is archived");

        return new RelationshipResponseDto
        {
            Id = relationship.Id,
            Name = relationship.Name,
            Description = relationship.Description,
            Uuid = relationship.Uuid,
            ProjectId = relationship.ProjectId,
            OrganizationId = relationship.OrganizationId,
            LastUpdatedBy = relationship.LastUpdatedBy,
            LastUpdatedAt = relationship.LastUpdatedAt,
            IsArchived = relationship.IsArchived,
            OriginId = relationship.OriginId,
            DestinationId = relationship.DestinationId
        };
    }

    /// <summary>
    ///     Creates a new relationship based on the data transfer object supplied.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="dto">A data transfer object with details on the new relationship to be created.</param>
    /// <returns>The new relationship which was just created.</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship or origin/destination classes not found</exception>
    public async Task<RelationshipResponseDto> CreateRelationship(long currentUserId, long organizationId,
        long? projectId,
        CreateRelationshipRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        if (dto.OriginId != null)
        {
            var originClass = await _context.Classes.FirstOrDefaultAsync(c => c.Id == dto.OriginId && !c.IsArchived);
            if (originClass == null) throw new KeyNotFoundException($"Origin class with ID {dto.OriginId} not found.");
        }

        if (dto.DestinationId != null)
        {
            var destinationClass =
                await _context.Classes.FirstOrDefaultAsync(c => c.Id == dto.DestinationId && !c.IsArchived);
            if (destinationClass == null)
                throw new KeyNotFoundException($"Destination class with ID {dto.DestinationId} not found.");
        }

        var relationship = new Relationship
        {
            Name = dto.Name,
            Description = dto.Description,
            Uuid = dto.Uuid,
            OriginId = dto.OriginId,
            DestinationId = dto.DestinationId,
            OrganizationId = organizationId,
            ProjectId = projectId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId
        };

        _context.Relationships.Add(relationship);
        await _context.SaveChangesAsync();

        if (dto.OriginId != null)
            await _context.Entry(relationship)
                .Reference(r => r.Origin)
                .LoadAsync();

        if (dto.DestinationId != null)
            await _context.Entry(relationship)
                .Reference(r => r.Destination)
                .LoadAsync();

        // log relationship create event
        await _eventBusiness.CreateEvent(
            currentUserId, 
            organizationId, 
            projectId, 
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "relationship",
                EntityId = relationship.Id,
                EntityName = relationship.Name,
                Properties = JsonSerializer.Serialize(new {relationship.Name}),
            });
        
        return new RelationshipResponseDto
        {
            Id = relationship.Id,
            Name = relationship.Name,
            Description = relationship.Description,
            Uuid = relationship.Uuid,
            OrganizationId = relationship.OrganizationId,
            ProjectId = relationship.ProjectId,
            LastUpdatedBy = relationship.LastUpdatedBy,
            LastUpdatedAt = relationship.LastUpdatedAt,
            IsArchived = relationship.IsArchived,
            OriginId = relationship.OriginId,
            DestinationId = relationship.DestinationId
        };
    }

    /// <summary>
    ///     Creates a new relationship based on the data transfer object supplied.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="relationships">
    ///     A list of relationship data transfer objects with details on the new relationship to be
    ///     created.
    /// </param>
    /// <returns>The new relationship which was just created.</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship or origin/destination classes not found</exception>
    public async Task<List<RelationshipResponseDto>> BulkCreateRelationships(
        long currentUserId,
        long organizationId,
        long? projectId,
        List<CreateRelationshipRequestDto> relationships)
    {
        // Bulk insert into relationships; if there is a name collision, update the description and uuid if present
        var sql = projectId.HasValue
            ? @"
          INSERT INTO deeplynx.relationships (organization_id, project_id, name, description, uuid, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL
            DO UPDATE SET
                description = COALESCE(EXCLUDED.description, relationships.description),
                uuid = COALESCE(EXCLUDED.uuid, relationships.uuid),
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
            RETURNING *;"
            : @"
            INSERT INTO deeplynx.relationships (organization_id, project_id, name, description, uuid, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, name) WHERE project_id IS NULL
            DO UPDATE SET
                description = COALESCE(EXCLUDED.description, relationships.description),
                uuid = COALESCE(EXCLUDED.uuid, relationships.uuid),
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
            RETURNING *;";

        // establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@organizationId", organizationId),
            new("@projectId", projectId.HasValue ? projectId : DBNull.Value),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new("@lastUpdatedBy", currentUserId)
        };

        // establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(relationships.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_name", dto.Name),
            new NpgsqlParameter($"@p{i}_desc", (object?)dto.Description ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_uuid", (object?)dto.Uuid ?? DBNull.Value)
        }));

        // stringify the params and comma separate them
        var valueTuples = string.Join(", ", relationships.Select((dto, i) =>
            $"(@organizationId, @projectId, @p{i}_name, @p{i}_desc, @p{i}_uuid, @now, false, @lastUpdatedBy)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // returns the resulting upserted relationships
        var result = await _context.Database
            .SqlQueryRaw<RelationshipResponseDto>(sql, parameters.ToArray())
            .ToListAsync();
        
        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "relationship"
        };
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);
        
        return result;
    }

    /// <summary>
    ///     Updates an existing relationship by its ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs.</param>
    /// <param name="relationshipId">The ID of the relationship to update.</param>
    /// <param name="dto">The relationship request data transfer object containing updated relationship details.</param>
    /// <returns>The updated relationship response DTO with its details</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship or origin/destination classes not found</exception>
    public async Task<RelationshipResponseDto> UpdateRelationship(long currentUserId, long organizationId,
        long? projectId,
        long relationshipId, UpdateRelationshipRequestDto dto)
    {
        var query = _context.Relationships
            .Where(r => r.Id == relationshipId && r.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId);

        var relationship = await query.FirstOrDefaultAsync();
        if (relationship is null || relationship.IsArchived)
            throw new KeyNotFoundException($"Relationship with ID {relationshipId} not found.");
        
        if (dto.OriginId.HasValue)
        {
            var originClass =
                await _context.Classes.FirstOrDefaultAsync(c =>
                    c.Id == dto.OriginId && !c.IsArchived);
            if (originClass == null) throw new KeyNotFoundException($"Origin class with ID {dto.OriginId} not found.");
        }

        if (dto.DestinationId.HasValue)
        {
            var destinationClass = await _context.Classes.FirstOrDefaultAsync(c =>
                c.Id == dto.DestinationId && !c.IsArchived);
            if (destinationClass == null)
                throw new KeyNotFoundException($"Destination class with ID {dto.DestinationId} not found.");
        }

        relationship.Name = dto.Name ?? relationship.Name;
        relationship.Description = dto.Description ?? relationship.Description;
        relationship.Uuid = dto.Uuid ?? relationship.Uuid;
        relationship.OriginId = dto.OriginId ?? relationship.OriginId;
        relationship.DestinationId = dto.DestinationId ?? relationship.DestinationId;
        relationship.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        relationship.LastUpdatedBy = currentUserId;
        _context.Relationships.Update(relationship);
        await _context.SaveChangesAsync();

        // log relationship update event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "update",
            EntityType = "relationship",
            EntityId = relationship.Id,
            EntityName = relationship.Name,
            Properties = JsonSerializer.Serialize(new {relationship.Name}),
        });
        
        return new RelationshipResponseDto
        {
            Id = relationship.Id,
            Name = relationship.Name,
            Description = relationship.Description,
            Uuid = relationship.Uuid,
            OrganizationId = organizationId,
            ProjectId = relationship.ProjectId,
            LastUpdatedBy = relationship.LastUpdatedBy,
            LastUpdatedAt = relationship.LastUpdatedAt,
            IsArchived = relationship.IsArchived,
            OriginId = relationship.OriginId,
            DestinationId = relationship.DestinationId
        };
    }

    /// <summary>
    ///     Archive a specific relationship by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs.</param>
    /// <param name="relationshipId">The ID of the relationship to archive</param>
    /// <returns>Boolean true on successful archive</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship not found</exception>
    public async Task<bool> ArchiveRelationship(long currentUserId, long organizationId, long? projectId,
        long relationshipId)
    {
        var relationship = await _context.Relationships.FindAsync(relationshipId);

        if (relationship == null || relationship.ProjectId != projectId || relationship.IsArchived)
            throw new KeyNotFoundException($"Relationship with id {relationshipId} not found");

        relationship.IsArchived = true;
        relationship.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        relationship.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log relationship archive event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "relationship",
            EntityId = relationship.Id,
            EntityName = relationship.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new {relationship.Name}),
        });
        
        return true;
    }

    /// <summary>
    ///     Unarchive a specific relationship by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs.</param>
    /// <param name="relationshipId">The ID of the relationship to unarchive</param>
    /// <returns>Boolean true on successful unarchive action</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship not found</exception>
    public async Task<bool> UnarchiveRelationship(long currentUserId, long organizationId, long? projectId,
        long relationshipId)
    {
        var relationship = await _context.Relationships.FindAsync(relationshipId);

        if (relationship == null || relationship.ProjectId != projectId || !relationship.IsArchived)
            throw new KeyNotFoundException($"Relationship with id {relationshipId} not found or is not archived.");

        relationship.IsArchived = false;
        relationship.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        relationship.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log relationship unarchive event
        // await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        // {
        //     Operation = "unarchive",
        //     EntityType = "relationship",
        //     EntityId = relationship.Id,
        //     EntityName = relationship.Name,
        //     DataSourceId = null,
        //     Properties = JsonSerializer.Serialize(new {relationship.Name}),
        // });
        
        return true;
    }

    /// <summary>
    ///     Validates that all provided relationship names exist in the database for the specified project.
    ///     Used by MetadataBusiness to enforce ontologyMutable settings.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project ID to search within</param>
    /// <param name="relationshipNames">List of relationship names to validate</param>
    /// <returns>List of relationships that were found</returns>
    /// <exception cref="KeyNotFoundException">Thrown if one or more relationship names not found</exception>
    /// <exception cref="ArgumentException">Thrown if relationshipNames list is null or empty</exception>
    public async Task<List<RelationshipResponseDto>> GetRelationshipsByName(long organizationId, long? projectId,
        List<string> relationshipNames)
    {
        if (relationshipNames == null || !relationshipNames.Any())
            throw new ArgumentException("Relationship names list cannot be null or empty", nameof(relationshipNames));


        var cleanRelationshipNames = relationshipNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        if (!cleanRelationshipNames.Any())
            throw new ArgumentException("No valid relationship names provided", nameof(relationshipNames));

        var existingRelationships = await _context.Relationships
            .Where(r => r.ProjectId == projectId
                        && r.IsArchived == false
                        && cleanRelationshipNames.Contains(r.Name))
            .Include(r => r.Origin)
            .Include(r => r.Destination)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.Uuid,
                r.ProjectId,
                r.LastUpdatedBy,
                r.LastUpdatedAt,
                r.IsArchived,
                r.OriginId,
                r.DestinationId
            })
            .ToListAsync();

        // Check for missing relationships
        var foundRelationshipNames = existingRelationships.Select(r => r.Name).ToHashSet();
        var missingRelationshipNames =
            cleanRelationshipNames.Where(name => !foundRelationshipNames.Contains(name)).ToList();

        if (missingRelationshipNames.Any())
            throw new KeyNotFoundException(
                $"Relationships not found with names: {string.Join(", ", missingRelationshipNames)}");

        // Convert to DTOs (manual mapping to avoid infinite loop with Origin/Destination)
        return existingRelationships.Select(r => new RelationshipResponseDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Uuid = r.Uuid,
            ProjectId = r.ProjectId,
            LastUpdatedBy = r.LastUpdatedBy,
            LastUpdatedAt = r.LastUpdatedAt,
            OriginId = r.OriginId,
            DestinationId = r.DestinationId,
            IsArchived = r.IsArchived
        }).ToList();
    }

    /// <summary>
    ///     Delete a specific relationship by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the relationship belongs.</param>
    /// <param name="relationshipId">The ID of the relationship to delete</param>
    /// <returns>Boolean true on successful deletion</returns>
    /// <exception cref="KeyNotFoundException">Returned if relationship not found</exception>
    public async Task<bool> DeleteRelationship(long currentUserId, long organizationId, long? projectId,
        long relationshipId)
    {
        var query = _context.Relationships
            .Where(r => r.Id == relationshipId && r.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId);

        var relationship = await query.FirstOrDefaultAsync();

        if (relationship is null)
            throw new KeyNotFoundException($"Relationship with id {relationshipId} not found");

        // grab this to log the deletion event after successful delete
        var deletedName = relationship.Name;

        _context.Relationships.Remove(relationship);
        await _context.SaveChangesAsync();
        
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "relationship",
            EntityId = relationship.Id,
            EntityName = relationship.Name,
            Properties = JsonSerializer.Serialize(new {relationship.Name}),
        });

        return true;
    }
}