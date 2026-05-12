using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.MetricsDTOs;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace deeplynx.business;

public class MetricsBusiness : IMetricsBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IFileBusinessFactory _fileBusinessFactory;
    private readonly IObjectStorageBusiness _objectStorageBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    /// <param name="fileBusinessFactory">Factory to create storage-specific file business instances</param>
    public MetricsBusiness(
        DeeplynxContext context, 
        IFileBusinessFactory fileBusinessFactory, 
        IObjectStorageBusiness objectStorageBusiness)
    {
        _context = context;
        _fileBusinessFactory = fileBusinessFactory;
        _objectStorageBusiness = objectStorageBusiness;
    }

    // -------------------------------------------------------------------------
    // Storage Metrics
    // -------------------------------------------------------------------------

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
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(objectStorageId);

        // Build prefix based on scope and storage type
        long? effectiveProjectId = projectId ?? objectStorage.ProjectId;
        var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
        var prefix = fileBusiness.BuildPrefix(organizationId, effectiveProjectId);
        
        var totalBytes = await fileBusiness.GetStorageSize(prefix, objectStorage.Config);
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
        
        long totalBytes = 0;
        
        var objectStorages = await _objectStorageBusiness.GetDecryptedObjectStorages(
            organizationId, projectId, null);

        // Group by unique config to avoid counting shared storage backends multiple times
        var uniqueStorages = objectStorages
            .GroupBy(os => new { os.Type, ConfigJson = JsonConvert.SerializeObject(os.Config) })
            .Select(g => g.First())
            .ToList();
        
        foreach (var objectStorage in uniqueStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                var prefix = fileBusiness.BuildPrefix(organizationId, projectId);
                
                var osBytes = await fileBusiness.GetStorageSize(prefix, objectStorage.Config);
                totalBytes += osBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
            }
        }
        
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

        long totalBytes = 0;
        
        var objectStorages = await _objectStorageBusiness.GetDecryptedObjectStorages(
            organizationId, null, null);
        
        // Group by unique config to avoid counting shared storage backends multiple times
        var uniqueStorages = objectStorages
            .GroupBy(os => new { os.Type, ConfigJson = JsonConvert.SerializeObject(os.Config) })
            .Select(g => g.First())
            .ToList();
        
        foreach (var objectStorage in uniqueStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                var prefix = fileBusiness.BuildPrefix(organizationId, null);
                
                var osBytes = await fileBusiness.GetStorageSize(prefix, objectStorage.Config);
                totalBytes += osBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
            }
        }
        
        return new StorageSizeDto{ Bytes = totalBytes };
    }

    /// <summary>
    ///     Gets total bytes for each object storage system-wide
    /// </summary>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<StorageSizeDto> GetSystemStorageSize()
    {
        long totalBytes = 0;
        
        // select only the first of each unique config in order to eliminate duplicates
        var objectStorages = await _objectStorageBusiness.GetDecryptedObjectStorages(
            null, null, null);
        
        // Group by unique config to avoid counting shared storage backends multiple times
        var uniqueStorages = objectStorages
            .GroupBy(os => new { os.Type, ConfigJson = JsonConvert.SerializeObject(os.Config) })
            .Select(g => g.First())
            .ToList();
        
        foreach (var objectStorage in uniqueStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                // Empty prefix for system-wide (get everything in this storage)
                var prefix = "";
                
                var osBytes = await fileBusiness.GetStorageSize(prefix, objectStorage.Config);
                totalBytes += osBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
            }
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