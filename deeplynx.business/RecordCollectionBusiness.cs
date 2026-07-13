using System.Data;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace deeplynx.business;

public class RecordCollectionBusiness : IRecordCollectionBusiness
{
    private readonly IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor;
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly ISensitivityLabelBusiness _labelBusiness;
    private readonly ISensitivityLabelService _sensitivityLabelService;
    private readonly ITagBusiness _tagBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the record operations.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    /// <param name="bulkCopyUpsertExecutor">Executor for efficient database inserts for bulk operations</param>
    /// <param name="tagBusiness">Used for creating tags related to a record.</param>
    /// <param name="labelBusiness">Used for creating tags related to a record.</param>
    /// <param name="sensitivityLabelService">Service for sensitivity label authorization operations.</param>
    public RecordCollectionBusiness(
        DeeplynxContext context,
        IEventBusiness eventBusiness,
        IBulkCopyUpsertExecutor bulkCopyUpsertExecutor,
        ITagBusiness tagBusiness,
        ISensitivityLabelBusiness labelBusiness,
        ISensitivityLabelService sensitivityLabelService)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _tagBusiness = tagBusiness;
        _bulkCopyUpsertExecutor = bulkCopyUpsertExecutor;
        _labelBusiness = labelBusiness;
        _sensitivityLabelService = sensitivityLabelService;
    }

    /// <summary>
    ///     Retrieves all record collections for a specific project and datasource.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="dto">The data transfer object of the search parameters and pagination</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of record collections based on the applied filters.</returns>
    public async Task<PaginatedResponse<RecordCollectionResponseDto>> GetAllRecordCollections(
        long currentUserId, long organizationId, long projectId, RecordCollectionQueryRequestDto dto,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var search = dto.Search?.Trim();


        var recordCollectionQuery = _context.RecordCollections
            .Where(c => c.ProjectId == projectId && c.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{search}%";

            recordCollectionQuery = recordCollectionQuery.Where(c =>
                            EF.Functions.ILike(c.Name, searchPattern) ||
                            (c.Description != null && EF.Functions.ILike(c.Description, searchPattern)) ||
                            c.Labels.Any(l => EF.Functions.ILike(l.Name, searchPattern)) ||
                            c.Tags.Any(t => EF.Functions.ILike(t.Name, searchPattern)));
        }

        if (hideArchived) recordCollectionQuery = recordCollectionQuery.Where(c => !c.IsArchived);

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            recordCollectionQuery = recordCollectionQuery.Where(c =>
                c.Labels.Count == 0 ||
                c.Labels.All(l => userAuthorizedLabels.Contains(l.Id)));
        }

        if (dto.SensitivityLabelIds?.Length > 0)
        {
            foreach (var labelId in dto.SensitivityLabelIds.Distinct())
            {
                recordCollectionQuery = recordCollectionQuery.Where(c =>
                    c.Labels.Any(l => l.Id == labelId));
            }
        }

        if (dto.TagIds?.Length > 0)
        {
            foreach (var tagId in dto.TagIds.Distinct())
            {
                recordCollectionQuery = recordCollectionQuery.Where(c => c.Tags.Any(t => t.Id == tagId));
            }
        }

        recordCollectionQuery = dto.Sort switch
        {
            "alphabeticalAsc" => recordCollectionQuery.OrderBy(c => c.Name),
            "alphabeticalDesc" => recordCollectionQuery.OrderByDescending(c => c.Name),
            "recordCountAsc" => recordCollectionQuery.OrderBy(c => c.Records.Count).ThenBy(c => c.Name),
            "recordCountDesc" => recordCollectionQuery.OrderByDescending(c => c.Records.Count).ThenBy(c => c.Name),
            "updatedAsc" => recordCollectionQuery.OrderBy(c => c.LastUpdatedAt).ThenBy(c => c.Name),
            "updatedDesc" => recordCollectionQuery.OrderByDescending(c => c.LastUpdatedAt).ThenBy(c => c.Name),
            _ => recordCollectionQuery.OrderByDescending(c => c.LastUpdatedAt).ThenBy(c => c.Name),
        };

        // Get total count before pagination
        var totalCount = await recordCollectionQuery.CountAsync();

        // Get pagination values
        var pageNumber = Math.Max(1, dto.PageNumber);
        var pageSize = dto.GetValidatedPageSize();

        // Apply pagination and execute query
        var items = await recordCollectionQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new RecordCollectionResponseDto
            {
                Id = c.Id,
                Description = c.Description,
                Properties = c.Properties,
                Name = c.Name,
                ProjectId = c.ProjectId,
                OrganizationId = c.OrganizationId,
                LastUpdatedBy = c.LastUpdatedBy,
                LastUpdatedAt = c.LastUpdatedAt,
                IsArchived = c.IsArchived,
                RecordCount = c.Records.Count(),
                Tags = c.Tags.Select(t => new RecordCollectionTagDto
                {
                    Id = t.Id,
                    Name = t.Name
                }).ToList(),
                Labels = c.Labels.Select(l => new RecordCollectionLabelDto
                {
                    Id = l.Id,
                    Name = l.Name
                }).ToList()
            }).ToListAsync();

        return new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };


    }

    /// <summary>
    /// Retrieves all authorized records in a specific record collection.
    /// </summary>
    /// <param name="currentUserId"></param>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordCollectionId"></param>
    /// <param name="hideArchived"></param>
    /// <param name="isSysAdmin"></param>
    /// <param name="isOrgAdmin"></param>
    /// <param name="isProjectAdmin"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<List<RecordResponseDto>> GetRecordsInRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var collectionExists = await _context.RecordCollections.AnyAsync(c =>
            c.Id == recordCollectionId &&
            c.OrganizationId == organizationId &&
            c.ProjectId == projectId &&
            (!hideArchived || !c.IsArchived));

        if (!collectionExists)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found");

        var recordQuery = _context.Records
            .Where(r =>
                r.OrganizationId == organizationId &&
                r.ProjectId == projectId &&
                r.RecordCollections.Any(c => c.Id == recordCollectionId));

        if (hideArchived)
            recordQuery = recordQuery.Where(r => !r.IsArchived);

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            recordQuery = recordQuery.Where(r =>
                r.Labels.Count == 0 ||
                r.Labels.All(l => userAuthorizedLabels.Contains(l.Id)));
        }

        var records = await recordQuery
            .Include(r => r.Tags)
            .Include(r => r.Labels)
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
            }).ToList(),
            Labels = r.Labels.Select(l => new RecordLabelDto
            {
                Id = l.Id,
                Name = l.Name
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Retrieves all authorized record collections for a specific record.
    /// </summary>
    /// <param name="currentUserId"></param>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordId"></param>
    /// <param name="hideArchived"></param>
    /// <param name="isSysAdmin"></param>
    /// <param name="dto">The data transfer object of the search parameters and pagination</param>
    /// <param name="isOrgAdmin"></param>
    /// <param name="isProjectAdmin"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<PaginatedResponse<RecordCollectionResponseDto>> GetRecordCollectionsForRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived,
        RecordCollectionQueryRequestDto dto, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var recordExists = await _context.Records.AnyAsync(r =>
            r.Id == recordId &&
            r.OrganizationId == organizationId &&
            r.ProjectId == projectId &&
            (!hideArchived || !r.IsArchived));

        if (!recordExists)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var collectionQuery = _context.RecordCollections
            .Where(c =>
                c.OrganizationId == organizationId &&
                c.ProjectId == projectId &&
                c.Records.Any(r => r.Id == recordId));

        if (hideArchived)
            collectionQuery = collectionQuery.Where(c => !c.IsArchived);

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            collectionQuery = collectionQuery.Where(c =>
                c.Labels.Count == 0 ||
                c.Labels.All(l => userAuthorizedLabels.Contains(l.Id)));
        }

        // Get total count before pagination
        var totalCount = await collectionQuery.CountAsync();

        // Get pagination values
        var pageNumber = Math.Max(1, dto.PageNumber);
        var pageSize = dto.GetValidatedPageSize();

        var items = await collectionQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new RecordCollectionResponseDto
            {
                Id = c.Id,
                Description = c.Description,
                Properties = c.Properties,
                Name = c.Name,
                ProjectId = c.ProjectId,
                OrganizationId = c.OrganizationId,
                LastUpdatedBy = c.LastUpdatedBy,
                LastUpdatedAt = c.LastUpdatedAt,
                IsArchived = c.IsArchived,
                RecordCount = c.Records.Count(),
                Tags = c.Tags.Select(t => new RecordCollectionTagDto
                {
                    Id = t.Id,
                    Name = t.Name
                }).ToList(),
                Labels = c.Labels.Select(l => new RecordCollectionLabelDto
                {
                    Id = l.Id,
                    Name = l.Name
                }).ToList()
            }).ToListAsync();

        return new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

    }

    /// <summary>
    ///     Get all records that contain all given tags
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="tagIds">List of tag IDs - returned records must contain every given ID</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns></returns>
    public async Task<List<RecordCollectionResponseDto>> GetRecordCollectionsByTags(
        long currentUserId, long organizationId, long projectId, long[] tagIds, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var recordCollectionQuery = _context.RecordCollections
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (hideArchived) recordCollectionQuery = recordCollectionQuery.Where(r => !r.IsArchived);

        // Only return records that contain ALL given IDs
        recordCollectionQuery = recordCollectionQuery.Where(r =>
            tagIds.All(tagId => r.Tags.Any(t => t.Id == tagId)));

        // if user is not admin, filter out unauthorized labels
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            recordCollectionQuery = recordCollectionQuery.Where(r =>
                r.Labels.Count == 0 ||
                r.Labels.All(l => userAuthorizedLabels.Contains(l.Id)));
        }

        return await recordCollectionQuery
            .Select(r => new RecordCollectionResponseDto
            {
                Id = r.Id,
                Description = r.Description,
                Properties = r.Properties,
                Name = r.Name,
                ProjectId = r.ProjectId,
                OrganizationId = r.OrganizationId,
                LastUpdatedBy = r.LastUpdatedBy,
                LastUpdatedAt = r.LastUpdatedAt,
                IsArchived = r.IsArchived,
                RecordCount = r.Records.Count(),
                Tags = r.Tags.Select(t => new RecordCollectionTagDto
                {
                    Id = t.Id,
                    Name = t.Name
                }).ToList(),
                Labels = r.Labels.Select(l => new RecordCollectionLabelDto
                {
                    Id = l.Id,
                    Name = l.Name
                }).ToList()
            }).ToListAsync();
    }


    /// <summary>
    ///     Add Records to Record Collection
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records and collections belong</param>
    /// <param name="recordCollectionId">The ID of the collection where the records will be added</param>
    /// <param name="recordIds">The IDs for the records to be added to the collection</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns></returns>
    public async Task<bool> AddRecordsToRecordCollection(long currentUserId, long organizationId,
    long projectId, long recordCollectionId, long[] recordIds, bool isSysAdmin = false,
    bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (recordIds.Length == 0)
            throw new ArgumentException("Record IDs list cannot be null or empty", nameof(recordIds));

        var distinctRecordIds = recordIds.Distinct().ToList();

        // no need to search record collections by authorized labels since middleware checks it using id
        var collection = await _context.RecordCollections
            .Where(c =>
                c.Id == recordCollectionId &&
                c.OrganizationId == organizationId &&
                c.ProjectId == projectId &&
                !c.IsArchived)
            .Include(c => c.Records)
            .Include(c => c.Labels)
            .FirstOrDefaultAsync();

        if (collection == null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        var authorizedRecordIds = distinctRecordIds.ToHashSet();
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            authorizedRecordIds = await _context.Records
                .Where(r => distinctRecordIds.Contains(r.Id) &&
                            r.OrganizationId == organizationId &&
                            r.ProjectId == projectId &&
                            !r.IsArchived &&
                            (!r.Labels.Any() || r.Labels.All(l => userAuthorizedLabels.Contains(l.Id))))
                .Select(r => r.Id)
                .ToHashSetAsync();

            var unauthorizedRecordIds = distinctRecordIds
                .Where(id => !authorizedRecordIds.Contains(id))
                .ToList();

            if (unauthorizedRecordIds.Any())
                throw new UnauthorizedAccessException(
                    $"These records do not exist, or you do not have access to them: {string.Join(", ", unauthorizedRecordIds)}");
        }

        var records = await _context.Records
            .Include(r => r.Labels)
            .Where(r =>
                distinctRecordIds.Contains(r.Id) &&
                r.OrganizationId == organizationId &&
                r.ProjectId == projectId &&
                !r.IsArchived)
            .ToListAsync();

        var foundRecordIds = records.Select(r => r.Id).ToHashSet();
        var missingRecordIds = distinctRecordIds.Where(id => !foundRecordIds.Contains(id)).ToList();

        if (missingRecordIds.Count > 0)
            throw new KeyNotFoundException(
                $"Records not found, archived, or outside this organization/project: {string.Join(", ", missingRecordIds)}");

        foreach (var record in records)
        {
            if (collection.Records.All(existing => existing.Id != record.Id))
            {
                collection.Records.Add(record);

                foreach (var label in record.Labels.Except(collection.Labels))
                {
                    collection.Labels.Add(label);
                }
            }

        }

        collection.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        collection.LastUpdatedBy = currentUserId;

        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Remove Records from Data Collection
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records and collections belong</param>
    /// <param name="recordCollectionId">The ID of the collection where the records will be removed</param>
    /// <param name="recordIds">The IDs for the records to be removed from the collection</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns></returns>
    public async Task<bool> RemoveRecordsFromRecordCollection(
    long currentUserId,
    long organizationId,
    long projectId,
    long recordCollectionId,
    long[] recordIds,
    bool isSysAdmin = false,
    bool isOrgAdmin = false,
    bool isProjectAdmin = false)
    {
        // 1. Validate input
        if (recordIds == null || !recordIds.Any())
            throw new ArgumentException("Record IDs list cannot be null or empty", nameof(recordIds));

        var distinctRecordIds = recordIds.Distinct().ToList();

        // 2. Check collection exists first
        var collection = await _context.RecordCollections
            .Where(c => c.Id == recordCollectionId && c.OrganizationId == organizationId &&
                        c.ProjectId == projectId && !c.IsArchived)
            .Include(c => c.Records)
            .FirstOrDefaultAsync();

        if (collection == null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        // 3. Check authorization
        var authorizedRecordIds = distinctRecordIds.ToHashSet();
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            authorizedRecordIds = await _context.Records
                .Where(r => distinctRecordIds.Contains(r.Id) &&
                            r.OrganizationId == organizationId &&
                            r.ProjectId == projectId &&
                            !r.IsArchived &&
                            (!r.Labels.Any() || r.Labels.All(l => userAuthorizedLabels.Contains(l.Id))))
                .Select(r => r.Id)
                .ToHashSetAsync();

            var unauthorizedRecordIds = distinctRecordIds
                .Where(id => !authorizedRecordIds.Contains(id))
                .ToList();

            if (unauthorizedRecordIds.Any())
                throw new UnauthorizedAccessException(
                    $"These records do not exist, or you do not have access to them: {string.Join(", ", unauthorizedRecordIds)}");
        }

        // 4. Check all records exist on the collection
        var collectionRecordIds = collection.Records.Select(r => r.Id).ToHashSet();
        var notInCollectionIds = distinctRecordIds.Where(id => !collectionRecordIds.Contains(id)).ToList();

        if (notInCollectionIds.Any())
            throw new KeyNotFoundException(
                $"Records not found in collection: {string.Join(", ", notInCollectionIds)}");

        // 5. Remove
        foreach (var record in collection.Records.Where(r => authorizedRecordIds.Contains(r.Id)).ToList())
        {
            collection.Records.Remove(record);
        }

        collection.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        collection.LastUpdatedBy = currentUserId;

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
        if (!dtos.Any())
            return true;

        // Bulk insert into record_tags
        var sql = @"INSERT INTO deeplynx.record_collection_tags (record_collection_id, tag_id) VALUES {0} ON CONFLICT DO NOTHING;";

        // establish parameters
        var parameters = new List<NpgsqlParameter>();
        parameters.AddRange(dtos.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@record{i}_id", dto.RecordId),
            new NpgsqlParameter($"@tag{i}_id", dto.TagId)
        }));

        // stringify params and comma separate them
        var valueTuples = string.Join(", ", dtos.Select((_, i) => $"(@record{i}_id, @tag{i}_id)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        return true;
    }

    /// <summary>
    ///     Create a new record collection
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project under which to create the record</param>
    /// <param name="dto">The data transfer object containing details on the record to be created</param>
    /// <param name="sensitivityLabelIds">sensitivity labels to apply to the collection on creation</param>
    /// <returns>The newly created metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project or datasource are not found</exception>
    /// <exception cref="Exception">Returned if the metadata is too deeply nested</exception>
    public async Task<RecordCollectionResponseDto> CreateRecordCollection(long currentUserId, long organizationId, long projectId, List<long>? sensitivityLabelIds,
     CreateRecordCollectionRequestDto dto)
    {
        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        List<SensitivityLabel> sensitivityLabelsToAdd = new List<SensitivityLabel>();

        if (sensitivityLabelIds != null && sensitivityLabelIds.Any())
        {
            sensitivityLabelsToAdd = await _context.SensitivityLabels
                .Where(sl => sensitivityLabelIds.Contains(sl.Id) &&
                             (sl.ProjectId == projectId ||
                              (sl.ProjectId == null && sl.OrganizationId == organizationId)))
                .ToListAsync();

            var missingLabelIds = sensitivityLabelIds
                .Where(id => sensitivityLabelsToAdd.All(sl => sl.Id != id))
                .ToList();

            if (missingLabelIds.Any())
                throw new KeyNotFoundException(
                    $"Sensitivity labels not found inside this organization/project: {string.Join(", ", missingLabelIds)}");
        }

        try
        {
            var collection = new RecordCollection
            {
                ProjectId = projectId,
                Properties = dto.Properties.ToString()!,
                Name = dto.Name,
                Description = dto.Description,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId,
                OrganizationId = organizationId,
                Labels = sensitivityLabelsToAdd
            };

            _context.RecordCollections.Add(collection);
            await _context.SaveChangesAsync();

            // Process tags (can be created on-the-fly)
            var tags = await ProcessTags(
                currentUserId, organizationId, projectId, collection.Id, dto.Tags);

            // Log Record Collection Create Event
            await _eventBusiness.CreateEvent(
                currentUserId,
                organizationId,
                projectId,
                new CreateEventRequestDto
                {
                    EntityType = "record_collection",
                    EntityId = collection.Id,
                    EntityName = collection.Name,
                    Operation = "create",
                    Properties = "{}"
                });

            await transaction.CommitAsync();

            return new RecordCollectionResponseDto
            {
                Id = collection.Id,
                Description = collection.Description,
                Properties = collection.Properties,
                Name = collection.Name,
                ProjectId = collection.ProjectId,
                OrganizationId = collection.OrganizationId,
                LastUpdatedBy = collection.LastUpdatedBy,
                LastUpdatedAt = collection.LastUpdatedAt,
                IsArchived = collection.IsArchived,
                RecordCount = 0,
                Tags = tags,
                Labels = collection.Labels.Select(l => new RecordCollectionLabelDto
                {
                    Id = l.Id,
                    Name = l.Name
                }).ToList()
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Update a record collection
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="dto">Data Transfer Object for the update request</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record collection is not found</exception>
    /// <exception cref="Exception">Thrown if the property depth is too large</exception>
    public async Task<RecordCollectionResponseDto> UpdateRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId, UpdateRecordCollectionRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var query = _context.RecordCollections
            .Where(c => c.Id == recordCollectionId && c.OrganizationId == organizationId && c.ProjectId == projectId && !c.IsArchived);

        var returnedRecordCollection = await query.FirstOrDefaultAsync();

        if (returnedRecordCollection is null)
            throw new KeyNotFoundException($"Record Collection with ID {recordCollectionId} not found.");

        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        returnedRecordCollection.Properties = dto.Properties != null ? dto.Properties.ToString() : returnedRecordCollection.Properties;
        returnedRecordCollection.Name = dto.Name ?? returnedRecordCollection.Name;
        returnedRecordCollection.Description = dto.Description ?? returnedRecordCollection.Description;
        returnedRecordCollection.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedRecordCollection.LastUpdatedBy = currentUserId;

        _context.RecordCollections.Update(returnedRecordCollection);
        await _context.SaveChangesAsync();

        var recordCount = await _context.RecordCollections
            .Where(c => c.Id == returnedRecordCollection.Id)
            .Select(c => c.Records.Count())
            .FirstAsync();

        // Log Record Update Event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            EntityType = "record_collection",
            EntityId = returnedRecordCollection.Id,
            EntityName = returnedRecordCollection.Name,
            Operation = "update",
            Properties = "{}",
        });

        return new RecordCollectionResponseDto
        {
            Id = returnedRecordCollection.Id,
            Description = returnedRecordCollection.Description,
            Properties = returnedRecordCollection.Properties,
            Name = returnedRecordCollection.Name,
            ProjectId = returnedRecordCollection.ProjectId,
            OrganizationId = returnedRecordCollection.OrganizationId,
            LastUpdatedBy = returnedRecordCollection.LastUpdatedBy,
            LastUpdatedAt = returnedRecordCollection.LastUpdatedAt,
            IsArchived = returnedRecordCollection.IsArchived,
            RecordCount = recordCount,
        };
    }

    /// <summary>
    ///     Attaches a tag to a record collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or tag are not found</exception>
    /// <exception cref="Exception">Thrown if the tag is already attached to the record</exception>
    public async Task<bool> AttachTag(long organizationId, long projectId, long recordCollectionId,
        long tagId)
    {
        var recordCollection = await _context.RecordCollections
            .Where(c => c.ProjectId == projectId
                        && c.Id == recordCollectionId
                        && c.OrganizationId == organizationId
                        && !c.IsArchived)
            .Include(r => r.Tags)
            .FirstOrDefaultAsync();

        if (recordCollection == null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        // Check if already attached
        var alreadyAttached = recordCollection.Tags.Any(t => t.Id == tagId);
        if (alreadyAttached)
            throw new InvalidOperationException($"Tag with id {tagId} is already attached to record collection {recordCollectionId}");

        // Fetch and validate tag in one query with all conditions
        var tag = await _context.Tags
            .Where(t => t.Id == tagId
                        && t.OrganizationId == organizationId
                        && (t.ProjectId == projectId || t.ProjectId == null)
                        && !t.IsArchived)
            .FirstOrDefaultAsync();

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} not found, is archived, or does not belong to this organization/project.");

        recordCollection.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Attach a label to a record collection
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordCollectionId"></param>
    /// <param name="labelId"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<bool> AttachLabel(long organizationId, long projectId, long recordCollectionId,
        long labelId)
    {
        var recordCollection = await _context.RecordCollections
            .Where(c => c.Id == recordCollectionId
                        && c.ProjectId == projectId
                        && c.OrganizationId == organizationId
                        && !c.IsArchived)
            .Include(c => c.Labels)
            .FirstOrDefaultAsync();

        if (recordCollection == null)
            throw new KeyNotFoundException($"Record Collection with id {recordCollectionId} not found or is archived.");

        var alreadyAttached = recordCollection.Labels.Any(l => l.Id == labelId);
        if (alreadyAttached)
            throw new InvalidOperationException($"Label with id {labelId} is already attached to record collection {recordCollectionId}");

        var label = await _context.SensitivityLabels
            .Where(l => l.Id == labelId
                    && l.OrganizationId == organizationId
                    && (l.ProjectId == projectId || l.ProjectId == null)
                    && !l.IsArchived)
            .FirstOrDefaultAsync();

        if (label == null)
            throw new KeyNotFoundException(
                $"Label with id {labelId} not found, is archived, or does not belong to this organization/project.");

        recordCollection.Labels.Add(label);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Unattach a tag from a record collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record collection or tag are not found</exception>
    public async Task<bool> UnattachTag(long organizationId, long projectId, long recordCollectionId,
        long tagId)
    {
        var recordCollection = await _context.RecordCollections
            .Where(c => c.ProjectId == projectId
                        && c.Id == recordCollectionId
                        && c.OrganizationId == organizationId
                        && !c.IsArchived)
            .Include(c => c.Tags)
            .FirstOrDefaultAsync();

        if (recordCollection == null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        var tag = recordCollection.Tags.FirstOrDefault(t => t.Id == tagId);

        if (tag == null)
            throw new KeyNotFoundException(
                $"Tag with id {tagId} is not attached to record collection {recordCollectionId}");

        recordCollection.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Unattach a sensitivity label from a record collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and sensitivity label</param>
    /// <param name="recordCollectionId">The ID of the record</param>
    /// <param name="labelId">The ID of the label</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or sensitivity label are not found</exception>
    public async Task<bool> UnattachLabel(long organizationId, long projectId, long recordCollectionId,
        long labelId)
    {
        var sensitivityLabelRequired =
            await _sensitivityLabelService.IsSensitivityLabelRequired(organizationId, projectId);

        var recordCollection = await _context.RecordCollections
            .Where(c => c.ProjectId == projectId
                        && c.Id == recordCollectionId
                        && c.OrganizationId == organizationId
                        && !c.IsArchived)
            .Include(c => c.Labels)
            .FirstOrDefaultAsync();

        if (recordCollection == null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        var label = recordCollection.Labels.FirstOrDefault(t => t.Id == labelId);

        if (label == null)
            throw new KeyNotFoundException($"Label with id {labelId} is not attached to record collection {recordCollectionId}");

        if (label.IsArchived ||
            label.OrganizationId != organizationId ||
            (label.ProjectId.HasValue && label.ProjectId != projectId))
            throw new InvalidOperationException(
                $"Label with id {labelId} is archived or does not belong to this organization/project.");

        if (sensitivityLabelRequired && recordCollection.Labels.Count == 1)
            throw new InvalidOperationException(
                "Sensitivity labels are required on all record collections. Add a new label first to remove this one");

        recordCollection.Labels.Remove(label);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Archive a record collection.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The record collection to be archived</param>
    /// <returns>Boolean indicating record collection was archived</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record to archive was not found.</exception>
    public async Task<bool> ArchiveRecordCollection(long currentUserId, long organizationId, long projectId, long recordCollectionId)
    {
        var returnedRecordCollection = await _context.RecordCollections
            .Include(c => c.Labels)
            .Where(c => c.Id == recordCollectionId && c.OrganizationId == organizationId && c.ProjectId == projectId &&
                        !c.IsArchived).FirstOrDefaultAsync();

        if (returnedRecordCollection is null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is already archived.");

        returnedRecordCollection.IsArchived = true;
        await _context.SaveChangesAsync();

        // Log record collection soft delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "record_collection",
            EntityId = recordCollectionId,
            EntityName = returnedRecordCollection.Name,
            Properties = JsonSerializer.Serialize(new { returnedRecordCollection.Name })
        });

        return true;
    }

    /// <summary>
    ///     Unarchive a record collection.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The record collection to be unarchived</param>
    /// <returns>Boolean indicating record collection was unarchived</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record collection to unarchive was not found.</exception>
    public async Task<bool> UnarchiveRecordCollection(long currentUserId, long organizationId, long projectId, long recordCollectionId)
    {
        var returnedRecordCollection = await _context.RecordCollections
            .Include(c => c.Labels)
            .Where(c => c.Id == recordCollectionId && c.OrganizationId == organizationId && c.ProjectId == projectId &&
                        c.IsArchived).FirstOrDefaultAsync();

        if (returnedRecordCollection is null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is not archived.");

        returnedRecordCollection.IsArchived = false;
        await _context.SaveChangesAsync();

        // Log record unarchive event
        await _eventBusiness.CreateEvent(currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "record_collection",
                EntityId = returnedRecordCollection.Id,
                EntityName = returnedRecordCollection.Name,
                Properties = JsonSerializer.Serialize(new { returnedRecordCollection.Name })
            });

        return true;
    }


    /// <summary>
    ///     Delete a record collection.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project to which the record belongs</param>
    /// <param name="recordCollectionId">The record collection to delete</param>
    /// <returns>Boolean indicating record was deleted</returns>
    /// <exception cref="KeyNotFoundException">Returned if the record to delete was not found.</exception>
    public async Task<bool> DeleteRecordCollection(long currentUserId, long organizationId, long projectId, long recordCollectionId)
    {
        var returnedRecordCollection = await _context.RecordCollections
            .Where(c => c.Id == recordCollectionId
                        && c.OrganizationId == organizationId
                        && c.ProjectId == projectId).FirstOrDefaultAsync();

        if (returnedRecordCollection is null)
            throw new KeyNotFoundException($"Record Collection with id {recordCollectionId} is not found");

        var recordCollectionName = returnedRecordCollection.Name;

        _context.RecordCollections.Remove(returnedRecordCollection);
        await _context.SaveChangesAsync();

        // Log record delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "record_collection",
            EntityId = recordCollectionId,
            EntityName = recordCollectionName,
            Properties = JsonSerializer.Serialize(new { recordCollectionName })
        });

        return true;
    }

    /// <summary>
    /// Get Sensitivity Labels for Record Collection
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordCollectionId"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<List<SensitivityLabel>> GetSensitivityLabelsForRecordCollection(long organizationId,
        long projectId, long recordCollectionId)
    {
        var recordCollection = await _context.RecordCollections
            .Include(rc => rc.Labels)
            .Where(rc => rc.Id == recordCollectionId
                         && rc.OrganizationId == organizationId
                         && rc.ProjectId == projectId
                         && !rc.IsArchived)
            .FirstOrDefaultAsync();

        if (recordCollection is null)
            throw new KeyNotFoundException($"Record collection with id {recordCollectionId} not found or is archived.");

        return recordCollection.Labels.ToList();
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

    private async Task<ICollection<RecordCollectionTagDto>> ProcessTags(long currentUserId, long organizationId,
        long projectId,
        long recordId,
        List<string>? tags)
    {
        // Handle tags if provided
        if (tags == null || !tags.Any())
            return new List<RecordCollectionTagDto>();

        // Filter out empty or whitespace strings
        tags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        if (!tags.Any())
            return new List<RecordCollectionTagDto>();

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
            .Select(tag => new RecordCollectionTagDto
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
