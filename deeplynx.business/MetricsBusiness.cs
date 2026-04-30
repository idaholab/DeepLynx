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

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    /// <param name="fileBusinessFactory">Factory to create storage-specific file business instances</param>
    public MetricsBusiness(DeeplynxContext context, IFileBusinessFactory fileBusinessFactory)
    {
        _context = context;
        _fileBusinessFactory = fileBusinessFactory;
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
        var objectStorage = await _context.ObjectStorages
            .FirstOrDefaultAsync(os => os.Id == objectStorageId 
                                    && os.OrganizationId == organizationId
                                    && !os.IsArchived);
        
        if (objectStorage == null)
            throw new KeyNotFoundException($"Object storage {objectStorageId} not found");
        
        // Validate project scope if provided
        if (projectId.HasValue)
        {
            await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId.Value);

            if (objectStorage.ProjectId.HasValue && objectStorage.ProjectId != projectId)
                throw new InvalidOperationException(
                    $"Object storage {objectStorageId} does not belong to project {projectId}");
        }

        // Build prefix based on scope and storage type
        long? effectiveProjectId = projectId ?? objectStorage.ProjectId;
        var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
        var prefix = fileBusiness.BuildPrefix(organizationId, effectiveProjectId);
        
        var configData = DeserializeConfig(objectStorage.Config);
        var totalBytes = await fileBusiness.GetStorageSize(prefix, configData);
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
        
        var objectStorages = await _context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId 
                      && (os.ProjectId == projectId || os.ProjectId == null)
                      && !os.IsArchived)
            .ToListAsync();
        
        foreach (var objectStorage in objectStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                var prefix = fileBusiness.BuildPrefix(organizationId, projectId);
                
                var configData = DeserializeConfig(objectStorage.Config);
                var osBytes = await fileBusiness.GetStorageSize(prefix, configData);
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
        
        var objectStorages = await _context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId && !os.IsArchived)
            .ToListAsync();
        
        foreach (var objectStorage in objectStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                var prefix = fileBusiness.BuildPrefix(organizationId, null);
                
                var configData = DeserializeConfig(objectStorage.Config);
                var osBytes = await fileBusiness.GetStorageSize(prefix, configData);
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
        var objectStorages = await _context.ObjectStorages
            .Where(os => !os.IsArchived)
            .GroupBy(os => os.Config)
            .Select(g => g.First())
            .ToListAsync();
        
        foreach (var objectStorage in objectStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                // Empty prefix for system-wide (get everything in this storage)
                var prefix = "";
                
                var configData = DeserializeConfig(objectStorage.Config);
                var osBytes = await fileBusiness.GetStorageSize(prefix, configData);
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
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>Quantity of data sources system-wide</returns>
    public async Task<int> GetOrganizationDataSourceCount(long organizationId, bool hideArchived = true)
    {
        var dsQuery = _context.DataSources
            .AsQueryable();

        dsQuery = dsQuery.Where(d => d.OrganizationId == organizationId);

        // hide archived data sources
        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        return await dsQuery.CountAsync();
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
    
    private static ObjectStorageConfigDto DeserializeConfig(string config)
    {
        return JsonConvert.DeserializeObject<ObjectStorageConfigDto>(config)
               ?? throw new InvalidOperationException("Config data for object storage is null or invalid");
    }
}