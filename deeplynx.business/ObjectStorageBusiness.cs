using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

// TODO: add event system
public class ObjectStorageBusiness : IObjectStorageBusiness
{
    private readonly DeeplynxContext _context;

    public ObjectStorageBusiness(DeeplynxContext context)
    {
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
            query = query.Where(os => os.ProjectId == projectId);

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
            query = query.Where(os => os.ProjectId == projectId);
        
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
    /// <param name="makeDefault"> A boolean that tells whether to make the object storage default or not</param>
    public async Task<ObjectStorageResponseDto> CreateObjectStorage(
        long currentUserId,
        long organizationId,
        long? projectId,
        CreateObjectStorageRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        string type;
        if (dto.Config.ContainsKey("mountPath"))
            type = "filesystem";
        else if (dto.Config.ContainsKey("azureConnectionString"))
            type = "azure_object";
        else if (dto.Config.ContainsKey("awsConnectionString"))
            type = "aws_s3";
        else
            throw new KeyNotFoundException("Request does not contain recognized config");

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
                Config = dto.Config.ToJsonString(),
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
            query = query.Where(os => os.ProjectId == projectId);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            returnedObjectStorage.Name = dto.Name;
            returnedObjectStorage.Default = dto.Default;
            returnedObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            returnedObjectStorage.LastUpdatedBy = currentUserId;
            await _context.SaveChangesAsync();

            // reset the defaults at the project or org level
            if (dto.Default)
            {
                if (projectId.HasValue)
                    await ResetProjectDefaults(projectId.Value, returnedObjectStorage.Id);
                else
                    await ResetOrganizationDefaults(organizationId, returnedObjectStorage.Id);
            }

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
            query = query.Where(os => os.ProjectId == projectId);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null || returnedObjectStorage.IsArchived)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be deleted." +
                                                " Please assign new default storage before deleting.");

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
            query = query.Where(os => os.ProjectId == projectId);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (returnedObjectStorage.IsArchived)
            throw new InvalidOperationException($"Object storage with id {objectStorageId} is already archived");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be archived." +
                                                " Please assign new default storage before archiving.");

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
            query = query.Where(os => os.ProjectId == projectId);

        var returnedObjectStorage = await query.FirstOrDefaultAsync();
        if (returnedObjectStorage is null)
            throw new KeyNotFoundException($"Object storage with id {objectStorageId} not found");

        if (!returnedObjectStorage.IsArchived)
            throw new InvalidOperationException($"Object storage with id {objectStorageId} is not archived");

        if (returnedObjectStorage.Default)
            throw new InvalidOperationException("Default object storage cannot be archived." +
                                                " Please assign new default storage before archiving.");

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
            query = query.Where(os => os.ProjectId == projectId);
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