using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using deeplynx.interfaces;
using deeplynx.models;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

// TODO: add event system
public class ObjectStorageBusiness : IObjectStorageBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IFileBusiness _fileAzureBusiness;
    private readonly EncryptionHelper _encryptionHelper;
    private readonly ILogger<ObjectStorageBusiness>? _logger;
    private static readonly TimeSpan ObjectStorageCacheTtl = TimeSpan.FromHours(1);

    public ObjectStorageBusiness(DeeplynxContext context, EncryptionHelper encryptionHelper,
        IFileBusiness fileAzureBusiness, ILogger<ObjectStorageBusiness>? logger = null)
    {
        _encryptionHelper = encryptionHelper;
        _context = context;
        _fileAzureBusiness = fileAzureBusiness;
        _logger = logger;
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
                FilesDeletable = os.FilesDeletable,
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
            FilesDeletable = returnedObjectStorage.FilesDeletable,
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
    /// <param name="createContainer">A bool to create a container</param>
    public async Task<ObjectStorageResponseDto> CreateObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        CreateObjectStorageRequestDto dto,
        bool createContainer = true)
    {
        ValidationHelper.ValidateModel(dto);

        var hasFilesystem = dto.Config.MountPath is not null;
        var hasAzure = dto.Config.AzureObjectConfig is not null;
        var hasAws = dto.Config.AwsConnectionString is not null;

        var isLocalEnv = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BACKEND_BASE_URL"));

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
        if (hasFilesystem && isLocalEnv)
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
                FilesDeletable = dto.FilesDeletable,
                ProjectId = projectId,
                OrganizationId = organizationId,
                ConfigEncrypted = SerializeAndEncryptConfig(dto.Config),
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId
            };

            _context.ObjectStorages.Add(newObjectStorage);
            await _context.SaveChangesAsync();

            if (hasAzure && createContainer && !dto.Config.AzureObjectConfig.ExistingContainer)
            {
                string containerName;
                if (projectId == null)
                {
                    containerName = ContainerName.UniqueContainerNameFromString(dto.Config.AzureObjectConfig?.AzureContainerName ?? "container");
                }
                else
                {
                    containerName = dto.Config.AzureObjectConfig?.AzureContainerName ?? ContainerName.UniqueContainerNameFromString("container");
                }

                dto.Config.AzureObjectConfig?.AzureContainerName = containerName;

                await _fileAzureBusiness.CreateContainer(
                    organizationId: organizationId,
                    containerName: containerName,
                    connectionString: dto.Config.AzureObjectConfig?.AzureConnectionString,
                    existingContainer: dto.Config.AzureObjectConfig.ExistingContainer);
            }

            if (dto.Default)
            {
                if (projectId.HasValue)
                {
                    var project = await _context.Projects
                        .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                        .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Project with id {projectId.Value} not found");

                    project.DefaultObjectStorageId = (int?)newObjectStorage.Id;
                    project.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    project.LastUpdatedBy = currentUserId;
                }
                else
                {
                    var organization = await _context.Organizations
                        .Where(o => o.Id == organizationId)
                        .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Organization with id {organizationId} not found");

                    organization.DefaultObjectStorageId = (int?)newObjectStorage.Id;
                    organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    organization.LastUpdatedBy = currentUserId;
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Update cached default object storage
            if (dto.Default)
            {
                await UpdateDefaultObjectStorageCache(newObjectStorage.Id, organizationId, projectId);
            }

            return new ObjectStorageResponseDto
            {
                Id = newObjectStorage.Id,
                Name = newObjectStorage.Name,
                Type = newObjectStorage.Type,
                ProjectId = newObjectStorage.ProjectId,
                OrganizationId = newObjectStorage.OrganizationId,
                Default = dto.Default,
                LastUpdatedAt = newObjectStorage.LastUpdatedAt,
                LastUpdatedBy = newObjectStorage.LastUpdatedBy,
                FilesDeletable = newObjectStorage.FilesDeletable,
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
            if (dto.Default)
            {
                if (projectId.HasValue)
                {
                    var project = await _context.Projects
                        .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                        .FirstOrDefaultAsync();

                    if (project == null)
                        throw new KeyNotFoundException($"Project with id {projectId.Value} not found");

                    project.DefaultObjectStorageId = (int?)returnedObjectStorage.Id;
                    project.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    project.LastUpdatedBy = currentUserId;
                }
                else
                {
                    var organization = await _context.Organizations
                        .Where(o => o.Id == organizationId)
                        .FirstOrDefaultAsync();

                    if (organization == null)
                        throw new KeyNotFoundException($"Organization with id {organizationId} not found");

                    organization.DefaultObjectStorageId = (int?)returnedObjectStorage.Id;
                    organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    organization.LastUpdatedBy = currentUserId;
                }
            }

            // Update the object storage fields (excluding Default flag)
            returnedObjectStorage.Name = dto.Name;
            returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            returnedObjectStorage.LastUpdatedBy = currentUserId;
            returnedObjectStorage.FilesDeletable = dto.FilesDeletable;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Update cached default object storage
            if (dto.Default)
            {
                await UpdateDefaultObjectStorageCache(objectStorageId, organizationId, projectId);
            }

            return new ObjectStorageResponseDto
            {
                Id = returnedObjectStorage.Id,
                Name = returnedObjectStorage.Name,
                Type = returnedObjectStorage.Type,
                ProjectId = returnedObjectStorage.ProjectId,
                OrganizationId = returnedObjectStorage.OrganizationId,
                Default = dto.Default,
                LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
                LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
                FilesDeletable = returnedObjectStorage.FilesDeletable,
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

        long? defaultObjectStorageId = null;
        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                .Select(p => new { p.DefaultObjectStorageId })
                .FirstOrDefaultAsync();

            if (project != null)
                defaultObjectStorageId = project.DefaultObjectStorageId;
        }
        else
        {
            var organization = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .Select(o => new { o.DefaultObjectStorageId })
                .FirstOrDefaultAsync();

            if (organization != null)
                defaultObjectStorageId = organization.DefaultObjectStorageId;
        }

        if (defaultObjectStorageId == objectStorageId)
            throw new InvalidOperationException("Default object storage cannot be archived. Please assign new default storage before archiving.");

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

        long? defaultObjectStorageId = null;
        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                .Select(p => new { p.DefaultObjectStorageId })
                .FirstOrDefaultAsync();

            if (project != null)
                defaultObjectStorageId = project.DefaultObjectStorageId;
        }
        else
        {
            var organization = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .Select(o => new { o.DefaultObjectStorageId })
                .FirstOrDefaultAsync();

            if (organization != null)
                defaultObjectStorageId = organization.DefaultObjectStorageId;
        }

        if (defaultObjectStorageId == objectStorageId)
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
    /// <exception cref="KeyNotFoundException">Thrown when the default object storage is not found or archived</exception>
    public async Task<ObjectStorageResponseDto> GetDefaultObjectStorage(
        long organizationId,
        long? projectId)
    {
        long? defaultObjectStorageId = null;

        string cacheKey = projectId.HasValue
            ? CacheKeys.ProjectDefaultObjectStorage(projectId.Value)
            : CacheKeys.OrganizationDefaultObjectStorage(organizationId);

        long? cachedId = null;
        try
        {
            cachedId = await CacheService.Instance.GetAsync<long?>(cacheKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache read failed for default-object-storage key {CacheKey}", cacheKey);
        }

        bool cacheHit = cachedId.HasValue;

        // Check the cache before querying the db
        if (cacheHit)
        {
            defaultObjectStorageId = cachedId;
        }
        else
        {
            if (projectId.HasValue)
            {
                var project = await _context.Projects
                    .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                    .Select(p => new { p.DefaultObjectStorageId })
                    .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Project with id {projectId.Value} not found");
                defaultObjectStorageId = project.DefaultObjectStorageId;
            }
            if (defaultObjectStorageId == null)
            {
                var organization = await _context.Organizations
                    .Where(o => o.Id == organizationId)
                    .Select(o => new { o.DefaultObjectStorageId })
                    .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Organization with id {organizationId} not found");
                defaultObjectStorageId = organization.DefaultObjectStorageId;
            }
        }

        if (defaultObjectStorageId == null)
        {
            var query = _context.ObjectStorages
            .Where(os => os.Default && os.OrganizationId == organizationId);

            if (projectId.HasValue)
                query = query.Where(os => os.ProjectId == projectId || os.ProjectId == null)
                    .OrderByDescending(os => os.ProjectId.HasValue);
            else
                query = query.Where(os => os.ProjectId == null);

            var defaultObjectStorageDto = await query.FirstOrDefaultAsync();

            defaultObjectStorageId = (defaultObjectStorageDto?.Id) ?? throw new KeyNotFoundException("Default object storage not found");
        }


        var returnedObjectStorage = await _context.ObjectStorages
            .Where(os => os.Id == defaultObjectStorageId && !os.IsArchived)
            .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Default object storage not found or is archived");

        // Repopulate cache on miss for subsequent reads
        if (!cacheHit)
        {
            try
            {
                await CacheService.Instance.SetAsync(cacheKey, returnedObjectStorage.Id, ObjectStorageCacheTtl);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache population failed for default-object-storage key {cacheKey}", cacheKey);
            }
        }

        return new ObjectStorageResponseDto
        {
            Id = returnedObjectStorage.Id,
            Name = returnedObjectStorage.Name,
            Type = returnedObjectStorage.Type,
            ProjectId = returnedObjectStorage.ProjectId,
            OrganizationId = returnedObjectStorage.OrganizationId,
            Default = true,
            LastUpdatedAt = returnedObjectStorage.LastUpdatedAt,
            LastUpdatedBy = returnedObjectStorage.LastUpdatedBy,
            FilesDeletable = returnedObjectStorage.FilesDeletable,
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


        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId.Value && p.OrganizationId == organizationId)
                .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Project with id {projectId.Value} not found");

            project.DefaultObjectStorageId = (int?)returnedObjectStorage.Id;
            project.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            project.LastUpdatedBy = currentUserId;
        }
        else
        {
            var organization = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .FirstOrDefaultAsync() ?? throw new KeyNotFoundException($"Organization with id {organizationId} not found");

            organization.DefaultObjectStorageId = (int?)returnedObjectStorage.Id;
            organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            organization.LastUpdatedBy = currentUserId;
        }

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        await UpdateDefaultObjectStorageCache(objectStorageId, organizationId, projectId);

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
            FilesDeletable = returnedObjectStorage.FilesDeletable,
            IsArchived = returnedObjectStorage.IsArchived
        };
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

    private async Task ResetOrganizationDefaults(long organizationId, long newDefaultId)
    {
        // check for existing defaults at the org level and remove them from being default
        await _context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId && os.ProjectId == null && os.Id != newDefaultId)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }

    private async Task UpdateDefaultObjectStorageCache(
        long objectStorageId,
        long organizationId,
        long? projectId)
    {
        var key = projectId.HasValue
            ? CacheKeys.ProjectDefaultObjectStorage(projectId.Value)
            : CacheKeys.OrganizationDefaultObjectStorage(organizationId);

        try
        {
            await CacheService.Instance.SetAsync(
                key,
                objectStorageId,
                ObjectStorageCacheTtl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Default object storage cache update failed for key {CacheKey}", key);
        }
    }
}