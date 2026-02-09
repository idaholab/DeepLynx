using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class HistoricalRecordBusiness : IHistoricalRecordBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoricalRecordBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the record operations.</param>
    public HistoricalRecordBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Retrieves all Historical Records for a specific project and datasource
    /// </summary>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="pointInTime">(Optional) Find the most current records that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived records from the result.</param>
    /// <returns>An array of records</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetAllHistoricalRecords(
        long currentUserId,
        long projectId,
        long organizationId,
        long? dataSourceId = null,
        DateTime? pointInTime = null,
        bool hideArchived = true)
    {
        var recordQuery = _context.HistoricalRecords
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (dataSourceId.HasValue) recordQuery = recordQuery.Where(r => r.DataSourceId == dataSourceId);

        // specification for "current" should override any supplied pointInTime
        if (pointInTime.HasValue)
        {
            // convert the point in time to timestamp without timezone
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);

            // compare the timestamp to the most recent update
            recordQuery = recordQuery
                .Where(r => r.LastUpdatedAt <= unspecifiedPointInTime)
                .OrderByDescending(r => r.LastUpdatedAt);
        }

        var records = await recordQuery
            .GroupBy(e => e.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).FirstOrDefault())
            .ToListAsync();
        
        // Get user's authorized labels
        var userAuthorizedLabels =
            await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId, "read record");

        // need to check for archived at after DB retrieval since filtering archived results before querying could
        // result in inaccurate "most recent" results if a record has been archived
        if (hideArchived && records.Count > 0) records = records.Where(r => !r.IsArchived).ToList();

        var liveRecords = await _context.Records
            .Include(r => r.Labels)
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId).ToListAsync();
    
        var authorizedHistoricalRecords = records.Where(r => 
        {
            // Find the live record that matches this historical record's originalId
            var liveRecord = liveRecords.FirstOrDefault(lr => lr.Id == r.RecordId);
    
            // If no live record found, exclude this historical record
            if (liveRecord == null) return false;
    
            // If the live record has no labels, include this historical record
            if (liveRecord.Labels == null || !liveRecord.Labels.Any()) return true;
    
            // If the live record has labels, check if ALL labels are in the authorized list
            return liveRecord.Labels.All(label => userAuthorizedLabels.Contains(label.Id));
        }).ToList();

        return authorizedHistoricalRecords
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                Description = r.Description,
                ClassId = r.ClassId,
                ClassName = r.ClassName,
                DataSourceId = r.DataSourceId,
                DataSourceName = r.DataSourceName,
                ObjectStorageId = r.ObjectStorageId,
                ObjectStorageName = r.ObjectStorageName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                Tags = r.Tags,
                Labels = r.Labels,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
                LastUpdatedAt = r.LastUpdatedAt
            });
    }

    /// <summary>
    ///     Show the historical updates of a specific record
    /// </summary>
    /// <param name="recordId">The ID of the record to list history for</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <returns>An array of record instances for the given record</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetHistoryForRecord(long currentUserId, long recordId, long organizationId)
    {
        var record = await _context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.OrganizationId == organizationId);
        if (record == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        if (record.Labels.Count > 0)
        {
            var userAuthorizedLabels = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, record.ProjectId, "read record");
            
            var recordLabelIds = record.Labels.Select(l => l.Id).ToList();
            var hasAllRequiredLabels = recordLabelIds.All(labelId =>
                userAuthorizedLabels.Contains(labelId));

            if (!hasAllRequiredLabels)
            {
                throw new UnauthorizedAccessException(
                    $"You do not have access to all required sensitivity labels for record {recordId}");
            }
        }

        var historicalRecord = await _context.HistoricalRecords
            .Where(r => r.RecordId == recordId && r.OrganizationId == organizationId)
            .OrderByDescending(r => r.LastUpdatedAt)
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                Description = r.Description,
                ClassId = r.ClassId,
                ClassName = r.ClassName,
                DataSourceId = r.DataSourceId,
                DataSourceName = r.DataSourceName,
                ObjectStorageId = r.ObjectStorageId,
                ObjectStorageName = r.ObjectStorageName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                Tags = r.Tags,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
                LastUpdatedAt = r.LastUpdatedAt
            })
            .ToListAsync();
        if (historicalRecord.Count == 0)
            throw new Exception($"Record with id {recordId} exists but history was not found");
        return historicalRecord;
    }

    /// <summary>
    ///     Find a record at a given point in time
    /// </summary>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="pointInTime">(Optional) Find the most current record that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived records from the result.</param>
    /// <returns>A record that matches the applied filters.</returns>
    /// <exception cref="KeyNotFoundException">Returned if record not found</exception>
    public async Task<HistoricalRecordResponseDto> GetHistoricalRecord(
        long currentUserId,
        long recordId,
        long organizationId,
        DateTime? pointInTime,
        bool hideArchived = true)
    {
        var liveRecord = await _context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.OrganizationId == organizationId);
        
        if (liveRecord == null) throw new KeyNotFoundException($"Record with id {recordId} not found");
        
        if (liveRecord.Labels.Count > 0)
        {
            var userAuthorizedLabels = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, liveRecord.ProjectId, "write record");

            var recordLabelIds = liveRecord.Labels.Select(l => l.Id).ToList();
            var hasAllRequiredLabels = recordLabelIds.All(labelId =>
                userAuthorizedLabels.Contains(labelId));

            if (!hasAllRequiredLabels)
            {
                throw new UnauthorizedAccessException(
                    $"You do not have access to all required sensitivity labels for record {recordId}");
            }
        }
        
        var recordQuery = _context.HistoricalRecords
            .Where(r => r.RecordId == recordId && r.OrganizationId == organizationId)
            .OrderByDescending(r => r.LastUpdatedAt);

        if (pointInTime.HasValue)
        {
            // convert the point in time to timestamp without timezone
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);

            // compare the timestamp to the most recent update
            recordQuery = recordQuery
                .Where(r => r.LastUpdatedAt <= unspecifiedPointInTime)
                .OrderByDescending(r => r.LastUpdatedAt);
        }

        var record = await recordQuery
            .FirstOrDefaultAsync();

        if (record == null)
            throw new KeyNotFoundException(
                $"Historical record with id {recordId} not found at point in time {pointInTime}.");

        if (hideArchived && record.IsArchived)
            throw new KeyNotFoundException($"Historical record with id {recordId} not found or is archived.");

        return new HistoricalRecordResponseDto
        {
            Id = record.RecordId,
            Uri = record.Uri,
            Properties = record.Properties,
            OriginalId = record.OriginalId,
            Name = record.Name,
            Description = record.Description,
            ClassId = record.ClassId,
            ClassName = record.ClassName,
            DataSourceId = record.DataSourceId,
            DataSourceName = record.DataSourceName,
            ObjectStorageId = record.ObjectStorageId,
            ObjectStorageName = record.ObjectStorageName,
            ProjectId = record.ProjectId,
            ProjectName = record.ProjectName,
            Tags = record.Tags,
            LastUpdatedBy = record.LastUpdatedBy,
            IsArchived = record.IsArchived,
            LastUpdatedAt = record.LastUpdatedAt
        };
    }
}