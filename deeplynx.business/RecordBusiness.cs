using System.Data;
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

public class RecordBusiness : IRecordBusiness
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
    public RecordBusiness(
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
    ///     Retrieves all records for a specific project and datasource.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="fileType">File extension to filter by (e.g., pdf, png, jpg)</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of records based on the applied filters.</returns>
    public async Task<List<RecordResponseDto>> GetAllRecords(
        long currentUserId, long organizationId, long projectId, long? dataSourceId, bool hideArchived,
        string? fileType = null, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
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
        
        // if user is not admin, filter out unauthorized labels
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");
            
            recordQuery = recordQuery.WithAuthorizedLabels(userAuthorizedLabels);
        }

        var records = await recordQuery
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .ToListAsync();

        return records.Select(r => new RecordResponseDto
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
            FileSize = r.FileSize,
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
///     Retrieves all records for a specific project with pagination.
/// </summary>
/// <param name="currentUserId">The ID of current user</param>
/// <param name="organizationId">The ID of the organization to which the project belongs</param>
/// <param name="projectId">The ID of the project whose records are to be retrieved</param>
/// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
/// <param name="queryDto">Filter criteria and pagination parameters</param>
/// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
/// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
/// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
/// <returns>Paginated response containing records and pagination metadata</returns>
public async Task<PaginatedResponse<RecordResponseDto>> GetAllRecordsPaginated(
    long currentUserId, long organizationId, long projectId, bool hideArchived,
    RecordQueryRequestDto? queryDto,
    bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
{
    var recordQuery = _context.Records
        .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId)
        .AsQueryable();

    if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

    if (queryDto != null)
    {
        if (queryDto.DataSourceId.HasValue)
            recordQuery = recordQuery.Where(r => r.DataSourceId == queryDto.DataSourceId);

        if (!string.IsNullOrWhiteSpace(queryDto.FileType))
        {
            var formattedFileType = queryDto.FileType.TrimStart('.').ToLower();
            recordQuery = recordQuery.Where(r => r.FileType == formattedFileType);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Name))
            recordQuery = recordQuery.Where(r => EF.Functions.ILike(r.Name, $"%{queryDto.Name.Trim()}%"));

        if (queryDto.ClassId.HasValue)
            recordQuery = recordQuery.Where(r => r.ClassId == queryDto.ClassId);

        if (queryDto.StartDate.HasValue)
            recordQuery = recordQuery.Where(r => r.LastUpdatedAt >= queryDto.StartDate.Value);

        if (queryDto.EndDate.HasValue)
            recordQuery = recordQuery.Where(r => r.LastUpdatedAt <= queryDto.EndDate.Value);
    }

    // if user is not admin, filter out unauthorized labels
    if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
    {
        var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
            currentUserId, organizationId, projectId, "read record");
        recordQuery = recordQuery.WithAuthorizedLabels(userAuthorizedLabels);
    }

    // Get total count before pagination
    var totalCount = await recordQuery.CountAsync();

    // Get pagination values
    var pageNumber = queryDto?.PageNumber ?? 1;
    var pageSize = queryDto?.GetValidatedPageSize() ?? 25;

    // Apply pagination and execute query
    var items = await recordQuery
        .Include(r => r.Tags)
        .Include(r => r.Labels)
        .OrderByDescending(r => r.LastUpdatedAt)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
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
            FileSize = r.FileSize,
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
        })
        .ToListAsync();

    return new PaginatedResponse<RecordResponseDto>
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
    public async Task<List<RecordResponseDto>> GetRecordsByTags(
        long currentUserId, long organizationId, long projectId, long[] tagIds, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var recordQuery = _context.Records
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        // Only return records that contain ALL given IDs
        recordQuery = recordQuery.Where(r =>
            tagIds.All(tagId => r.Tags.Any(t => t.Id == tagId)));
        
        // if user is not admin, filter out unauthorized labels
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");
            recordQuery = recordQuery.WithAuthorizedLabels(userAuthorizedLabels);
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
                FileSize = r.FileSize,
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
    ///     Retrieves a specific record by its ID
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project of the record to retrieve</param>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record in question</returns>
    /// <exception cref="KeyNotFoundException">Returned if record not found</exception>
    public async Task<RecordResponseDto> GetRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived)
    {
        var record = await _context.Records
            .Where(r => r.ProjectId == projectId
                        && r.Id == recordId
                        && r.OrganizationId == organizationId
                        && (!hideArchived || !r.IsArchived))
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync();

        if (record == null)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

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
            FileSize = record.FileSize,
            Tags = record.Tags.Select(t => new RecordTagDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList(),
            Labels = record.Labels.Select(t => new RecordLabelDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList()
        };
    }

    /// <summary>
    ///     Attaches a tag to a record
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or tag are not found</exception>
    /// <exception cref="Exception">Thrown if the tag is already attached to the record</exception>
    public async Task<bool> AttachTag(long currentUserId, long organizationId, long projectId, long recordId,
        long tagId)
    {
        var record = await _context.Records
            .Where(r => r.ProjectId == projectId
                        && r.Id == recordId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived)
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync();

        if (record == null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        // Check if already attached
        var alreadyAttached = record.Tags.Any(t => t.Id == tagId);
        if (alreadyAttached)
            throw new InvalidOperationException($"Tag with id {tagId} is already attached to record {recordId}");

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

        record.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Attaches a sensitivity label to a record
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and label</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="labelId">The ID of the label</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or label are not found</exception>
    /// <exception cref="Exception">Thrown if the label is already attached to the record</exception>
    public async Task<bool> AttachLabel(long currentUserId, long organizationId, long projectId, long recordId,
    long labelId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var record = await _context.Records
                .Where(r => r.ProjectId == projectId
                            && r.Id == recordId
                            && r.OrganizationId == organizationId
                            && !r.IsArchived)
                .Include(r => r.Labels)
                .FirstOrDefaultAsync();

            if (record == null)
                throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

            var alreadyAttached = record.Labels.Any(t => t.Id == labelId);
            if (alreadyAttached)
                throw new InvalidOperationException($"Label with id {labelId} is already attached to record {recordId}");

            var label = await _context.SensitivityLabels
                .Where(t => t.Id == labelId
                            && t.OrganizationId == organizationId
                            && (t.ProjectId == projectId || t.ProjectId == null)
                            && !t.IsArchived)
                .FirstOrDefaultAsync();

            if (label == null)
                throw new KeyNotFoundException(
                    $"Label with id {labelId} not found, is archived, or does not belong to this organization/project.");

            var collectionsWithRecord = await _context.RecordCollections
                .Where(c => c.ProjectId == projectId
                            && c.OrganizationId == organizationId
                            && !c.IsArchived
                            && c.Records.Any(r => r.Id == recordId))
                .Include(c => c.Labels)
                .ToListAsync();

            record.Labels.Add(label);

            foreach (var collection in collectionsWithRecord)
            {
                if (collection.Labels.All(l => l.Id != labelId))
                    collection.Labels.Add(label);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Unattach a tag from a record
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and tag</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or tag are not found</exception>
    public async Task<bool> UnattachTag(long currentUserId, long organizationId, long projectId, long recordId,
        long tagId)
    {
        var record = await _context.Records
            .Where(r => r.ProjectId == projectId
                        && r.Id == recordId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived)
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync();

        if (record == null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        // Find the tag
        var tag = record.Tags.FirstOrDefault(t => t.Id == tagId);

        if (tag == null)
            throw new KeyNotFoundException($"Tag with id {tagId} is not attached to record {recordId}");

        if (tag.IsArchived ||
            tag.OrganizationId != organizationId ||
            (tag.ProjectId.HasValue && tag.ProjectId != projectId))
            throw new InvalidOperationException(
                $"Tag with id {tagId} is archived or does not belong to this organization/project.");

        record.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Unattach a sensitivity label from a record
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">Project ID for the record and sensitivity label</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="labelId">The ID of the label</param>
    /// <returns>True if successful</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the record or sensitivity label are not found</exception>
    public async Task<bool> UnattachLabel(long currentUserId, long organizationId, long projectId, long recordId,
        long labelId)
    {
        var sensitivityLabelRequired =
            await _sensitivityLabelService.IsSensitivityLabelRequired(organizationId, projectId);

        var record = await _context.Records
            .Where(r => r.ProjectId == projectId
                        && r.Id == recordId
                        && r.OrganizationId == organizationId
                        && !r.IsArchived)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync();

        if (record == null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        var label = record.Labels.FirstOrDefault(t => t.Id == labelId);

        if (label == null)
            throw new KeyNotFoundException($"Label with id {labelId} is not attached to record {recordId}");

        if (label.IsArchived ||
            label.OrganizationId != organizationId ||
            (label.ProjectId.HasValue && label.ProjectId != projectId))
            throw new InvalidOperationException(
                $"Label with id {labelId} is archived or does not belong to this organization/project.");

        if (sensitivityLabelRequired && record.Labels.Count == 1)
            throw new InvalidOperationException(
                "Sensitivity labels are required on all records. Add a new label first to remove this one");

        record.Labels.Remove(label);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Bulk attach tags and records
    /// </summary>
    /// <param name="dtos">A list of record_id/tag_id pairs to be inserted</param>
    /// <returns>True if successful</returns>
    /// <exception cref="Exception">Thrown if tags unable to be attached</exception>
    public async Task<bool> BulkInsertRecordTagLinks(List<RecordTagLinkDto> dtos)
    {
        if (!dtos.Any())
            return true;

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
        var valueTuples = string.Join(", ", dtos.Select((_, i) => $"(@record{i}_id, @tag{i}_id)"));

        // put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        return true;
    }

    /// <summary>
    ///     Bulk unattach tags and records
    /// </summary>
    /// <param name="dtos">A list of record_id/tag_id pairs to be inserted</param>
    /// <returns>True if successful</returns>
    /// <exception cref="Exception">Thrown if tags unable to be unattached</exception>
    public async Task<bool> BulkDeleteRecordTagLinks(List<RecordTagLinkDto> dtos)
    {
        if (!dtos.Any())
            return true;
        
        // Bulk delete from record_tags
        var sql = @"DELETE FROM deeplynx.record_tags WHERE (record_id, tag_id) IN ({0});";
        
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
    ///     Bulk attach sensitivity labels and records
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="recordIds">The IDs of the records to which the labels will be attached</param>
    /// <param name="sensitivityLabelIds">The IDs of the labels to attach</param>
    /// <returns>True if successful</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if user doesn't have access to the labels</exception>
    /// <exception cref="ArgumentException">Thrown if recordIds or sensitivityLabelIds are empty</exception>
    public async Task<bool> BulkAttachLabels(
    long currentUserId, long organizationId, long projectId,
    List<long> recordIds, List<long> sensitivityLabelIds)
    {
        if (recordIds == null || !recordIds.Any())
            throw new ArgumentException("Record IDs list cannot be null or empty", nameof(recordIds));

        if (sensitivityLabelIds == null || !sensitivityLabelIds.Any())
            throw new ArgumentException("Sensitivity label IDs list cannot be null or empty",
                nameof(sensitivityLabelIds));

        var distinctRecordIds = recordIds.Distinct().ToList();
        var distinctLabelIds = sensitivityLabelIds.Distinct().ToList();

        // Create list of record and label ID pairs
        var recordLabelPairs = distinctRecordIds
            .SelectMany(recordId => distinctLabelIds.Select(labelId => (recordId, labelId)))
            .ToList();

        // Bulk insert into record_labels using raw SQL
        var sql = @"INSERT INTO deeplynx.record_labels (record_id, label_id) 
                    VALUES {0} ON CONFLICT (record_id, label_id) DO NOTHING;";

        var parameters = new List<NpgsqlParameter>();
        parameters.AddRange(recordLabelPairs.SelectMany((pair, i) => new[]
        {
            new NpgsqlParameter($"@record{i}_id", pair.recordId),
            new NpgsqlParameter($"@label{i}_id", pair.labelId)
        }));

        var valueTuples = string.Join(", ", recordLabelPairs.Select((_, i) => $"(@record{i}_id, @label{i}_id)"));
        sql = string.Format(sql, valueTuples);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
            
            // Clear tracker so EF fetches fresh state after the raw SQL
            _context.ChangeTracker.Clear();
            
            // Fetch labels to attach to collections
            var labels = await _context.SensitivityLabels
                .Where(l => distinctLabelIds.Contains(l.Id))
                .ToListAsync();

            // Find all collections containing any of the records
            var collectionsWithRecords = await _context.RecordCollections
                .Where(c => c.ProjectId == projectId
                            && c.OrganizationId == organizationId
                            && !c.IsArchived
                            && c.Records.Any(r => distinctRecordIds.Contains(r.Id)))
                .Include(c => c.Labels)
                .ToListAsync();

            foreach (var collection in collectionsWithRecords)
            {
                foreach (var label in labels)
                {
                    if (collection.Labels.All(l => l.Id != label.Id))
                    {
                        collection.Labels.Add(label);
                    }
                }

                collection.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                collection.LastUpdatedBy = currentUserId;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return true;
    }

    /// <summary>
    ///     Create a new record
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project under which to create the record</param>
    /// <param name="dataSourceId">The ID of the data source under which to create the record</param>
    /// <param name="dto">The data transfer object containing details on the record to be created</param>
    /// <param name="sensitivityLabelIds">The IDs of the labels to attach</param>
    /// <param name="embedded">Boolean value that determines if the file will be embedded by Insight</param>
    /// <returns>The newly created metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project or datasource are not found</exception>
    /// <exception cref="Exception">Returned if the metadata is too deeply nested</exception>
    public async Task<RecordResponseDto> CreateRecord(long currentUserId, long organizationId, long projectId,
        long dataSourceId, CreateRecordRequestDto dto, List<long>? sensitivityLabelIds = null, bool embedded = false)
    {
        ValidationHelper.ValidateModel(dto);
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        if (dto.Properties == null)
            throw new ArgumentNullException(nameof(dto.Properties), "Properties cannot be null");

        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        if (dto.ObjectStorageId != null)
            await CheckObjectStorageExists(organizationId, projectId, dto.ObjectStorageId.Value);

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
                FileSize = dto.FileSize,
                OrganizationId = organizationId,
                Embedded = embedded
            };

            _context.Records.Add(record);
            await _context.SaveChangesAsync();

            // Process tags (can be created on-the-fly)
            var tags = await ProcessTags(
                currentUserId, organizationId, projectId, record.Id, dto.Tags);

            if (sensitivityLabelIds?.Count > 0)
            {
                var labels = await _context.SensitivityLabels
                    .Where(l => sensitivityLabelIds.Contains(l.Id))
                    .ToListAsync();

                foreach (var label in labels) record.Labels.Add(label);

                await _context.SaveChangesAsync();
            }

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
                FileSize = record.FileSize,
                Tags = tags,
                Labels = record.Labels.Select(l => new RecordLabelDto
                {
                    Id = l.Id,
                    Name = l.Name
                }).ToList(),
                Embedded = embedded
            };
        }
        catch (Exception exc)
        {
            await transaction.RollbackAsync();
            throw new DependencyDeletionException(
                $"unable to create record or its downstream dependents: {exc}");
        }
    }

    /// <summary>
    ///     Create new records
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project under which to create the record</param>
    /// <param name="dataSourceId">The ID of the data source under which to create the record</param>
    /// <param name="records">Enumerable list for of record transfer objects containing details on the records to be created</param>
    /// <param name="sensitivityLabelIds">The IDs of the labels to attach</param>
    /// <returns>The newly created metadata record</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project or datasource are not found</exception>
    /// <exception cref="Exception">Returned on other general errors</exception>
    public async Task<List<RecordResponseDto>> BulkCreateRecords(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        List<CreateRecordRequestDto> records,
        List<long>? sensitivityLabelIds = null)
    {
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        if (records.Count == 0) throw new Exception("Unable to bulk create records: no records selected for creation");

        await EnsureMultipleObjectStoragesExistOnce(organizationId, projectId, records);

        var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // 1. Extract all unique tag names from request DTOs
        var allTagNames = records
            .Where(r => r.Tags != null && r.Tags.Count > 0)
            .SelectMany(r => r.Tags)
            .Distinct()
            .ToList();

        Dictionary<string, long> tagNameToIdMap = new();

        // 2. Bulk upsert tags (before record insertion)
        if (allTagNames.Count > 0)
        {
            var tagDtos = allTagNames.Select(name => new CreateTagRequestDto { Name = name }).ToList();
            var createdTags = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, projectId, tagDtos);
            tagNameToIdMap = createdTags.ToDictionary(t => t.Name, t => t.Id);
        }

        // 3. Bulk insert records
        const string createTempSql = @"
        CREATE TEMP TABLE tmp_records
        (
            organization_id     BIGINT NOT NULL,
            project_id          BIGINT NOT NULL,
            data_source_id      BIGINT NOT NULL,
            name                TEXT NULL,
            description         TEXT NULL,
            uri                 TEXT NULL,
            original_id         TEXT NOT NULL,
            properties          JSONB NULL,
            class_id            BIGINT NULL,
            object_storage_id   BIGINT NULL,
            file_type           TEXT NULL,
            file_size           BIGINT NULL,
            last_updated_at     TIMESTAMP WITHOUT TIME ZONE NOT NULL,
            is_archived         BOOLEAN NOT NULL,
            last_updated_by     BIGINT NULL
        ) ON COMMIT DROP;";

        const string copyCmd = @"
        COPY tmp_records
        (organization_id, project_id, data_source_id, name, description, uri,
         original_id, properties, class_id, object_storage_id, file_type, file_size,
         last_updated_at, is_archived, last_updated_by)
        FROM STDIN (FORMAT BINARY)";

        const string upsertSql = @"
        INSERT INTO deeplynx.records
        (organization_id, project_id, data_source_id, name, description, uri,
         original_id, properties, class_id, object_storage_id, file_type, file_size,
         last_updated_at, is_archived, last_updated_by)
        SELECT organization_id, project_id, data_source_id, name, description, uri,
               original_id, properties, class_id, object_storage_id, file_type, file_size,
               last_updated_at, is_archived, last_updated_by
        FROM tmp_records
        ON CONFLICT (project_id, data_source_id, original_id) DO UPDATE
          SET name              = COALESCE(EXCLUDED.name, records.name),
              description       = COALESCE(EXCLUDED.description, records.description),
              uri               = COALESCE(EXCLUDED.uri, records.uri),
              properties        = COALESCE(EXCLUDED.properties, records.properties),
              class_id          = COALESCE(EXCLUDED.class_id, records.class_id),
              object_storage_id = COALESCE(EXCLUDED.object_storage_id, records.object_storage_id),
              last_updated_at   = EXCLUDED.last_updated_at,
              file_type         = COALESCE(EXCLUDED.file_type, records.file_type),
              file_size         = COALESCE(EXCLUDED.file_size, records.file_size),
              last_updated_by   = EXCLUDED.last_updated_by
        RETURNING id, organization_id, project_id, data_source_id, original_id, name, class_id, 
            object_storage_id, file_type, file_size, last_updated_by, description, properties;";

        var inserted = await _bulkCopyUpsertExecutor.CopyUpsertAsync(
            conn, tx,
            createTempSql,
            copyCmd,
            records,
            (w, dto) =>
            {
                w.Write(organizationId, NpgsqlDbType.Bigint);
                w.Write(projectId, NpgsqlDbType.Bigint);
                w.Write(dataSourceId, NpgsqlDbType.Bigint);
                if (dto.Name is null) w.WriteNull();
                else w.Write(dto.Name, NpgsqlDbType.Text);
                if (dto.Description is null) w.WriteNull();
                else w.Write(dto.Description, NpgsqlDbType.Text);
                if (dto.Uri is null) w.WriteNull();
                else w.Write(dto.Uri, NpgsqlDbType.Text);
                w.Write(dto.OriginalId, NpgsqlDbType.Text);
                if (dto.Properties is null) w.WriteNull();
                else w.Write(JsonSerializer.Serialize(dto.Properties), NpgsqlDbType.Jsonb);

                if (dto.ClassId.HasValue) w.Write(dto.ClassId.Value, NpgsqlDbType.Bigint);
                else w.WriteNull();
                if (dto.ObjectStorageId.HasValue) w.Write(dto.ObjectStorageId.Value, NpgsqlDbType.Bigint);
                else w.WriteNull();
                if (dto.FileType is null) w.WriteNull();
                else w.Write(dto.FileType, NpgsqlDbType.Text);
                if (dto.FileSize is null) w.WriteNull();
                else w.Write(dto.FileSize, NpgsqlDbType.Bigint);

                w.Write(now, NpgsqlDbType.Timestamp);
                w.Write(false, NpgsqlDbType.Boolean);
                w.Write(currentUserId, NpgsqlDbType.Bigint);
            },
            upsertSql,
            MapRecord
        );

        // if sensitivityLabelIds are provided, Bulk insert record-label relationships
        if (sensitivityLabelIds != null && sensitivityLabelIds.Count > 0)
        {
            const string createTempLabelsSql = @"
                CREATE TEMP TABLE tmp_record_labels
                (
                    record_id BIGINT NOT NULL,
                    label_id BIGINT NOT NULL
                ) ON COMMIT DROP;";

            await using var createTempLabelsCmd = new NpgsqlCommand(createTempLabelsSql, conn, tx);
            await createTempLabelsCmd.ExecuteNonQueryAsync();

            const string copyLabelsCmd = @"
                COPY tmp_record_labels (record_id, label_id)
                FROM STDIN (FORMAT BINARY)";

            // Wrap the writer in its own scope
            {
                await using var writer = await conn.BeginBinaryImportAsync(copyLabelsCmd);

                // Apply the same label(s) to all inserted records
                foreach (var record in inserted)
                foreach (var labelId in sensitivityLabelIds)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(record.Id, NpgsqlDbType.Bigint);
                    await writer.WriteAsync(labelId, NpgsqlDbType.Bigint);
                }

                await writer.CompleteAsync();
            } // Writer is disposed here

            const string insertLabelsSql = @"
                INSERT INTO deeplynx.record_labels (record_id, label_id)
                SELECT record_id, label_id FROM tmp_record_labels
                ON CONFLICT (record_id, label_id) DO NOTHING;";

            await using var insertLabelsCmd = new NpgsqlCommand(insertLabelsSql, conn, tx);
            await insertLabelsCmd.ExecuteNonQueryAsync();
        }

        // Bulk insert record-tag relationships
        if (tagNameToIdMap.Count > 0)
        {
            const string createTempTagsSql = @"
                CREATE TEMP TABLE tmp_record_tags
                (
                    record_id BIGINT NOT NULL,
                    tag_id BIGINT NOT NULL
                ) ON COMMIT DROP;";

            await using var createTempTagsCmd = new NpgsqlCommand(createTempTagsSql, conn, tx);
            await createTempTagsCmd.ExecuteNonQueryAsync();

            const string copyTagsCmd = @"
                COPY tmp_record_tags (record_id, tag_id)
                FROM STDIN (FORMAT BINARY)";

            // Wrap the writer in its own scope
            {
                await using var writer = await conn.BeginBinaryImportAsync(copyTagsCmd);

                for (var i = 0; i < records.Count; i++)
                {
                    var dto = records[i];
                    var record = inserted[i];

                    if (dto.Tags != null && dto.Tags.Count > 0)
                        foreach (var tagName in dto.Tags)
                            if (tagNameToIdMap.TryGetValue(tagName, out var tagId))
                            {
                                await writer.StartRowAsync();
                                await writer.WriteAsync(record.Id, NpgsqlDbType.Bigint);
                                await writer.WriteAsync(tagId, NpgsqlDbType.Bigint);
                            }
                }

                await writer.CompleteAsync();
            } // Writer is disposed here

            const string insertTagsSql = @"
                INSERT INTO deeplynx.record_tags (record_id, tag_id)
                SELECT record_id, tag_id FROM tmp_record_tags
                ON CONFLICT (record_id, tag_id) DO NOTHING;";

            await using var insertTagsCmd = new NpgsqlCommand(insertTagsSql, conn, tx);
            await insertTagsCmd.ExecuteNonQueryAsync();
        }

        // Add the tags and labels to the Inserted records for the response 
        // Fetch label names
        Dictionary<long, string> labelNameMap = new();
        if (sensitivityLabelIds != null && sensitivityLabelIds.Count > 0)
        {
            const string fetchLabelNamesSql = @"
        SELECT id, name 
        FROM deeplynx.sensitivity_labels 
        WHERE id = ANY(@labelIds)";

            await using var cmd = new NpgsqlCommand(fetchLabelNamesSql, conn, tx);
            cmd.Parameters.AddWithValue("labelIds", sensitivityLabelIds);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) labelNameMap[reader.GetInt64(0)] = reader.GetString(1);
        }

        // Map tags and labels to inserted records
        for (var i = 0; i < records.Count; i++)
        {
            var dto = records[i];
            var record = inserted[i];

            // Map tags (we already have ID and name)
            if (dto.Tags != null && dto.Tags.Count > 0)
                record.Tags = dto.Tags
                    .Where(tagName => tagNameToIdMap.TryGetValue(tagName, out _))
                    .Select(tagName => new RecordTagDto
                    {
                        Id = tagNameToIdMap[tagName],
                        Name = tagName
                    })
                    .ToList();
            else
                record.Tags = new List<RecordTagDto>();

            // Map labels (same labels applied to all records)
            if (labelNameMap.Count > 0)
                record.Labels = sensitivityLabelIds!
                    .Where(id => labelNameMap.ContainsKey(id))
                    .Select(id => new RecordLabelDto
                    {
                        Id = id,
                        Name = labelNameMap[id]
                    })
                    .ToList();
            else
                record.Labels = new List<RecordLabelDto>();
        }

        // events logging
        var events = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "record",
            DataSourceId = dataSourceId
        };
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, events, records.Count);

        await tx.CommitAsync();
        return inserted;
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
            .Include(r => r.Labels)
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        !r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found or is archived.");

        var lastUpdatedAt = DateTime.UtcNow;

        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.archive_record({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE, {2}::INTEGER)",
                    recordId, lastUpdatedAt, currentUserId
                );

                if (archived == 0)
                    throw new DependencyDeletionException(
                        $"unable to archive record {recordId} or its downstream dependents.");
                
                // Clear the change tracker so EF fetches fresh state after the procedure
                _context.ChangeTracker.Clear();

                // Remove record from all collections it belongs to
                var collectionsWithRecord = await _context.RecordCollections
                    .Where(c => c.ProjectId == projectId
                                && c.OrganizationId == organizationId
                                && !c.IsArchived
                                && c.Records.Any(r => r.Id == recordId))
                    .Include(c => c.Records)
                    .ToListAsync();

                foreach (var collection in collectionsWithRecord)
                {
                    var recordToRemove = collection.Records.FirstOrDefault(r => r.Id == recordId);
                    if (recordToRemove != null)
                    {
                        collection.Records.Remove(recordToRemove);
                        collection.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                        collection.LastUpdatedBy = currentUserId;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to archive record {recordId} or its downstream dependents: {exc}");
            }
        }

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
            .Include(r => r.Labels)
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
            .Include(r => r.Labels)
            .Where(r => r.Id == recordId
                        && r.OrganizationId == organizationId
                        && r.ProjectId == projectId
                        && !r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} is archived or not found");

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
            .Include(r => r.Labels)
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        !r.IsArchived);

        var returnedRecord = await query.FirstOrDefaultAsync();

        if (returnedRecord is null)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var maxDepth = CalculateJsonMaxDepth(dto.Properties);
        if (maxDepth > 3)
            throw new Exception(
                $"The depth of the JSON structure exceeds the maximum allowed depth of 3. Current depth of properties is {maxDepth}.");

        if (dto.ObjectStorageId != null)
            await CheckObjectStorageExists(organizationId, projectId, dto.ObjectStorageId.Value);

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
        returnedRecord.FileSize = dto.FileSize ?? returnedRecord.FileSize;

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
            FileType = returnedRecord.FileType,
            FileSize = returnedRecord.FileSize
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
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId,
            hideArchived);
        var recordQuery = _context.Records
            .Where(r => r.OrganizationId == organizationId && r.ProjectId == projectId &&
                        r.DataSourceId == dataSourceId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        return await recordQuery.CountAsync();
    }

    /// <summary>
    ///     Returns a list of textual descriptors for the Records table to be used by Lattice.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the records belong</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <returns>List of record textual descriptor columns, including class names</returns>
    public async Task<List<LatticeRecordDto>> GetLatticeRecords(long organizationId, long projectId)
    {
        var classes = await _context.Database
            .SqlQuery<LatticeRecordDto>(
                $"SELECT * FROM deeplynx.get_lattice_records({organizationId}, {projectId})"
            ).ToListAsync();

        return classes;
    }

    /// <summary>
    ///     Get records by their original ID
    /// </summary>
    /// <param name="currentUserId">The ID current user making the request</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The project ID to search within</param>
    /// <param name="dataSourceId">The data source ID to search within</param>
    /// <param name="originalIds">List of original IDs to validate</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>List of records that were found</returns>
    /// <exception cref="KeyNotFoundException">Thrown if one or more original IDs not found</exception>
    /// <exception cref="ArgumentException">Thrown if originalIds list is null or empty</exception>
    public async Task<List<RecordResponseDto>> GetRecordsByOriginalId(
        long currentUserId, long organizationId, long projectId, long dataSourceId,
        List<string> originalIds, bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
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

        var dataSourceExists = await _context.DataSources.AnyAsync(d =>
            d.OrganizationId == organizationId &&
            (d.ProjectId == null || d.ProjectId == projectId) &&
            d.Id == dataSourceId);

        if (!dataSourceExists) throw new KeyNotFoundException($"No data source with Id {dataSourceId} in org {organizationId} or project {projectId}");

        // Query for existing records (excluding archived)
        var recordQuery = _context.Records
            .Include(r => r.Labels)
            .Include(r => r.Tags)
            .Where(r => r.ProjectId == projectId
                        && r.DataSourceId == dataSourceId
                        && r.OrganizationId == organizationId
                        && (!hideArchived || !r.IsArchived)
                        && cleanOriginalIds.Contains(r.OriginalId));
        
        // if user is not admin, filter out unauthorized labels
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");
            recordQuery = recordQuery.WithAuthorizedLabels(userAuthorizedLabels);
        }
        
        var existingRecords = await recordQuery.ToListAsync();

        // Check for missing records
        var foundOriginalIds = existingRecords.Select(r => r.OriginalId).ToHashSet();
        var missingOriginalIds = cleanOriginalIds.Where(id => !foundOriginalIds.Contains(id)).ToList();

        if (missingOriginalIds.Any())
            throw new KeyNotFoundException(
                $"Records not found or access is unauthorized with original IDs: {string.Join(", ", missingOriginalIds)}");

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
            FileSize = r.FileSize,
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

    /// <summary>
    ///     Make sure every object storage ID exists, filtering in memory with one DB trip
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId"> Shared project ID of the object storages </param>
    /// <param name="records"> Records with object storages to check</param>
    /// <exception cref="KeyNotFoundException">If an object storage ID is not found</exception>
    /// <returns>Exception if obj storage ID not exist</returns>
    private async Task EnsureMultipleObjectStoragesExistOnce(long organizationId, long projectId,
        List<CreateRecordRequestDto> records)
    {
        var ids = records.Where(r => r.ObjectStorageId.HasValue)
            .Select(r => r.ObjectStorageId!.Value)
            .Distinct()
            .ToArray();
        if (ids.Length == 0) return;

        // One database round trip                                                                                                            
        var ok = await _context.ObjectStorages
            .Where(os =>
                os.OrganizationId == organizationId && ids.Contains(os.Id) &&
                (os.ProjectId == projectId || os.ProjectId == null))
            .Select(os => os.Id)
            .ToListAsync();

        if (ok.Count != ids.Length)
        {
            var missing = ids.Except(ok).Take(5);
            throw new KeyNotFoundException(
                $"One or more object storage IDs do not exist in organization {organizationId} (e.g., {string.Join(",", missing)}).");
        }
    }

    private async Task CheckObjectStorageExists(long organizationId, long projectId, long objectStorageId)
    {
        var referencedObjectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(o =>
            o.OrganizationId == organizationId && o.Id == objectStorageId &&
            (o.ProjectId == projectId || o.ProjectId == null));

        if (referencedObjectStorage == null)
            throw new KeyNotFoundException($"Object storage with ID {objectStorageId} does not exist");
    }

    private async Task<ICollection<RecordTagDto>> ProcessTags(long currentUserId, long organizationId,
        long projectId,
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

        if (recordTags.Any()) await BulkInsertRecordTagLinks(recordTags);

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

    /// <summary>
    ///     Validate and bulk attach tags to records for public API use.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId"> The ID of the project to which the records belong</param>
    /// <param name="dtos">A list of record_id/tag_id pairs to be inserted</param>
    /// <exception cref="ArgumentException"> Thrown if no record/tag pairs are provided or if no authorized record/tag pairs remain after filtering</exception>
    /// <exception cref="KeyNotFoundException">Returned if one or more records or tags are not found or archived</exception>
    /// <returns>True if successful</returns>
    public async Task<bool> BulkAttachTags(long currentUserId, long organizationId, 
        long projectId, List<RecordTagLinkDto> dtos)
    {
        if (dtos.Count == 0)
            throw new ArgumentException("Record,tag pairs cannot be null or empty", nameof(dtos));
        
        var recordIds = dtos
            .Select(r => r.RecordId)
            .Distinct()
            .ToList();
        
        var tagIds = dtos
            .Select(r => r.TagId)
            .Distinct()
            .ToList();
        
        // Validate records belong to this organization/project and are not archived 
        var records = await _context.Records
            .Where(r => recordIds.Contains(r.Id) && r.OrganizationId == organizationId && r.ProjectId == projectId && !r.IsArchived)
            .Select(r => r.Id)
            .ToListAsync();

        if (records.Count != recordIds.Count)
            throw new KeyNotFoundException("One or more records were not found or archived.");

        // Validate tags belong to this organization/project and are not archived 
        var tags = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.OrganizationId == organizationId && (t.ProjectId == projectId || t.ProjectId == null) && !t.IsArchived)
            .Select(t => t.Id)
            .ToListAsync();
        
        if (tags.Count != tagIds.Count)
            throw new KeyNotFoundException("One or more tags were not found or archived.");
        
        var authorizedRecordIds = await _sensitivityLabelService
            .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, recordIds, _context);
        
        dtos = dtos
            .Where(dto => authorizedRecordIds.Contains(dto.RecordId))
            .ToList();
        
        if (dtos.Count == 0)
            throw new ArgumentException("User does not have access to any provided records", nameof(dtos));
        
        await BulkInsertRecordTagLinks(dtos);

        return true;
    }

    /// <summary>
    ///     Validate and bulk unattach tags from records for public API use.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId"> The ID of the project to which the records belong</param>
    /// <param name="dtos">A list of record_id/tag_id pairs to be deleted</param>
    /// <exception cref="ArgumentException"> Thrown if no record/tag pairs are provided or if no authorized record/tag pairs remain after filtering</exception>
    /// <exception cref="KeyNotFoundException">Returned if one or more records or tags are not found or archived</exception>
    /// <returns>True if successful</returns>
    public async Task<bool> BulkUnattachTags(long currentUserId, long organizationId,
        long projectId, List<RecordTagLinkDto> dtos)
    {
        if (dtos.Count == 0)
            throw new ArgumentException("Record,tag pairs cannot be null or empty", nameof(dtos));
        
        var recordIds = dtos
            .Select(r => r.RecordId)
            .Distinct()
            .ToList();

        var tagIds = dtos
            .Select(r => r.TagId)
            .Distinct()
            .ToList();
        
        // Validate records belong to this organization/project and are not archived 
        var records = await _context.Records
            .Where(r => recordIds.Contains(r.Id) && r.OrganizationId == organizationId && r.ProjectId == projectId && !r.IsArchived)
            .Select(r => r.Id)
            .ToListAsync();
        
        if (records.Count != recordIds.Count)
            throw new KeyNotFoundException("One or more records were not found or archived.");
        
        // Validate tags belong to this organization/project and are not archived 
        var tags = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.OrganizationId == organizationId && (t.ProjectId == projectId || t.ProjectId == null) && !t.IsArchived)
            .Select(t => t.Id)
            .ToListAsync();
        
        if (tags.Count != tagIds.Count)
            throw new KeyNotFoundException("One or more tags were not found or archived.");

        var authorizedRecordIds = await _sensitivityLabelService
            .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, recordIds, _context);
        
        dtos = dtos
            .Where(dto => authorizedRecordIds.Contains(dto.RecordId))
            .ToList();
        
        if (dtos.Count == 0)
            throw new ArgumentException("User does not have access to any provided records", nameof(dtos));
        
        await BulkDeleteRecordTagLinks(dtos);
        
        return true;
    }
    
    /// <summary>
    ///     Map an NPGSQL data reader to a return DTO usually during high scale read operations
    /// </summary>
    /// <param name="r">NPGSQL reader object containing DTO params</param>
    /// <returns>A response data transfer object with fields mapped from the pg reader</returns>
    private static RecordResponseDto MapRecord(NpgsqlDataReader r)
    {
        var iId = r.GetOrdinal("id");
        var iProj = r.GetOrdinal("project_id");
        var iDs = r.GetOrdinal("data_source_id");
        var iOrig = r.GetOrdinal("original_id");
        var iName = r.GetOrdinal("name");
        var iCls = r.GetOrdinal("class_id");
        var iObj = r.GetOrdinal("object_storage_id");
        var iType = r.GetOrdinal("file_type");
        var iSize = r.GetOrdinal("file_size");
        var iUser = r.GetOrdinal("last_updated_by");
        var iDesc = r.GetOrdinal("description");
        var iProp = r.GetOrdinal("properties");

        return new RecordResponseDto
        {
            Id = r.GetInt64(iId),
            ProjectId = r.GetInt64(iProj),
            DataSourceId = r.GetInt64(iDs),
            OriginalId = r.GetString(iOrig),
            Name = r.IsDBNull(iName) ? null : r.GetString(iName),
            ClassId = r.IsDBNull(iCls) ? null : r.GetInt64(iCls),
            ObjectStorageId = r.IsDBNull(iObj) ? null : r.GetInt64(iObj),
            FileType = r.IsDBNull(iType) ? null : r.GetString(iType),
            FileSize = r.IsDBNull(iSize) ? null : r.GetInt64(iSize),
            LastUpdatedBy = r.IsDBNull(iUser) ? null : r.GetInt64(iUser),
            Description = r.IsDBNull(iDesc) ? null : r.GetString(iDesc),
            Properties = r.IsDBNull(iProp) ? null : r.GetString(iProp)
        };
    }
}