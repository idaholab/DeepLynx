using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.MetricsDTOs;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers.Cache;

namespace deeplynx.business;

public class MetricsBusiness : IMetricsBusiness
{
    private readonly DeeplynxContext _context;
    private readonly TimeSpan _storageSizeCacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    public MetricsBusiness(
        DeeplynxContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // Storage Metrics
    // -------------------------------------------------------------------------

    private async Task<long> BuildProjectStorageSizeFromDb(long projectId)
    {
        return await _context.Records
            .Where(r =>
                r.ProjectId == projectId &&
                !r.IsArchived &&
                r.FileSize != null)
            .SumAsync(r => r.FileSize ?? 0);
    }

    private async Task<long> GetProjectStorageSizeBytes(long projectId)
    {
        var cacheKey = CacheKeys.ProjectStorageSize(projectId);

        var cachedSize = await CacheService.Instance.GetAsync<long?>(cacheKey);
        if (cachedSize.HasValue)
        {
            return cachedSize.Value;
        }
        
        var totalSize = await BuildProjectStorageSizeFromDb(projectId);
        
        await CacheService.Instance.SetAsync(cacheKey, totalSize, _storageSizeCacheTtl);

        return totalSize;
    }
    
    /// <summary>
    ///     Gets total bytes for a specific object storage within an optional project scope
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">ID of the project in which the object storage belongs</param>
    /// <param name="objectStorageId">ID of the object storage from which to get the total bytes</param>
    public async Task<StorageSizeDto> GetObjectStorageSize(
        long organizationId,
        long? projectId,
        long objectStorageId)
    {
        await ExistenceHelper.EnsureOrganizationExistsAsync(_context, organizationId);

        var query = _context.Records
            .Where(r =>
                r.OrganizationId == organizationId &&
                r.ObjectStorageId == objectStorageId &&
                !r.IsArchived &&
                r.FileSize != null);

        if (projectId.HasValue)
        {
            query = query.Where(r => r.ProjectId == projectId.Value);
        }

        var totalBytes = await query.SumAsync(r => r.FileSize ?? 0);
        
        return new StorageSizeDto{ Bytes = totalBytes };
    }

    /// <summary>
    ///     Gets total bytes for each object storage in a project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">ID of the project from which to get the total bytes</param>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<StorageSizeDto> GetProjectStorageSize(
        long organizationId, 
        long projectId)
    {
        // validate org and project exist and match
        await ExistenceHelper.EnsureOrganizationExistsAsync(_context, organizationId);
        var project = await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId);
        
        if (project.OrganizationId != organizationId)
            throw new InvalidOperationException($"Project {projectId} does not belong to organization {organizationId}");
        
        var totalBytes = await GetProjectStorageSizeBytes(projectId);
        
        return new StorageSizeDto{ Bytes = totalBytes };
    }

    /// <summary>
    ///     Gets total bytes for each object storage in an organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization from which to get the total bytes</param>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<StorageSizeDto> GetOrganizationStorageSize(long organizationId)
    {
        // verify organization exists
        await ExistenceHelper.EnsureOrganizationExistsAsync(_context, organizationId);

        var projectIds = await _context.Projects
            .Where(p => p.OrganizationId == organizationId && !p.IsArchived)
            .Select(p => p.Id)
            .ToListAsync();

        long totalBytes = 0;

        foreach (var projectId in projectIds)
        {
            totalBytes += await GetProjectStorageSizeBytes(projectId);
        }
        
        return new StorageSizeDto{ Bytes = totalBytes };
    }

    /// <summary>
    ///     Gets total bytes for each object storage system-wide
    /// </summary>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<StorageSizeDto> GetSystemStorageSize()
    {
        var projectIds = await _context.Projects
            .Where(p => !p.IsArchived)
            .Select(p => p.Id)
            .ToListAsync();
        
        long totalBytes = 0;

        foreach (var projectId in projectIds)
        {
            totalBytes += await GetProjectStorageSizeBytes(projectId);
        }
        
        return new StorageSizeDto{ Bytes = totalBytes };
    }

    /// <summary>
    ///     Gets datasource count for project
    /// </summary>
    /// <param name="projectId">The ID of the organization for which the data source belongs to</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>Quantity of data sources system-wide</returns>
    public async Task<int> GetProjectDataSourceCount(long projectId, bool hideArchived = true)
    {
        var dsQuery = _context.DataSources
            .AsQueryable();

        dsQuery = dsQuery.Where(d => d.ProjectId == projectId);    

        // hide archived data sources
        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        return await dsQuery.CountAsync();
    }

    /// <summary>
    ///     Gets datasource count for organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectIds">ID's of the projects whose data sources are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>Quantity of data sources system-wide</returns>
    public async Task<int> GetOrganizationDataSourceCount(
        long organizationId, 
        long[]? projectIds, 
        bool hideArchived = true
        )
    {
        var dsQuery = _context.DataSources
            .AsQueryable();

        dsQuery = dsQuery.Where(d => d.OrganizationId == organizationId);

        // If project ids supplied, inherit org level data sources too 
        if (projectIds is { Length: > 0 })
            dsQuery = dsQuery.Where(d =>
                (d.ProjectId.HasValue && projectIds.Contains(d.ProjectId.Value)) || d.ProjectId == null);

        // hide archived data sources
        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        return await dsQuery.CountAsync();
    }
    
    /// <summary>
    /// Gets the number of unique data modalities in the organization's records
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public async Task<int> GetOrganizationDataModalityCount(
        long organizationId,
        long? projectId)
    {
        return await _context.Records
            .Where(r => r.FileType != null)
            .Where(r => r.OrganizationId == organizationId &&
                        (projectId == null || r.ProjectId == projectId))
            .Select(r => r.FileType)
            .Distinct()
            .CountAsync();
    }


    /// <summary>
    ///     Gets datasource count system-wide
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>Quantity of data sources system-wide</returns>
    public async Task<int> GetSystemDataSourceCount(bool hideArchived = true)
    {
        var dsQuery = _context.DataSources
            .AsQueryable();

        // hide archived data sources
        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        return await dsQuery.CountAsync();
    }
    
    /// <summary>
    ///     Get record count for a scope
    /// </summary>
    /// <param name="organizationId">The ID of the organization the records belong</param>
    /// <param name="projectId">The ID of the project the records belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    public async Task<int> GetRecordCount(long? organizationId, long? projectId, bool hideArchived)
    {
        var projectIds = projectId.HasValue ? new[] { projectId.Value } : null;
        return await GetRecordCount(organizationId, projectIds, hideArchived);
    }
    
    /// <summary>
    ///     Get record count for a scope
    /// </summary>
    /// <param name="organizationId">The ID of the organization the records belong</param>
    /// <param name="projectIds">The IDs of the projects the records belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    public async Task<int> GetRecordCount(long? organizationId, long[]? projectIds, bool hideArchived)
    {
        var recordQuery = _context.Records.AsQueryable();
        
        if (organizationId != null) recordQuery = recordQuery.Where(r => r.OrganizationId == organizationId);

        if (projectIds is { Length: > 0 })
            recordQuery = recordQuery.Where(r => projectIds.Contains(r.ProjectId));
        
        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);
        
        return await recordQuery.CountAsync();
    }

    /// <summary>
    ///     Get Files Count
    /// </summary>
    /// <param name="organizationId">The ID of the organization the files belong</param>
    /// <param name="projectId">The ID of the project the files belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The record count for the given scope</returns>
    public async Task<int> GetFileCount(long? organizationId, long? projectId, bool hideArchived = true)
    {
        var projectIds = projectId.HasValue ? new[] { projectId.Value } : null;
        return await GetFileCount(organizationId, projectIds, hideArchived);
    }
    
    /// <summary>
    ///     Get Files Count
    /// </summary>
    /// <param name="organizationId">The ID of the organization the files belong</param>
    /// <param name="projectIds">The IDs of the projects the files belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The record count for the given scope</returns>
    public async Task<int> GetFileCount(long? organizationId, long[]? projectIds, bool hideArchived)
    {
        var fileQuery = _context.Records
            .Where(r => r.Uri != null)
            .AsQueryable();
        
        if (organizationId != null) fileQuery = fileQuery.Where(r => r.OrganizationId == organizationId);

        if (projectIds is { Length: > 0 })
            fileQuery = fileQuery.Where(r => projectIds.Contains(r.ProjectId));
        
        if (hideArchived) fileQuery = fileQuery.Where(r => !r.IsArchived);
        
        return await fileQuery.CountAsync();
    }
}