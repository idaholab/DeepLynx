using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class RecordBusiness : IRecordBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly ITagBusiness _tagBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the record operations.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    /// <param name="tagBusiness">Used for creating tags related to a record.</param>
    public RecordBusiness(DeeplynxContext context, IEventBusiness eventBusiness, ITagBusiness tagBusiness)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _tagBusiness = tagBusiness;
    }

    /// <summary>
    ///     Retrieves all records for a specific project and datasource.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="fileType">File extension to filter by (e.g., pdf, png, jpg)</param>
    /// <returns>A list of records based on the applied filters.</returns>
    public async Task<List<RecordResponseDto>> GetAllRecords(
        long organizationId, long projectId, long? dataSourceId, bool hideArchived, string? fileType = null)
    {
        var recordQuery = _context.Records
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        if (dataSourceId.HasValue) recordQuery = recordQuery.Where(r => r.DataSourceId == dataSourceId);

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            var formattedFileType = fileType.TrimStart('.').ToLower();
            recordQuery = recordQuery.Where(r => r.FileType == formattedFileType);
        }

        var records = await recordQuery
            .Include(r => r.Tags)
            .ToListAsync();

        return records
            .Select(r => new RecordResponseDto
            {
                Id = r.Id,
                Description = r.Description,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                ClassId = r.ClassId,
                DataSourceId = r.DataSourceId,
                ProjectId = r.ProjectId,
                OrganizationId = r.OrganizationId,
                LastUpdatedBy = r.LastUpdatedBy,
                LastUpdatedAt = r.LastUpdatedAt,
                IsArchived = r.IsArchived,
                FileType = r.FileType,
                Tags = r.Tags.Select(t => new RecordTagDto
                {
                    Id = t.Id,
                    Name = t.Name
                }).ToList()
            }).ToList();
    }

    /// <summary>
    ///     Get all records that contain all given tags
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="tagIds">List of tag IDs - returned records must contain every given ID</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns></returns>
    public async Task<List<RecordResponseDto>> GetRecordsByTags(
        long organizationId, long projectId, long[] tagIds, bool hideArchived)
    {
        var recordQuery = _context.Records
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        // Only return records that contain ALL given IDs
        recordQuery = recordQuery.Where(r =>
            tagIds.All(tagId => r.Tags.Any(t => t.Id == tagId)));

        var records = await recordQuery.Include(r => r.Tags).ToListAsync();

        return records
            .Select(r => new RecordResponseDto
            {
                Id = r.Id,
                Description = r.Description,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                ClassId = r.ClassId,
                DataSourceId = r.DataSourceId,
                ProjectId = r.ProjectId,
                OrganizationId = r.OrganizationId,
                LastUpdatedBy = r.LastUpdatedBy,
                LastUpdatedAt = r.LastUpdatedAt,
                IsArchived = r.IsArchived,
                FileType = r.FileType,
                Tags = r.Tags.Select(t => new RecordTagDto
                {
                    Id = t.Id,
                    Name = t.Name
                }).ToList()
            }).ToList();
    }

    /// <summary>
    ///     Retrieves a specific record by its ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project of the record to retrieve</param>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record in question</returns>
    /// <exception cref="KeyNotFoundException">Returned if record not found</exception>
    public async Task<RecordResponseDto> GetRecord(
        long organizationId, long projectId, long recordId, bool hideArchived)
    {
        var record = await _context.Records
            .Where(r => r.ProjectId == projectId && r.Id == recordId && r.OrganizationId == organizationId)
            .Include(r => r.Tags)
            .FirstOrDefaultAsync();

        if (record == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        if (hideArchived && record.IsArchived) throw new KeyNotFoundException($"Record with id {recordId} is archived");

        return new RecordResponseDto
        {
            Id = record.Id,
            Description = record.Description,
            Uri = record.Uri,
            Properties = record.Properties,
            OriginalId = record.OriginalId,
            ObjectStorageId = record.ObjectStorageId,
            Name = record.Name,
            ClassId = record.ClassId,
            DataSourceId = record.DataSourceId,
            ProjectId = record.ProjectId,
            OrganizationId = record.OrganizationId,
            LastUpdatedBy = record.LastUpdatedBy,
            LastUpdatedAt = record.LastUpdatedAt,
            IsArchived = record.IsArchived,
            FileType = record.FileType,
            Tags = record.Tags.Select(t => new RecordTagDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList()
        };
    }

    /// <summary>
    ///     Attaches a tag to a record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or tag are not found</exception>
    /// <exception cref="Exception">Thrown if the tag is already attached to the record</exception>
    public async Task<bool> AttachTag(long organizationId, long projectId, long recordId, long tagId)
    {
        var recordExists = await _context.Records
            .AnyAsync(r => r.Id == recordId
                           && r.ProjectId == projectId
                           && r.OrganizationId == organizationId
                           && !r.IsArchived);

        if (!recordExists)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        var tagExists = await _context.Tags
            .AnyAsync(t => t.Id == tagId
                           && t.OrganizationId == organizationId
                           && (t.ProjectId == projectId || t.ProjectId == null)
                           && !t.IsArchived);

        if (!tagExists)
            throw new KeyNotFoundException($"Tag with id {tagId} not found or is archived.");

        var alreadyAttached = await _context.Records
            .Where(r => r.Id == recordId)
            .SelectMany(r => r.Tags)
            .AnyAsync(t => t.Id == tagId);

        if (alreadyAttached)
            throw new InvalidOperationException($"Tag with id {tagId} is already attached to record {recordId}");

        // Only now load entities for modification
        var record = await _context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        var tag = await _context.Tags.FindAsync(tagId);

        record.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Unattach a tag from a record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or tag are not found</exception>
    public async Task<bool> UnattachTag(long organizationId, long projectId, long recordId, long tagId)
    {
        var recordExists = await _context.Records
            .AnyAsync(r => r.Id == recordId
                           && r.ProjectId == projectId
                           && r.OrganizationId == organizationId
                           && !r.IsArchived);

        if (!recordExists)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        var tagExists = await _context.Tags
            .AnyAsync(t => t.Id == tagId
                           && t.OrganizationId == organizationId
                           && (t.ProjectId == projectId || t.ProjectId == null)
                           && !t.IsArchived);

        if (!tagExists)
            throw new KeyNotFoundException($"Tag with id {tagId} not found or is archived.");

        // Only now load entities for modification
        var record = await _context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == recordId);
        var tag = await _context.Tags.FindAsync(tagId);

        record.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Bulk attach tags and records
    /// </summary>
    /// <param name="dtos">A list of record_id/tag_id pairs to be inserted</param>
    /// <returns>True if successful</returns>
    /// <exception cref="Exception">Thrown if tags unable to be attached</exception>
    public async Task<bool> BulkAttachTags(List<RecordTagLinkDto> dtos)
    {
        // Bulk insert into record_tags
        var sql = @"INSERT INTO deeplynx.record_tags (record_id, tag_id) VALUES {0} ON CONFLICT DO NOTHING;";

        // establish parameters
        var parameters = new List<NpgsqlParameter>();
        parameters.AddRange(dtos.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@record{i}_id", dto.RecordId),
            new NpgsqlParameter($"@tag{i}_id", dto.TagId)
        }));

        // stringify params and comma separate them
        var valueTuples = string.Join(", ", dtos.Select((dto, i) => $"(@record{i}_id, @tag{i}_id)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        return true;
    }

    /// <summary>
    ///     Get records by their original ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project ID to search within</param>
    /// <param name="originalIds">List of original IDs to validate</param>
    /// <returns>List of records that were found</returns>
    /// <exception cref="KeyNotFoundException">Thrown if one or more original IDs not found</exception>
    /// <exception cref="ArgumentException">Thrown if originalIds list is null or empty</exception>
    public async Task<List<RecordResponseDto>> GetRecordsByOriginalId(long organizationId, long projectId,
        List<string> originalIds)
    {
        if (originalIds == null || !originalIds.Any())
            throw new ArgumentException("Original IDs list cannot be null or empty", nameof(originalIds));

        // Remove duplicates and filter out null/empty values
        var cleanOriginalIds = originalIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (!cleanOriginalIds.Any())
            throw new ArgumentException("No valid original IDs provided", nameof(originalIds));

        // Query for existing records (excluding archived)
        var existingRecords = await _context.Records
            .Where(r => r.ProjectId == projectId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived
                        && cleanOriginalIds.Contains(r.OriginalId))
            .Include(r => r.Tags)
            .ToListAsync();

        // Check for missing records
        var foundOriginalIds = existingRecords.Select(r => r.OriginalId).ToHashSet();
        var missingOriginalIds = cleanOriginalIds.Where(id => !foundOriginalIds.Contains(id)).ToList();

        if (missingOriginalIds.Any())
            throw new KeyNotFoundException(
                $"Records not found with original IDs: {string.Join(", ", missingOriginalIds)}");

        // Convert to DTOs
        return existingRecords.Select(r => new RecordResponseDto
        {
            Id = r.Id,
            Description = r.Description,
            Uri = r.Uri,
            Properties = r.Properties,
            OriginalId = r.OriginalId,
            Name = r.Name,
            ClassId = r.ClassId,
            DataSourceId = r.DataSourceId,
            ProjectId = r.ProjectId,
            OrganizationId = r.OrganizationId,
            LastUpdatedBy = r.LastUpdatedBy,
            LastUpdatedAt = r.LastUpdatedAt,
            IsArchived = r.IsArchived,
            FileType = r.FileType,
            Tags = r.Tags.Select(t => new RecordTagDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList()
        }).ToList();
    }

    /// <summary>
    ///     Create a new record
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project under which to create the record</param>
    /// <param name="dataSourceId">The ID of the data source under which to create the record</param>
    /// <param name="dto">The data transfer object containing details on the record to be created</param>
    /// <returns>The newly created metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project or datasource are not found</exception>
    /// <exception cref="Exception">Returned if the metadata is too deeply nested</exception>
    public async Task<RecordResponseDto> CreateRecord(long currentUserId, long organizationId, long projectId,
        long dataSourceId,
        CreateRecordRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        if (dto.Properties == null)
            throw new ArgumentNullException(nameof(dto.Properties), "Properties cannot be null");

        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        if (dto.ObjectStorageId != null) await CheckObjectStorageExists(projectId, dto.ObjectStorageId.Value);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var record = new Record
            {
                ProjectId = projectId,
                DataSourceId = dataSourceId,
                Uri = dto.Uri,
                ObjectStorageId = dto.ObjectStorageId,
                Properties = dto.Properties.ToString()!,
                OriginalId = dto.OriginalId,
                Name = dto.Name,
                Description = dto.Description,
                ClassId = dto.ClassId,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId,
                FileType = dto.FileType,
                OrganizationId = organizationId
            };

            _context.Records.Add(record);
            await _context.SaveChangesAsync();

            // We need to handle tag creation/linking separate of record object save
            var tags = await ProcessTags(currentUserId, organizationId, projectId, record.Id, dto.Tags);

            // Log Record Create Event
            await _eventBusiness.CreateEvent(
                currentUserId,
                organizationId,
                projectId,
                new CreateEventRequestDto
                {
                    EntityType = "record",
                    EntityId = record.Id,
                    EntityName = record.Name,
                    Operation = "create",
                    Properties = "{}",
                    DataSourceId = record.DataSourceId
                });

            await transaction.CommitAsync();

            return new RecordResponseDto
            {
                Id = record.Id,
                Description = record.Description,
                Uri = record.Uri,
                Properties = record.Properties,
                ObjectStorageId = record.ObjectStorageId,
                OriginalId = record.OriginalId,
                Name = record.Name,
                ClassId = record.ClassId,
                DataSourceId = record.DataSourceId,
                ProjectId = record.ProjectId,
                OrganizationId = record.OrganizationId,
                LastUpdatedBy = record.LastUpdatedBy,
                LastUpdatedAt = record.LastUpdatedAt,
                IsArchived = record.IsArchived,
                FileType = record.FileType,
                Tags = tags
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw new Exception("Unable to create record and/or create/attach tags");
        }
    }

    /// <summary>
    ///     Create new records
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project under which to create the record</param>
    /// <param name="dataSourceId">The ID of the data source under which to create the record</param>
    /// <param name="records">The data transfer object containing details on the records to be created</param>
    /// <returns>The newly created metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project or datasource are not found</exception>
    /// <exception cref="Exception">Returned if the metadata is too deeply nested</exception>
    public async Task<List<RecordResponseDto>> BulkCreateRecords(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        List<CreateRecordRequestDto> records)
    {
        // Leaving existence check in here for project as this method can be invoked without middleware
        await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId);
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        if (records.Count == 0) throw new Exception("Unable to bulk create records: no records selected for creation");

        // Checks to see if Object Storage Ids reference an existing object storage in the project
        foreach (var dto in records)
            if (dto.ObjectStorageId != null)
                await CheckObjectStorageExists(projectId, dto.ObjectStorageId.Value);

        // Bulk insert into records; if there is an original ID collision, update name, desc, uri, class, and props
        var sql = @"
            INSERT INTO deeplynx.records (project_id, data_source_id, name, description, uri,
                              original_id, properties, class_id, object_storage_id, file_type,
                              last_updated_at, is_archived, last_updated_by, organization_id)
            VALUES {0}
            ON CONFLICT (project_id, data_source_id, original_id) DO UPDATE SET
                name = COALESCE(EXCLUDED.name, records.name),
                description = COALESCE(EXCLUDED.description, records.description),
                uri = COALESCE(EXCLUDED.uri, records.uri),
                properties = COALESCE(EXCLUDED.properties, records.properties),
                class_id = COALESCE(EXCLUDED.class_id, records.class_id),
                object_storage_id = COALESCE(EXCLUDED.object_storage_id, records.object_storage_id),
                last_updated_at = @now,
                last_updated_by = @lastUpdatedBy,
                file_type = COALESCE(EXCLUDED.file_type, records.file_type)
            RETURNING *;                                                          
        ";

        // establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@projectId", projectId),
            new("@dataSourceId", dataSourceId),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new("@lastUpdatedBy", currentUserId),
            new("@organizationId", organizationId)
        };

        // establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(records.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_name", dto.Name),
            new NpgsqlParameter($"@p{i}_desc", dto.Description),
            new NpgsqlParameter($"@p{i}_uri", (object?)dto.Uri ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_props", JsonSerializer.Serialize(dto.Properties)),
            new NpgsqlParameter($"@p{i}_orig", dto.OriginalId),
            new NpgsqlParameter($"@p{i}_class", (object?)dto.ClassId ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_object_storage", (object?)dto.ObjectStorageId ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_file_type", (object?)dto.FileType ?? DBNull.Value)
        }));

        // stringify the params and comma separate them
        var valueTuples = string.Join(", ", records.Select((dto, i) =>
            $"(@projectId, @dataSourceId, @p{i}_name, @p{i}_desc, " +
            $"@p{i}_uri, @p{i}_orig, @p{i}_props::jsonb, @p{i}_class, @p{i}_object_storage, @p{i}_file_type, @now, false, @lastUpdatedBy, @organizationId)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // returns the resulting upserted classes
        var result = await _context.Database
            .SqlQueryRaw<RecordResponseDto>(sql, parameters.ToArray())
            .ToListAsync();

        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "record",
            DataSourceId = dataSourceId
        };
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

        return result;
    }

    /// <summary>
    ///     Archive a metadata record.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record belongs</param>
    /// <param name="recordId">The record to be archived</param>
    /// <returns>Boolean indicating record was archived</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record to archive was not found.</exception>
    public async Task<bool> ArchiveRecord(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var query = _context.Records
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        !r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run archive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the archive record procedure, which archives this record
                // and all child objects with record_id as a foreign key
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.archive_record({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE, {2}::INTEGER)",
                    recordId, lastUpdatedAt, currentUserId
                );

                if (archived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to archive record {recordId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to archive record {recordId} or its downstream dependents: {exc}");
            }
        }

        // Log record soft delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "record",
            EntityId = recordId,
            EntityName = returnedRecord.Name,
            DataSourceId = returnedRecord.DataSourceId,
            Properties = JsonSerializer.Serialize(new { returnedRecord.Name })
        });

        return true;
    }

    /// <summary>
    ///     Unarchive a metadata record.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record belongs</param>
    /// <param name="recordId">The record to be unarchived</param>
    /// <returns>Boolean indicating record was unarchived</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record to unarchive was not found.</exception>
    public async Task<bool> UnarchiveRecord(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var query = _context.Records
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is not archived.");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run unarchive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the unarchive record procedure, which unarchives this record
                // and all child objects with record_id as a foreign key
                var unarchived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.unarchive_record({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE, {2}::INTEGER)",
                    recordId, lastUpdatedAt, currentUserId
                );

                if (unarchived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to unarchive record {recordId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to unarchive record {recordId} or its downstream dependents: {exc}");
            }
        }

        // Log record unarchive event
        await _eventBusiness.CreateEvent(currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "record",
                EntityId = returnedRecord.Id,
                EntityName = returnedRecord.Name,
                DataSourceId = returnedRecord.DataSourceId,
                Properties = JsonSerializer.Serialize(new { returnedRecord.Name })
            });

        return true;
    }

    /// <summary>
    ///     Delete a metadata record.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record belongs</param>
    /// <param name="recordId">The record in question</param>
    /// <returns>Boolean indicating record was deleted</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record to delete was not found.</exception>
    /// TODO: return warning that historical data will be entirely wiped with this action
    public async Task<bool> DeleteRecord(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var query = _context.Records
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var recordName = returnedRecord.Name;
        var recordDataSourceId = returnedRecord.DataSourceId;

        _context.Records.Remove(returnedRecord);
        await _context.SaveChangesAsync();

        // Log record delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "record",
            EntityId = recordId,
            EntityName = recordName,
            DataSourceId = recordDataSourceId,
            Properties = JsonSerializer.Serialize(new { recordName })
        });

        return true;
    }

    /// <summary>
    ///     Updates a record with new information
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to be updated</param>
    /// <param name="dto">The data transfer object containing details on the record to be updated</param>
    /// <returns>The newly updated metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if record to be updated is not found</exception>
    public async Task<RecordResponseDto> UpdateRecord(long currentUserId, long organizationId, long projectId,
        long recordId,
        UpdateRecordRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var query = _context.Records
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        !r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        if (dto.ObjectStorageId != null) await CheckObjectStorageExists(projectId, dto.ObjectStorageId.Value);

        returnedRecord.Uri = dto.Uri ?? returnedRecord.Uri;
        returnedRecord.Properties = dto.Properties != null ? dto.Properties.ToString() : returnedRecord.Properties;
        returnedRecord.OriginalId = dto.OriginalId ?? returnedRecord.OriginalId;
        returnedRecord.ObjectStorageId = dto.ObjectStorageId ?? returnedRecord.ObjectStorageId;
        returnedRecord.Name = dto.Name ?? returnedRecord.Name;
        returnedRecord.Description = dto.Description ?? returnedRecord.Description;
        returnedRecord.ClassId = dto.ClassId ?? returnedRecord.ClassId;
        returnedRecord.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedRecord.LastUpdatedBy = currentUserId;
        returnedRecord.FileType = dto.FileType ?? returnedRecord.FileType;

        _context.Records.Update(returnedRecord);
        await _context.SaveChangesAsync();

        // Log Record Update Event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            EntityType = "record",
            EntityId = returnedRecord.Id,
            EntityName = returnedRecord.Name,
            Operation = "update",
            Properties = "{}",
            DataSourceId = returnedRecord.DataSourceId
        });

        return new RecordResponseDto
        {
            Id = returnedRecord.Id,
            Description = returnedRecord.Description,
            Uri = returnedRecord.Uri,
            Properties = returnedRecord.Properties,
            ObjectStorageId = returnedRecord.ObjectStorageId,
            OriginalId = returnedRecord.OriginalId,
            Name = returnedRecord.Name,
            ClassId = returnedRecord.ClassId,
            DataSourceId = returnedRecord.DataSourceId,
            ProjectId = returnedRecord.ProjectId,
            OrganizationId = returnedRecord.OrganizationId,
            LastUpdatedBy = returnedRecord.LastUpdatedBy,
            LastUpdatedAt = returnedRecord.LastUpdatedAt,
            IsArchived = returnedRecord.IsArchived,
            FileType = returnedRecord.FileType
        };
    }

    /// <summary>
    ///     Get record count for a data source
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project of the records to retrieve</param>
    /// <param name="dataSourceId">The ID of the data source by which to filter records</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given data source</returns>
    public async Task<int> GetRecordsCountByDataSource(
        long organizationId, long projectId, long dataSourceId, bool hideArchived)
    {
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId, hideArchived);
        var recordQuery = _context.Records
            .Where(r => r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        r.DataSourceId == dataSourceId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        return await recordQuery.CountAsync();
    }

    /// <summary>
    ///     Private method used to calculate json depth of properties (should be less than three)
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private int CalculateJsonMaxDepth(JsonNode? node)
    {
        if (node is not JsonObject && node is not JsonArray)
            return 0;

        var maxDepth = 0;
        if (node is JsonObject jsonObject)
            foreach (var prop in jsonObject)
            {
                var depth = CalculateJsonMaxDepth(prop.Value);
                if (depth > maxDepth)
                    maxDepth = depth;
            }
        else if (node is JsonArray jsonArray)
            foreach (var item in jsonArray)
            {
                var depth = CalculateJsonMaxDepth(item);
                if (depth > maxDepth)
                    maxDepth = depth;
            }

        return maxDepth + 1;
    }

    private async Task CheckObjectStorageExists(long projectId, long objectStorageId)
    {
        var referencedObjectStorage =
            await _context.ObjectStorages.FirstOrDefaultAsync(o => o.ProjectId == projectId && o.Id == objectStorageId);
        if (referencedObjectStorage == null)
            throw new KeyNotFoundException($"Object storage with ID {objectStorageId} does not exist");
    }

    private async Task<ICollection<RecordTagDto>> ProcessTags(long currentUserId, long organizationId, long projectId,
        long recordId,
        List<string>? tags)
    {
        // Handle tags if provided
        if (tags == null || !tags.Any())
            return new List<RecordTagDto>();

        // Deduplicate tags before processing
        var distinctTags = tags.Distinct().ToList();

        var tagsToInsert = distinctTags.Select(t => new CreateTagRequestDto { Name = t }).ToList();
        var tagMap = await BulkUpsertTags(organizationId, currentUserId, projectId, tagsToInsert);

        var recordTags = distinctTags
            .Where(tag => tagMap.ContainsKey(tag))
            .Select(tag => new RecordTagLinkDto
            {
                RecordId = recordId,
                TagId = tagMap[tag].Id
            })
            .ToList();

        if (recordTags.Any()) await BulkAttachTags(recordTags);

        // Convert tagMap to RecordTagDto collection
        return distinctTags
            .Where(tag => tagMap.ContainsKey(tag))
            .Select(tag => new RecordTagDto
            {
                Id = tagMap[tag].Id,
                Name = tagMap[tag].Name
            })
            .ToList();
    }

    private async Task<Dictionary<string, TagResponseDto>> BulkUpsertTags(
        long organizationId,
        long currentUserId,
        long projectId,
        List<CreateTagRequestDto> tags)
    {
        var inserted = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, projectId, tags);
        return inserted.ToDictionary(t => t.Name, t => t);
    }
}