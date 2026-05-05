using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

// TODO: add event system
public class ObjectStorageBusiness : IObjectStorageBusiness
{
    private readonly DeeplynxContext _context;
    private readonly EncryptionHelper _encryptionHelper;

    public ObjectStorageBusiness(DeeplynxContext context, EncryptionHelper encryptionHelper)
    {
        _encryptionHelper = encryptionHelper;
        _context = context;
    }

    /// <summary>
    ///     Gets all the object storages for a project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storages belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived ObjectStorages from the result </param>
    public async Task<List<ObjectStorageResponseDto>> GetAllObjectStorages(
        long organizationId,
        long? projectId,
        bool hideArchived)
    {
        var query = _context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        if (hideArchived)
            query = query.Where(os => !os.IsArchived);

        var objectStorages = await query.ToListAsync();
        return objectStorages
            .Select(os => new ObjectStorageResponseDto
            {
                Id = os.Id,
                Name = os.Name,
                Type = os.Type,
                ProjectId = os.ProjectId,
                OrganizationId = os.OrganizationId,
                Default = os.Default,
                LastUpdatedAt = os.LastUpdatedAt,
                LastUpdatedBy = os.LastUpdatedBy,
                IsArchived = os.IsArchived
            }).ToList();
    }

    /// <summary>
    ///     Gets a single object storage
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">ID of object storage</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storage from the result</param>
    /// <exception cref="KeyNotFoundException">Thrown when the object storage is not found or archived</exception>
    public async Task<ObjectStorageResponseDto> GetObjectStorage(
        long organizationId,
        long? projectId,
        long objectStorageId,
        bool hideArchived)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();

        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (hideArchived && returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} is archived");

        return new ObjectStorageResponseDto
        {
            Id = returnedObjectStorage.Id,
            Name = returnedObjectStorage.Name,
            Type = returnedObjectStorage.Type,
            ProjectId = returnedObjectStorage.ProjectId,
            OrganizationId = returnedObjectStorage.OrganizationId,
            Default = returnedObjectStorage.Default,
            LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
            LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
            IsArchived = returnedObjectStorage.IsArchived
        };
    }

    /// <summary>
    ///     Creates an object storage
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="dto">A data transfer object with details on the new object storage to be created.</param>
    public async Task<ObjectStorageResponseDto> CreateObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        CreateObjectStorageRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var hasFilesystem = dto.Config.MountPath is not null;
        var hasAzure = dto.Config.AzureObjectConfig is not null;
        var hasAws = dto.Config.AwsConnectionString is not null;


        var populatedCount = new[]
        {
            hasFilesystem,
            hasAzure,
            hasAws
        }.Count(x => x);

        if (populatedCount != 1)
            throw new InvalidOperationException(
                $"Exactly one config must be provided, you provided {populatedCount}. Check for empty strings and/or objects.");

        string type;
        if (hasFilesystem)
        {
            if (string.IsNullOrWhiteSpace(dto.Config.MountPath))
                throw new ArgumentException("Mount path cannot be empty string");
            type = "filesystem";
        }
        else if (hasAzure)
        {
            if (string.IsNullOrWhiteSpace(dto.Config.AzureObjectConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is empty");

            if (string.IsNullOrWhiteSpace(dto.Config.AzureObjectConfig.AzureContainerName))
            {
                Env.Load("../.env");
                var azureContainerName = Environment.GetEnvironmentVariable("AZURE_CONTAINER_NAME");
                if (string.IsNullOrWhiteSpace(azureContainerName))
                    throw new ArgumentException(
                        "Default Azure container name is not set or is empty, please provide a container name or set default using env variables");

                dto.Config.AzureObjectConfig.AzureContainerName = azureContainerName;
            }

            type = "azure_object";
        }
        else // hasAws
        {
            if (string.IsNullOrWhiteSpace(dto.Config.AwsConnectionString))
                throw new ArgumentException("AWS connection string cannot be empty");
            type = "aws_s3";
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var newObjectStorage = new ObjectStorage
            {
                Name = dto.Name,
                Type = type,
                Default = dto.Default,
                ProjectId = projectId,
                OrganizationId = organizationId,
                ConfigEncrypted = SerializeAndEncryptConfig(dto.Config),
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId
            };

            _context.ObjectStorages.Add(newObjectStorage);
            await _context.SaveChangesAsync();

            // reset the defaults at the project or org level
            if (dto.Default)
            {
                if (projectId.HasValue)
                    await ResetProjectDefaults(projectId.Value, newObjectStorage.Id);
                else
                    await ResetOrganizationDefaults(organizationId, newObjectStorage.Id);
            }

            await transaction.CommitAsync();

            return new ObjectStorageResponseDto
            {
                Id = newObjectStorage.Id,
                Name = newObjectStorage.Name,
                Type = newObjectStorage.Type,
                ProjectId = newObjectStorage.ProjectId,
                OrganizationId = newObjectStorage.OrganizationId,
                Default = newObjectStorage.Default,
                LastUpdatedAt = newObjectStorage.LastUpdatedAt,
                LastUpdatedBy = newObjectStorage.LastUpdatedBy,
                IsArchived = newObjectStorage.IsArchived
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw new Exception("Unable to create object storage");
        }
    }

    /// <summary>
    ///     Updates an object storage
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">ID of object storage</param>
    /// <param name="dto">A data transfer object with details on object storage fields to be updated</param>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<ObjectStorageResponseDto> UpdateObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        long objectStorageId,
        UpdateObjectStorageRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        // Organization os cannot be updated from a project level
        if (projectId.HasValue && returnedObjectStorage.ProjectId == null)
            throw new InvalidOperationException(
                "Organization object storages cannot be updated from the child projects.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // reset the defaults at the project or org level
            // * Before saving the updated object storage
            if (dto.Default)
            {
                if (projectId.HasValue)
                    await ResetProjectDefaults(projectId.Value, returnedObjectStorage.Id);
                else
                    await ResetOrganizationDefaults(organizationId, returnedObjectStorage.Id);
            }
            
            returnedObjectStorage.Name = dto.Name;
            returnedObjectStorage.Default = dto.Default;
            returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            returnedObjectStorage.LastUpdatedBy = currentUserId;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return new ObjectStorageResponseDto
            {
                Id = returnedObjectStorage.Id,
                Name = returnedObjectStorage.Name,
                Type = returnedObjectStorage.Type,
                ProjectId = returnedObjectStorage.ProjectId,
                OrganizationId = returnedObjectStorage.OrganizationId,
                Default = returnedObjectStorage.Default,
                LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
                LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
                IsArchived = returnedObjectStorage.IsArchived
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw new Exception("Unable to update object storage");
        }
    }

    /// <summary>
    ///     Delete an object storage by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">ID of object storage</param>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<bool> DeleteObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        long objectStorageId)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);


        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be deleted." +
                                                " Please assign new default storage before deleting.");

        // Organization os cannot be updated from a project level
        if (projectId.HasValue && returnedObjectStorage.ProjectId == null)
            throw new InvalidOperationException(
                "Organization object storages cannot be updated from the child projects.");

        _context.ObjectStorages.Remove(returnedObjectStorage);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Archives (soft deletes) an object storage by ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">ID of object storage</param>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<bool> ArchiveObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        long objectStorageId)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (returnedObjectStorage.IsArchived)
            throw new InvalidOperationException($"Object storage with id {objectStorageId} is already archived");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be archived." +
                                                " Please assign new default storage before archiving.");

        // Organization os cannot be updated from a project level
        if (projectId.HasValue && returnedObjectStorage.ProjectId == null)
            throw new InvalidOperationException(
                "Organization object storages cannot be updated from the child projects.");

        returnedObjectStorage.IsArchived = true;
        returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedObjectStorage.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Unarchives a data storage
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">ID of object storage</param>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<bool> UnarchiveObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        long objectStorageId)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (!returnedObjectStorage.IsArchived)
            throw new InvalidOperationException($"Object storage with id {objectStorageId} is not archived");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be archived." +
                                                " Please assign new default storage before archiving.");
        // Organization os cannot be updated from a project level
        if (projectId.HasValue && returnedObjectStorage.ProjectId == null)
            throw new InvalidOperationException(
                "Organization object storages cannot be updated from the child projects.");

        returnedObjectStorage.IsArchived = false;
        returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedObjectStorage.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Gets default object storage for project or org
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <exception cref="KeyNotFoundException">Thrown when the object storage is not found or archived</exception>
    public async Task<ObjectStorageResponseDto> GetDefaultObjectStorage(
        long organizationId,
        long? projectId)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Default && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null)
                .OrderByDescending(os => os.ProjectId.HasValue);
        else
            query = query.Where(os => os.ProjectId == null);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();

        if (returnedObjectStorage is null)
            throw new KeyNotFoundException("Default object storage not found");

        return new ObjectStorageResponseDto
        {
            Id = returnedObjectStorage.Id,
            Name = returnedObjectStorage.Name,
            Type = returnedObjectStorage.Type,
            ProjectId = returnedObjectStorage.ProjectId,
            OrganizationId = returnedObjectStorage.OrganizationId,
            Default = returnedObjectStorage.Default,
            LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
            LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
            IsArchived = returnedObjectStorage.IsArchived
        };
    }

    /// <summary>
    ///     Sets the default object storage for a project or org
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">ID of the project in which the object storage belongs</param>
    /// <param name="objectStorageId">ID of the object storage to change to default</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<ObjectStorageResponseDto> SetDefaultObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        long objectStorageId)
    {
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId && os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            returnedObjectStorage.Default = true;
            returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            returnedObjectStorage.LastUpdatedBy = currentUserId;
            await _context.SaveChangesAsync();

            // reset the defaults at the project or org level
            if (projectId.HasValue)
                await ResetProjectDefaults(projectId.Value, returnedObjectStorage.Id);
            else
                await ResetOrganizationDefaults(organizationId, returnedObjectStorage.Id);

            await transaction.CommitAsync();

            return new ObjectStorageResponseDto
            {
                Id = returnedObjectStorage.Id,
                Name = returnedObjectStorage.Name,
                Type = returnedObjectStorage.Type,
                ProjectId = returnedObjectStorage.ProjectId,
                OrganizationId = returnedObjectStorage.OrganizationId,
                Default = returnedObjectStorage.Default,
                LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
                LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
                IsArchived = returnedObjectStorage.IsArchived
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw new Exception($"Unable to set object storage {objectStorageId} as default");
        }
    }
    
    /// <summary>
    ///     For internal use only (don't hook an API up to this)- returns a single decrypted object storage config
    /// </summary>
    /// <param name="objectStorageId">ID of the object storage to get config for</param>
    /// <returns>A single object storage with its decrypted config</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<ObjectStorageDecryptedDto> GetDecryptedObjectStorage(long objectStorageId)
    {
        // filter out archived by default instead of providing param
        var query = _context.ObjectStorages
            .Where(os => os.Id == objectStorageId)
            .Where(os => !os.IsArchived);
        
        var returnedObjectStorage = await query.FirstOrDefaultAsync();

        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        return new ObjectStorageDecryptedDto
        {
            Id = returnedObjectStorage.Id,
            Name = returnedObjectStorage.Name,
            Type = returnedObjectStorage.Type,
            ProjectId = returnedObjectStorage.ProjectId,
            OrganizationId = returnedObjectStorage.OrganizationId,
            Default = returnedObjectStorage.Default,
            LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
            LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
            IsArchived = returnedObjectStorage.IsArchived,
            Config = DeserializeAndDecryptConfig(returnedObjectStorage.ConfigEncrypted)
        };
    }

    /// <summary>
    ///     For internal use only (don't hook an API up to this)- returns the decrypted object storage config
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="projectId">ID of the project in which the object storage belongs</param>
    /// <param name="objectStorageIds">IDs of the object storage configs to get configs for</param>
    /// <returns>A list of object storages, including their decrypted configs</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<List<ObjectStorageDecryptedDto>> GetDecryptedObjectStorages(
        long? organizationId,
        long? projectId,
        List<long>? objectStorageIds)
    {
        // filter out archived by default instead of providing a param
        var query = _context.ObjectStorages
            .Where(os => !os.IsArchived);

        if (organizationId.HasValue)
            query = query.Where(os => os.OrganizationId == organizationId);

        if (projectId.HasValue)
            query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null);
        else
            query = query.Where(os => os.ProjectId == null);

        if (objectStorageIds != null && objectStorageIds.Any())
            query = query.Where(os => objectStorageIds.Contains(os.Id));
        
        var objectStorages = await query.ToListAsync();
        return objectStorages
            .Select(os => new ObjectStorageDecryptedDto
            {
                Id = os.Id,
                Name = os.Name,
                Type = os.Type,
                ProjectId = os.ProjectId,
                OrganizationId = os.OrganizationId,
                Default = os.Default,
                LastUpdatedAt = os.LastUpdatedAt,
                LastUpdatedBy = os.LastUpdatedBy,
                IsArchived = os.IsArchived,
                Config = DeserializeAndDecryptConfig(os.ConfigEncrypted)
            }).ToList();
    }
    
    // Private Helpers
    private String SerializeAndEncryptConfig(ObjectStorageConfigDto config)
    {
        return _encryptionHelper.SerializeAndEncrypt(config);
    }

    private ObjectStorageConfigDto DeserializeAndDecryptConfig(string encryptedConfig)
    {
        return _encryptionHelper.DeserializeAndDecrypt<ObjectStorageConfigDto>(encryptedConfig);
    }
    
    private async Task ResetProjectDefaults(long projectId, long newDefaultId)
    {
        // check for existing defaults at the project level and remove them from being default
        await _context.ObjectStorages
            .Where(os => os.ProjectId == projectId && os.Id != newDefaultId)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }

    private async Task ResetOrganizationDefaults(long organizationId, long newDefaultId)
    {
        // check for existing defaults at the org level and remove them from being default
        await _context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId && os.ProjectId == null && os.Id != newDefaultId)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }
}