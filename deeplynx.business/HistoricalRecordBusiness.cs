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
    private readonly ISensitivityLabelService _sensitivityLabelService;
    private readonly IProjectRolePermissionService _projectRolePermissionService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoricalRecordBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the record operations.</param>
    /// <param name="sensitivityLabelService">The sensitivity Label Service. </param>
    /// <param name="projectRolePermissionService">Used to check class/tag read permissions in a project, ahead of querying.</param>
    public HistoricalRecordBusiness(
        DeeplynxContext context,
        ISensitivityLabelService sensitivityLabelService,
        IProjectRolePermissionService projectRolePermissionService)
    {
        _context = context;
        _sensitivityLabelService = sensitivityLabelService;
        _projectRolePermissionService = projectRolePermissionService;
    }


    /// <summary>
    ///     Retrieves all Historical Records for a specific project and datasource
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="paginatedRequestDto">(optional) Pagination parameters; if null, all matching records are returned unpaginated</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="pointInTime">(Optional) Find the most current records that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived records from the result.</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A paginated list of historical records, or all records if no pagination is specified</returns>
    public async Task<PaginatedResponse<HistoricalRecordResponseDto>> GetAllHistoricalRecordsPaginated(
        long currentUserId, long projectId, long organizationId, PaginatedRequestDto paginatedRequestDto, long? dataSourceId = null, DateTime? pointInTime = null,
        bool hideArchived = true, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        // Permission check happens first, ahead of any historical-record query.
        var (canReadClass, canReadTags) = await GetHistoricalFieldPermissions(
            currentUserId, projectId, isSysAdmin, isOrgAdmin, isProjectAdmin);

        var recordQuery = _context.HistoricalRecords
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (dataSourceId.HasValue) recordQuery = recordQuery.Where(r => r.DataSourceId == dataSourceId);

        if (pointInTime.HasValue)
        {
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);
            recordQuery = recordQuery.Where(r => r.LastUpdatedAt <= unspecifiedPointInTime);
        }

        var latestPerRecord = await recordQuery
            .GroupBy(e => e.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).First())
            .ToListAsync();

        var records = latestPerRecord
            .Select(r => new
            {
                r.RecordId,
                r.Uri,
                r.Properties,
                r.OriginalId,
                r.Name,
                r.Description,
                ClassId = canReadClass ? r.ClassId : null,
                ClassName = canReadClass ? r.ClassName : null,
                r.DataSourceId,
                r.DataSourceName,
                r.ObjectStorageId,
                r.ObjectStorageName,
                r.ProjectId,
                r.ProjectName,
                Tags = canReadTags ? r.Tags : "[]",
                r.LastUpdatedBy,
                r.FileType,
                r.FileSize,
                r.IsArchived,
                r.LastUpdatedAt
            })
            .ToList();

        if (hideArchived && records.Count > 0)
            records = records.Where(r => !r.IsArchived).ToList();

        var recordIds = records.Select(r => r.RecordId).ToList();

        // if user is not admin, filter out unauthorized labels
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedIds = await _sensitivityLabelService
                .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, recordIds, _context);
            records = records.Where(r => authorizedIds.Contains(r.RecordId)).ToList();
        }

        if (records.Count == 0)
        {
            return new PaginatedResponse<HistoricalRecordResponseDto>
            {
                Items = [],
                PageNumber = paginatedRequestDto.PageNumber,
                PageSize = paginatedRequestDto.PageSize,
                TotalCount = 0
            };
        }

        var authorizedDownloadLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
            currentUserId,
            organizationId,
            projectId,
            "download file");

        var currentRecords = await _context.Records
            .Where(r => recordIds.Contains(r.Id))
            .Include(r => r.Labels)
            .ToDictionaryAsync(r => r.Id);

        var orderedItems = records
            .OrderBy(r => r.RecordId)
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = currentRecords.TryGetValue(r.RecordId, out var currentRecord) &&
                    ExposeUriHelper.CanExposeUri(currentRecord, authorizedDownloadLabels, isSysAdmin, isOrgAdmin, isProjectAdmin)
                    ? r.Uri
                    : null,
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
                Labels = currentRecord?.Labels?.Select(l => l.Name).ToList(),
                LastUpdatedBy = r.LastUpdatedBy,
                FileType = r.FileType,
                FileSize = r.FileSize,
                IsArchived = r.IsArchived,
                LastUpdatedAt = r.LastUpdatedAt
            })
            .ToList();

        var totalCount = orderedItems.Count;

        if (paginatedRequestDto.PageSize == -1)
        {
            return new PaginatedResponse<HistoricalRecordResponseDto>
            {
                Items = orderedItems,
                PageNumber = 1,
                PageSize = orderedItems.Count,
                TotalCount = totalCount
            };
        }

        var pagedItems = orderedItems
            .Skip((paginatedRequestDto.PageNumber - 1) * paginatedRequestDto.PageSize)
            .Take(paginatedRequestDto.PageSize)
            .ToList();

        return new PaginatedResponse<HistoricalRecordResponseDto>
        {
            Items = pagedItems,
            PageNumber = paginatedRequestDto.PageNumber,
            PageSize = paginatedRequestDto.PageSize,
            TotalCount = totalCount
        };
    }

    #region Deprecated

    /// <summary>
    /// [DEPRECATED - V1 ONLY] List all historical records without pagination
    /// Superseded by <see cref="GetAllHistoricalRecordsPaginated"/>. Do not call this from new controller versions;
    /// it exists solely to back the deprecated v1 historical record controllers and should be deleted once
    /// those v1 endpoints are sunset.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="pointInTime">(Optional) Find the most current records that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived records from the result.</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>An array of records</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetAllHistoricalRecords(
        long currentUserId, long projectId, long organizationId, long? dataSourceId = null, DateTime? pointInTime = null,
        bool hideArchived = true, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var (canReadClass, canReadTags) = await GetHistoricalFieldPermissions(
            currentUserId, projectId, isSysAdmin, isOrgAdmin, isProjectAdmin);

        var recordQuery = _context.HistoricalRecords
            .Where(r => r.ProjectId == projectId && r.OrganizationId == organizationId);

        if (dataSourceId.HasValue) recordQuery = recordQuery.Where(r => r.DataSourceId == dataSourceId);

        if (pointInTime.HasValue)
        {
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);
            recordQuery = recordQuery.Where(r => r.LastUpdatedAt <= unspecifiedPointInTime);
        }

        var latestPerRecord = await recordQuery
            .GroupBy(e => e.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).FirstOrDefault())
            .ToListAsync();

        var records = latestPerRecord
            .Select(r => new
            {
                r.RecordId,
                r.Uri,
                r.Properties,
                r.OriginalId,
                r.Name,
                r.Description,
                ClassId = canReadClass ? r.ClassId : null,
                ClassName = canReadClass ? r.ClassName : null,
                r.DataSourceId,
                r.DataSourceName,
                r.ObjectStorageId,
                r.ObjectStorageName,
                r.ProjectId,
                r.ProjectName,
                Tags = canReadTags ? r.Tags : "[]",
                r.LastUpdatedBy,
                r.FileType,
                r.FileSize,
                r.IsArchived,
                r.LastUpdatedAt
            })
            .ToList();

        // need to check for archived after DB retrieval since filtering before querying could
        // result in inaccurate "most recent" results if a record has been archived
        if (hideArchived && records.Count > 0)
            records = records.Where(r => !r.IsArchived).ToList();

        // if user is not admin, filter out unauthorized labels    
        var recordIds = records.Select(r => r.RecordId).ToList();

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedIds = await _sensitivityLabelService
                        .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, recordIds, _context);
            records = records.Where(r => authorizedIds.Contains(r.RecordId)).ToList();
        }

        var authorizedDownloadLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
            currentUserId,
            organizationId,
            projectId,
            "download file");

        var currentRecords = await _context.Records
            .Where(r => recordIds.Contains(r.Id))
            .Include(r => r.Labels)
            .ToDictionaryAsync(r => r.Id);

        return records
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = currentRecords.TryGetValue(r.RecordId, out var currentRecord) &&
                    ExposeUriHelper.CanExposeUri(currentRecord, authorizedDownloadLabels, isSysAdmin, isOrgAdmin, isProjectAdmin)
                    ? r.Uri
                    : null,
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
                Labels = currentRecord?.Labels?.Select(l => l.Name).ToList(),
                LastUpdatedBy = r.LastUpdatedBy,
                FileType = r.FileType,
                FileSize = r.FileSize,
                IsArchived = r.IsArchived,
                LastUpdatedAt = r.LastUpdatedAt
            });
    }

    #endregion

    /// <summary>
    ///     Show the historical updates of a specific record
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="recordId">The ID of the record to list history for</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="isSysAdmin">Whether the current user is a system administrator</param>
    /// <param name="isOrgAdmin">Whether the current user is an organization administrator</param>
    /// <param name="isProjectAdmin">Whether the current user is a project administrator</param>
    /// <returns>An array of record instances for the given record</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetHistoryForRecord(long currentUserId, long recordId,
    long organizationId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        // We need the record's ProjectId to run the permission check, so this one small lookup
        // has to happen before we know canReadClass/canReadTags. Everything after this point —
        // the actual historical-record query — is still shaped by the permission result.
        var record = await _context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.OrganizationId == organizationId);
        if (record == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        var (canReadClass, canReadTags) = await GetHistoricalFieldPermissions(
            currentUserId, record.ProjectId, isSysAdmin, isOrgAdmin, isProjectAdmin);

        var authorizedDownloadLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
            currentUserId,
            organizationId,
            record.ProjectId,
            "download file");

        var canExposeUri = ExposeUriHelper.CanExposeUri(
            record,
            authorizedDownloadLabels,
            isSysAdmin,
            isOrgAdmin,
            isProjectAdmin);

        var historicalRecord = await _context.HistoricalRecords
            .Where(r => r.RecordId == recordId && r.OrganizationId == organizationId)
            .OrderByDescending(r => r.LastUpdatedAt)
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = canExposeUri
                    ? r.Uri
                    : null,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                Description = r.Description,
                ClassId = canReadClass ? r.ClassId : null,
                ClassName = canReadClass ? r.ClassName : null,
                DataSourceId = r.DataSourceId,
                DataSourceName = r.DataSourceName,
                ObjectStorageId = r.ObjectStorageId,
                ObjectStorageName = r.ObjectStorageName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                Tags = canReadTags ? r.Tags : "[]",
                LastUpdatedBy = r.LastUpdatedBy,
                FileType = r.FileType,
                FileSize = r.FileSize,
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
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="pointInTime">(Optional) Find the most current record that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived records from the result.</param>
    /// <param name="isSysAdmin">Whether the current user is a system administrator</param>
    /// <param name="isOrgAdmin">Whether the current user is an organization administrator</param>
    /// <param name="isProjectAdmin">Whether the current user is a project administrator</param>
    /// <returns>A record that matches the applied filters.</returns>
    /// <exception cref="KeyNotFoundException">Returned if record not found</exception>
    public async Task<HistoricalRecordResponseDto> GetHistoricalRecord(
        long currentUserId,
        long recordId,
        long organizationId,
        DateTime? pointInTime,
        bool hideArchived = true,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false)
    {
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

        var currentRecord = await _context.Records
            .Where(r => r.Id == recordId && r.OrganizationId == organizationId)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync();

        if (currentRecord == null)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var (canReadClass, canReadTags) = await GetHistoricalFieldPermissions(
            currentUserId, record.ProjectId, isSysAdmin, isOrgAdmin, isProjectAdmin);

        var authorizedDownloadLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
            currentUserId,
            organizationId,
            record.ProjectId,
            "download file");

        return new HistoricalRecordResponseDto
        {
            Id = record.RecordId,
            Uri = ExposeUriHelper.CanExposeUri(
                currentRecord,
                authorizedDownloadLabels,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin)
                    ? record.Uri
                    : null,
            Properties = record.Properties,
            OriginalId = record.OriginalId,
            Name = record.Name,
            Description = record.Description,
            ClassId = canReadClass ? record.ClassId : null,
            ClassName = canReadClass ? record.ClassName : null,
            DataSourceId = record.DataSourceId,
            DataSourceName = record.DataSourceName,
            RecordId = record.RecordId,
            ObjectStorageId = record.ObjectStorageId,
            ObjectStorageName = record.ObjectStorageName,
            ProjectId = record.ProjectId,
            ProjectName = record.ProjectName,
            Tags = canReadTags ? record.Tags : "[]",
            Labels = currentRecord?.Labels?.Select(l => l.Name).ToList(),
            LastUpdatedBy = record.LastUpdatedBy,
            FileType = record.FileType,
            FileSize = record.FileSize,
            IsArchived = record.IsArchived,
            LastUpdatedAt = record.LastUpdatedAt
        };
    }


    /// <summary>
    ///     Checks, ahead of any historical-record query, whether the caller may see class and tag
    ///     data for the given project. Admins see both; non-admins are checked individually against
    ///     <see cref="IProjectRolePermissionService.PermissionInProject"/>. The result is used to decide
    ///     what the subsequent database query actually selects — unauthorized fields are never
    ///     retrieved, not stripped out afterward.
    /// </summary>
    private async Task<(bool canReadClass, bool canReadTags)> GetHistoricalFieldPermissions(
        long currentUserId,
        long projectId,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin)
    {
        if (isSysAdmin || isOrgAdmin || isProjectAdmin)
            return (true, true);

        var canReadClass = await _projectRolePermissionService.PermissionInProject(
            currentUserId, projectId, "read", "class");
        var canReadTags = await _projectRolePermissionService.PermissionInProject(
            currentUserId, projectId, "read", "tag");

        return (canReadClass, canReadTags);
    }

}