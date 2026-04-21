using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
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
    public async Task<long> GetObjectStorageSize(
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
        if (projectId.HasValue && objectStorage.ProjectId.HasValue && objectStorage.ProjectId != projectId)
            throw new InvalidOperationException($"Object storage {objectStorageId} does not belong to project {projectId}");
        
        // Build prefix based on scope and storage type
        long? effectiveProjectId = projectId ?? objectStorage.ProjectId;
        var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
        var prefix = fileBusiness.BuildPrefix(organizationId, effectiveProjectId);
        
        var configData = DeserializeConfig(objectStorage.Config);
        return await fileBusiness.GetStorageSize(prefix, configData);
    }

    /// <summary>
    ///     Gets total bytes for each object storage in a project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">ID of the project from which to get the total bytes</param>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<Dictionary<long, long>> GetProjectStorageSize(
        long organizationId, 
        long projectId)
    {
        var results = new Dictionary<long, long>();
        
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
                var totalBytes = await fileBusiness.GetStorageSize(prefix, configData);
                results[objectStorage.Id] = totalBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
                results[objectStorage.Id] = 0;
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Gets total bytes for each object storage in an organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization from which to get the total bytes</param>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<Dictionary<long, long>> GetOrganizationStorageSize(long organizationId)
    {
        var results = new Dictionary<long, long>();
        
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
                var totalBytes = await fileBusiness.GetStorageSize(prefix, configData);
                results[objectStorage.Id] = totalBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
                results[objectStorage.Id] = 0;
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Gets total bytes for each object storage system-wide
    /// </summary>
    /// <returns>Dictionary of objectStorageId -> total bytes</returns>
    public async Task<Dictionary<long, long>> GetSystemStorageSize()
    {
        var results = new Dictionary<long, long>();
        
        var objectStorages = await _context.ObjectStorages
            .Where(os => !os.IsArchived)
            .ToListAsync();
        
        foreach (var objectStorage in objectStorages)
        {
            try
            {
                var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);
                // Empty prefix for system-wide (get everything in this storage)
                var prefix = "";
                
                var configData = DeserializeConfig(objectStorage.Config);
                var totalBytes = await fileBusiness.GetStorageSize(prefix, configData);
                results[objectStorage.Id] = totalBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for object storage {objectStorage.Id}: {ex.Message}");
                results[objectStorage.Id] = 0;
            }
        }
        
        return results;
    }

    private static ObjectStorageConfigDto DeserializeConfig(string config)
    {
        return JsonConvert.DeserializeObject<ObjectStorageConfigDto>(config)
               ?? throw new InvalidOperationException("Config data for object storage is null or invalid");
    }
}