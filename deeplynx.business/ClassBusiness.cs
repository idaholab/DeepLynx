using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class ClassBusiness : IClassBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly IRecordBusiness _recordBusiness;
    private readonly IRelationshipBusiness _relationshipBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClassBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for class operations</param>
    /// <param name="edgeMappingBusiness">Passed in context of edge mapping objects</param>
    /// <param name="recordBusiness">Passed in context of record objects</param>
    /// <param name="relationshipBusiness">Passed in context of relationship objects</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    public ClassBusiness(
        DeeplynxContext context,
        IRecordBusiness recordBusiness,
        IRelationshipBusiness relationshipBusiness,
        IEventBusiness eventBusiness
    )
    {
        _context = context;
        _recordBusiness = recordBusiness;
        _relationshipBusiness = relationshipBusiness;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     Retrieves all classes
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the classes belong</param>
    /// <param name="projectIds">(optional) The ID(s) of the project(s) to filter classes by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result</param>
    /// <returns>A list of classes</returns>
    public async Task<List<ClassResponseDto>> GetAllClasses(
        long organizationId,
        long[]? projectIds,
        bool hideArchived)
    {
        // Start with base query
        var query = _context.Classes
            .Where(c => c.OrganizationId == organizationId)
            .AsQueryable();

        // Filter by projectIds if provided and not empty
        if (projectIds is { Length: > 0 })
            query = query.Where(c => c.ProjectId.HasValue && projectIds.Contains(c.ProjectId.Value));

        // Optionally hide archived classes
        if (hideArchived)
            query = query.Where(c => !c.IsArchived);

        // Execute the query and project to DTO
        return await query
            .Select(c => new ClassResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Uuid = c.Uuid,
                ProjectId = c.ProjectId,
                OrganizationId = c.OrganizationId,
                LastUpdatedAt = c.LastUpdatedAt,
                LastUpdatedBy = c.LastUpdatedBy,
                IsArchived = c.IsArchived
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves a specific class by ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">The ID by which to retrieve the class</param>
    /// <param name="classId">The ID of the class to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result</param>
    /// <returns>The given class to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if class not found or is archived</exception>
    public async Task<ClassResponseDto> GetClass(
        long organizationId,
        long? projectId,
        long classId,
        bool hideArchived)
    {
        var query = _context.Classes
            .Where(c => c.Id == classId && c.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId is not null)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();
        if (returnedClass is null)
            throw new KeyNotFoundException($"Class with id {classId} not found");
        if (hideArchived && returnedClass.IsArchived)
            throw new KeyNotFoundException($"Class with id {classId} is archived");

        return new ClassResponseDto
        {
            Id = returnedClass.Id,
            Name = returnedClass.Name,
            Description = returnedClass.Description,
            Uuid = returnedClass.Uuid,
            ProjectId = returnedClass.ProjectId,
            OrganizationId = returnedClass.OrganizationId,
            LastUpdatedAt = returnedClass.LastUpdatedAt,
            LastUpdatedBy = returnedClass.LastUpdatedBy,
            IsArchived = returnedClass.IsArchived
        };
    }

    /// <summary>
    ///     Creates a new class based on the data transfer object supplied.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="dto">A data transfer object with details on the new class to be created.</param>
    /// <returns>The new class which was just created.</returns>
    /// <exception cref="KeyNotFoundException">Returned if class not found</exception>
    /// <exception cref="Exception">Returned if class already exists</exception>
    public async Task<ClassResponseDto> CreateClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        CreateClassRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var newClass = new Class
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Name = dto.Name,
            Description = dto.Description,
            Uuid = dto.Uuid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            IsArchived = false
        };

        _context.Classes.Add(newClass);
        await _context.SaveChangesAsync();

        // log event with class create details
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "class",
                EntityId = newClass.Id,
                EntityName = newClass.Name,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { newClass.Name })
            });

        return new ClassResponseDto
        {
            Id = newClass.Id,
            Name = newClass.Name,
            Description = newClass.Description,
            Uuid = newClass.Uuid,
            OrganizationId = newClass.OrganizationId,
            ProjectId = newClass.ProjectId,
            LastUpdatedAt = newClass.LastUpdatedAt,
            LastUpdatedBy = newClass.LastUpdatedBy,
            IsArchived = newClass.IsArchived
        };
    }

    /// <summary>
    ///     Bulk creates new classes based on the data transfer objects supplied.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the classes belong</param>
    /// <param name="projectId">The ID of the project to which the classes belong</param>
    /// <param name="classes">A list of class data transfer object with details on the new class to be created.</param>
    /// <returns>The new classes which were just created.</returns>
    /// <exception cref="Exception">Returned if classes already exist</exception>
    public async Task<List<ClassResponseDto>> BulkCreateClasses(
        long currentUserId,
        long organizationId,
        long? projectId,
        List<CreateClassRequestDto> classes)
    {
        // Bulk insert into classes; if there is a name collision, update the description and uuid if present
        var sql = projectId.HasValue
            ? @"
            INSERT INTO deeplynx.classes (
                organization_id, project_id, name, description, uuid, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, project_id, name) WHERE project_id IS NOT NULL 
            DO UPDATE SET
                description = COALESCE(EXCLUDED.description, classes.description),
                uuid = COALESCE(EXCLUDED.uuid, classes.uuid),
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
           RETURNING id, project_id, organization_id, name, description, 
                uuid, last_updated_at, last_updated_by, is_archived;"
            : @"
            INSERT INTO deeplynx.classes (
                organization_id, project_id, name, description, uuid, last_updated_at, is_archived, last_updated_by)
            VALUES {0}
            ON CONFLICT (organization_id, name) WHERE project_id IS NULL 
            DO UPDATE SET
                description = COALESCE(EXCLUDED.description, classes.description),
                uuid = COALESCE(EXCLUDED.uuid, classes.uuid),
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy
           RETURNING id, project_id, organization_id, name, description, 
                uuid, last_updated_at, last_updated_by, is_archived;";

        // establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@organizationId", organizationId),
            new("@projectId", projectId.HasValue ? projectId.Value : DBNull.Value),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new("@lastUpdatedBy", currentUserId)
        };

        // establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(classes.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_name", dto.Name),
            new NpgsqlParameter($"@p{i}_desc", (object?)dto.Description ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_uuid", (object?)dto.Uuid ?? DBNull.Value)
        }));

        // stringify the params and comma separate them
        var valueTuples = string.Join(", ", classes.Select((dto, i) =>
            $"(@organizationId, @projectId, @p{i}_name, @p{i}_desc, @p{i}_uuid, @now, false, @lastUpdatedBy)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // returns the resulting upserted classes
        var result = await _context.Database
            .SqlQueryRaw<ClassResponseDto>(sql, parameters.ToArray())
            .ToListAsync();

        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "class",
            DataSourceId = null,
        };
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

        return result;
    }

    /// <summary>
    ///     Updates an existing class by its ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">The ID of the project to which the class belongs.</param>
    /// <param name="classId">The ID of the class to update.</param>
    /// <param name="dto">The class request data transfer object containing updated class details.</param>
    /// <returns>The updated class response DTO with its details</returns>
    /// <exception cref="KeyNotFoundException">Returned if class not found or if ids missing</exception>
    public async Task<ClassResponseDto> UpdateClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        long classId,
        UpdateClassRequestDto dto)
    {
        var query = _context.Classes
            .Where(c => c.Id == classId && c.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();
        if (returnedClass is null || returnedClass.IsArchived)
            throw new KeyNotFoundException($"Class with id {classId} not found");

        returnedClass.Name = dto.Name ?? returnedClass.Name;
        returnedClass.Description = dto.Description ?? returnedClass.Description;
        returnedClass.Uuid = dto.Uuid ?? returnedClass.Uuid;
        returnedClass.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedClass.LastUpdatedBy = currentUserId;

        _context.Classes.Update(returnedClass);
        await _context.SaveChangesAsync();

        // log event with class update details
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "class",
                EntityId = returnedClass.Id,
                EntityName = returnedClass.Name,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { returnedClass.Name })
            });

        return new ClassResponseDto
        {
            Id = returnedClass.Id,
            Name = returnedClass.Name,
            Description = returnedClass.Description,
            Uuid = returnedClass.Uuid,
            OrganizationId = returnedClass.OrganizationId,
            ProjectId = returnedClass.ProjectId,
            LastUpdatedAt = returnedClass.LastUpdatedAt,
            LastUpdatedBy = returnedClass.LastUpdatedBy,
            IsArchived = returnedClass.IsArchived
        };
    }

    /// <summary>
    ///     Delete a class by id.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">ID of the project to which the class belongs.</param>
    /// <param name="classId">ID of the class to delete.</param>
    /// <returns>Boolean true on successful deletion.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if class is not found.</exception>
    public async Task<bool> DeleteClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        long classId)
    {
        var query = _context.Classes
            .Where(c => c.Id == classId && c.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();

        if (returnedClass is null)
            throw new KeyNotFoundException($"Class with id {classId} not found");

        // grab this to log the deletion event after successful delete
        var deletedName = returnedClass.Name;

        _context.Classes.Remove(returnedClass);
        await _context.SaveChangesAsync();

        // log event with class delete details
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "delete",
                EntityType = "class",
                EntityId = classId,
                EntityName = deletedName,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { deletedName })
            }
        );

        return true;
    }

    /// <summary>
    ///     Archive (soft delete) a class by id. This also archives downstream dependents.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">ID of the project to which the class belongs.</param>
    /// <param name="classId">ID of the class to archive.</param>
    /// <returns>Boolean true on successful archival.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if class is not found.</exception>
    /// <exception cref="DependencyDeletionException">Thrown if archival fails.</exception>
    public async Task<bool> ArchiveClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        long classId)
    {
        var query = _context.Classes
            .Where(c => c.Id == classId && c.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();

        if (returnedClass is null || returnedClass.IsArchived)
            throw new KeyNotFoundException($"Class with id {classId} not found or is already archived");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run archive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the archive class procedure, which archives this class
                // and all child objects with class_id as a foreign key
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.archive_class({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE,  {2}::INTEGER)",
                    classId, lastUpdatedAt, currentUserId
                );

                if (archived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to archive class {classId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to archive class {classId} or its downstream dependents: {exc}");
            }
        }

        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "archive",
                EntityType = "class",
                EntityId = returnedClass.Id,
                EntityName = returnedClass.Name,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { returnedClass.Name })
            });

        return true;
    }


    /// <summary>
    ///     Unarchive a class by id. This also unarchives downstream dependents.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">ID of the project to which the class belongs.</param>
    /// <param name="classId">ID of the class to unarchive.</param>
    /// <returns>Boolean true on successful unarchive.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if class is not found.</exception>
    /// <exception cref="DependencyDeletionException">Thrown if unarchive action fails.</exception>
    public async Task<bool> UnarchiveClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        long classId)
    {
        var query = _context.Classes
            .Where(c => c.Id == classId && c.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();

        if (returnedClass is null || !returnedClass.IsArchived)
            throw new KeyNotFoundException($"Class with id {classId} not found or is not archived");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run archive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the archive class procedure, which archives this class
                // and all child objects with class_id as a foreign key
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.unarchive_class({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE,  {2}::INTEGER)",
                    classId, lastUpdatedAt, currentUserId
                );

                if (archived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to unarchive class {classId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to unarchive class {classId} or its downstream dependents: {exc}");
            }
        }

        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "class",
                EntityId = returnedClass.Id,
                EntityName = returnedClass.Name,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { returnedClass.Name })
            }
        );

        return true;
    }

    /// <summary>
    ///     App-internal function to create a class by the given name if not exists,
    ///     or return a class by the given name if exists.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the class belongs</param>
    /// <param name="projectId">The ID of the project we are searching</param>
    /// <param name="className"> The name of the project class to search for</param>
    /// <returns>Class DTO of the found or created class</returns>
    public async Task<ClassResponseDto> GetOrCreateClass(
        long currentUserId,
        long organizationId,
        long? projectId,
        string className)
    {
        var query = _context.Classes
            .Where(c => c.Name == className && c.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        var returnedClass = await query.FirstOrDefaultAsync();
        // if a class by the supplied name is found, return it
        if (returnedClass is not null || !returnedClass.IsArchived)
            return new ClassResponseDto
            {
                Id = returnedClass.Id,
                Name = returnedClass.Name
            };

        // otherwise, create a new class with the supplied name
        return await CreateClass(currentUserId, organizationId, projectId,
            new CreateClassRequestDto { Name = className });
    }
}