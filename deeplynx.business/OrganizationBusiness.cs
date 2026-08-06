using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DotNetEnv;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using System.Text.RegularExpressions;


namespace deeplynx.business;

public class OrganizationBusiness : IOrganizationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly ILogger<OrganizationBusiness> _logger;
    private readonly IRoleBusiness _roleBusiness;
    private readonly IObjectStorageBusiness _objectStorageBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrganizationBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for organization CRUD operations.</param>
    /// <param name="eventBusiness">Used for logging events during CRUD operations.</param>
    /// <param name="roleBusiness">Used to create default roles automatically on project creation.</param>
    /// <param name="logger"></param>
    public OrganizationBusiness(
        DeeplynxContext context,
        IEventBusiness eventBusiness,
        IRoleBusiness roleBusiness,
        ILogger<OrganizationBusiness> logger,
        IObjectStorageBusiness objectStorageBusiness
    )
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _roleBusiness = roleBusiness;
        _logger = logger;
        _objectStorageBusiness = objectStorageBusiness;
    }

    /// <summary>
    ///     Retrieves all organizations
    /// </summary>
    /// <param name="userId">The ID of the requesting user</param>
    /// <param name="isSysAdmin">Boolean determining if the requesting user is a system admin</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived organizations from the result</param>
    /// <returns>A list of organizations</returns>
    public async Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizations(long userId, bool hideArchived = true, bool isSysAdmin = false)
    {
        return await GetAllOrganizationsForUser(userId, hideArchived, isSysAdmin);
    }

    /// <summary>
    ///     Retrieves organizations for current user
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived organizations from the result</param>
    /// <param name="userId">ID of the User executing this method.</param>
    /// <param name="isSysAdmin">Boolean value determining if the requesting user is a system admin</param>
    /// <returns>A list of organizations</returns>
    public async Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizationsForUser(long userId,
        bool hideArchived = true, bool isSysAdmin = false)
    {
        var query = _context.Organizations.AsQueryable();

        if (!isSysAdmin)
        {
            query = query.Where(o => o.OrganizationUsers.Any(ou => ou.UserId == userId));
        }

        if (hideArchived)
        {
            query = query.Where(o => !o.IsArchived);
        }

        return await query
            .Select(o => new OrganizationResponseDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                LastUpdatedAt = o.LastUpdatedAt,
                LastUpdatedBy = o.LastUpdatedBy,
                IsArchived = o.IsArchived,
                DefaultOrg = o.DefaultOrg,
                Banner = o.Banner,
                Theme = o.Theme
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves a specific organization by ID
    /// </summary>
    /// <param name="organizationId">The ID by which to retrieve the organization</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived organizations from the result</param>
    /// <returns>The given organization to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if the organization is not found or is archived</exception>
    public async Task<OrganizationResponseDto> GetOrganization(long organizationId, bool hideArchived = true)
    {
        var organization = await _context.Organizations
            .Where(o => o.Id == organizationId)
            .FirstOrDefaultAsync();

        if (organization == null)
            throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");

        if (hideArchived && organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} is archived");

        return new OrganizationResponseDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            LastUpdatedAt = organization.LastUpdatedAt,
            LastUpdatedBy = organization.LastUpdatedBy,
            IsArchived = organization.IsArchived,
            DefaultOrg = organization.DefaultOrg,
            Banner = organization.Banner,
            Theme = organization.Theme,
            CreateContainerPerProject = organization.CreateContainerPerProject,
            DisableFileTransfer = organization.DisableFileTransfer

        };
    }

    /// <summary>
    ///     Creates a new organization and logs the creation event.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="isDefault">Indicates whether the organization will be made the default</param>
    /// <param name="dto">A data transfer object with details on the organization to be created.</param>
    /// <returns>The created organization.</returns>
    public async Task<OrganizationResponseDto> CreateOrganization(long currentUserId, CreateOrganizationRequestDto dto,
        bool isDefault = false)
    {
        ValidationHelper.ValidateModel(dto);
        var organization = new Organization
        {
            Name = dto.Name,
            Description = dto.Description,
            DefaultOrg = isDefault,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            Banner = dto.Banner,
            RequireSensitivityLabel = dto.RequireSensitivityLabel ?? false,
            Theme = "default",
            CreateContainerPerProject = dto.CreateContainerPerProject ?? false
        };

        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync();

        var orgUser = new OrganizationUser
        {
            UserId = currentUserId,
            OrganizationId = organization.Id,
            IsOrgAdmin = true
        };
        _context.OrganizationUsers.Add(orgUser);
        await _context.SaveChangesAsync();

        if (isDefault) await MakePreviousDefaultsFalse(organization.Id);

        await SetOrganizationDefaults(currentUserId, organization.Id);

        // Log create Organization event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organization.Id,
            null,
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "organization",
                EntityId = organization.Id,
                EntityName = organization.Name,
                Properties = JsonSerializer.Serialize(new { organization.Name }),
            });

        return new OrganizationResponseDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            LastUpdatedAt = organization.LastUpdatedAt,
            LastUpdatedBy = organization.LastUpdatedBy,
            IsArchived = organization.IsArchived,
            DefaultOrg = organization.DefaultOrg,
            Banner = organization.Banner,
            RequireSensitivityLabel = organization.RequireSensitivityLabel,
            Theme = organization.Theme,
            CreateContainerPerProject = organization.CreateContainerPerProject,
            DisableFileTransfer = organization.DisableFileTransfer
        };
    }

    /// <summary>
    ///     Update an organization by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to be updated</param>
    /// <param name="dto">A data transfer object with details on the organization to be updated</param>
    /// <returns>The updated organization</returns>
    /// <exception cref="KeyNotFoundException">Returned if organization to update was not found</exception>
    public async Task<OrganizationResponseDto> UpdateOrganization(long currentUserId, long organizationId,
        UpdateOrganizationRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var organization = await _context.Organizations.FindAsync(organizationId);

        if (organization == null || organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");

        // Validate that if the RequireSensitivityLabel is enabled all existing records have labels
        if (!organization.RequireSensitivityLabel && dto.RequireSensitivityLabel == true)
        {
            var hasUnlabeledRecords = await _context.Records
                .Include(r => r.Labels)
                .Where(r => r.OrganizationId == organizationId)
                .AnyAsync(r => !r.Labels.Any());

            if (hasUnlabeledRecords)
                throw new InvalidOperationException(
                    "Cannot require sensitivity labels: organization contains records without labels. " +
                    "Please label all existing records before enabling this requirement.");
        }

        if (dto.RequireSensitivityLabel != null)
            organization.RequireSensitivityLabel = dto.RequireSensitivityLabel.Value;

        if (dto.Theme != null)
        {
            organization.Theme = dto.Theme.Value.ToCamelCaseValue();
        }

        if (dto.CreateContainerPerProject != null)
        {
            organization.CreateContainerPerProject = dto.CreateContainerPerProject.Value;
        }

        if (dto.DisableFileTransfer != null)
        {
            organization.DisableFileTransfer = dto.DisableFileTransfer.Value;
        }

        organization.Name = dto.Name ?? organization.Name;
        organization.Description = dto.Description ?? organization.Description;
        organization.DefaultOrg = dto.DefaultOrg ?? organization.DefaultOrg;
        organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        organization.LastUpdatedBy = currentUserId;
        organization.Banner = dto.Banner;

        _context.Organizations.Update(organization);

        if (dto.DefaultOrg != null && dto.DefaultOrg == true) await MakePreviousDefaultsFalse(organization.Id);

        await _context.SaveChangesAsync();

        // log update Organization event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organization.Id,
            null,
            new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "organization",
                EntityId = organization.Id,
                EntityName = organization.Name,
                Properties = JsonSerializer.Serialize(new { organization.Name }),
            });

        return new OrganizationResponseDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            LastUpdatedAt = organization.LastUpdatedAt,
            LastUpdatedBy = organization.LastUpdatedBy,
            IsArchived = organization.IsArchived,
            DefaultOrg = organization.DefaultOrg,
            Banner = organization.Banner,
            RequireSensitivityLabel = organization.RequireSensitivityLabel,
            Theme = organization.Theme,
            CreateContainerPerProject = organization.CreateContainerPerProject,
            DisableFileTransfer = organization.DisableFileTransfer
        };
    }

    /// <summary>
    ///     Archive a specific organization by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to archive</param>
    /// <returns>Boolean true on successful archive</returns>
    /// <exception cref="KeyNotFoundException">Returned if organization not found</exception>
    public async Task<bool> ArchiveOrganization(long currentUserId, long organizationId)
    {
        var organization = await _context.Organizations.FindAsync(organizationId);

        if (organization == null || organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} not found");

        // TODO: determine if this needs to be a cascade archive instead
        organization.IsArchived = true;
        organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        organization.LastUpdatedBy = currentUserId;
        _context.Organizations.Update(organization);
        await _context.SaveChangesAsync();

        // Log organization archive event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            null,
            new CreateEventRequestDto
            {
                Operation = "archive",
                EntityType = "organization",
                EntityId = organization.Id,
                EntityName = organization.Name,
                Properties = JsonSerializer.Serialize(new { organization.Name }),
            });

        return true;
    }

    /// <summary>
    ///     Unarchive a specific organization by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to unarchive</param>
    /// <returns>Boolean true on successful unarchive</returns>
    /// <exception cref="KeyNotFoundException">Returned if organization not found</exception>
    public async Task<bool> UnarchiveOrganization(long currentUserId, long organizationId)
    {
        var organization = await _context.Organizations.FindAsync(organizationId);

        if (organization == null || !organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} not found");

        // TODO: determine if this needs to be a cascade unarchive instead
        organization.IsArchived = false;
        organization.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        organization.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log organization archive event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organization.Id,
            null,
            new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "organization",
                EntityId = organization.Id,
                EntityName = organization.Name,
                Properties = JsonSerializer.Serialize(new { organization.Name }),
            });

        return true;
    }

    /// <summary>
    ///     Delete a specific organization by ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to delete</param>
    /// <returns>Boolean true on successful deletion</returns>
    /// <exception cref="KeyNotFoundException">Returned if organization not found</exception>
    public async Task<bool> DeleteOrganization(long organizationId)
    {
        var organization = await _context.Organizations.FindAsync(organizationId);

        if (organization == null || organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} not found");

        _context.Organizations.Remove(organization);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Add a user to an Organization
    /// </summary>
    /// <param name="organizationId">The ID of the org to add the user to</param>
    /// <param name="userId">The ID of the user to add</param>
    /// <param name="isAdmin">Whether user should be org admin or not</param>
    /// <param name="allowServiceAccounts">(Internal Use only) Allows service users to be added to an organization</param>
    /// <returns>False if user is already in org, True upon successfully adding user</returns>
    /// <exception cref="KeyNotFoundException">Returned if user or org does not exist</exception>
    public async Task<bool> AddUserToOrganization(long organizationId, long userId, bool isAdmin = false, bool allowServiceAccounts = false)
    {
        // check if the user is already in the organization
        var existingOrgUser = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == userId);
        if (existingOrgUser != null)
            return false; // org user already exists

        // TODO: determine if user account discovery/creation is required
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.IsArchived)
            throw new KeyNotFoundException($"User with id {userId} not found");

        // service users are restricted to project scope. Cannot be added only to an org
        if (user.AccountType == AccountType.Service && !allowServiceAccounts)
            throw new InvalidOperationException("Service accounts must be added directly to a project");

        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (organization == null || organization.IsArchived)
            throw new KeyNotFoundException($"Organization with id {organizationId} not found");

        // add user to org and assign admin privileges
        var orgUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            IsOrgAdmin = isAdmin
        };

        _context.OrganizationUsers.Add(orgUser);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Removes a logo file and updates the active logo metadata.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs.</param>
    /// <returns>True if the file is successfully removed, false otherwise.</returns>
    public async Task<bool> RemoveLogoFileAsync(long organizationId)
    {
        var realObjectStorageId = await _objectStorageBusiness.GetDefaultObjectStorage(organizationId, null);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId.Id);

        if (objectStorage.Config.MountPath != null)
        {
            var logosFolderPath = Path.Combine(
                objectStorage.Config.MountPath,
                $"org_{organizationId}",
                "logos");

            if (!Directory.Exists(logosFolderPath))
            {
                throw new DirectoryNotFoundException($"Logos folder not found for organization {organizationId}");
            }

            var metadataFilePath = Path.Combine(logosFolderPath, "active_logo.txt");
            if (!File.Exists(metadataFilePath))
            {
                return false;
            }

            var activeLogoFileName = await File.ReadAllTextAsync(metadataFilePath);
            activeLogoFileName = activeLogoFileName?.Trim();

            if (string.IsNullOrEmpty(activeLogoFileName))
            {
                return false;
            }

            var activeLogoFilePath = Path.Combine(logosFolderPath, activeLogoFileName);

            if (!File.Exists(activeLogoFilePath))
            {
                return false;
            }

            File.Delete(activeLogoFilePath);

            var remainingFiles = Directory.GetFiles(logosFolderPath).OrderByDescending(File.GetLastWriteTime).ToList();

            if (remainingFiles.Count != 0)
            {
                var newActiveLogoFile = Path.GetFileName(remainingFiles.First());
                File.WriteAllText(metadataFilePath, newActiveLogoFile);
            }
            else
            {
                if (File.Exists(metadataFilePath))
                {
                    File.Delete(metadataFilePath);
                }
            }

            return true;
        }
        else
        {
            var azureConfig = objectStorage.Config.AzureObjectConfig;

            if (azureConfig == null)
                throw new ArgumentException("Azure Object Storage configuration is missing.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is null or empty.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
                throw new ArgumentException("Azure container name is null or empty.");

            var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

            var logosFolderPath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/logos"
                : $"{baseFilePath.TrimEnd('/')}/logos";

            var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);

            var metadataBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/active_logo.txt");

            if (!await metadataBlobClient.ExistsAsync())
            {
                return false;
            }

            var downloadResponse = await metadataBlobClient.DownloadContentAsync();
            var activeLogoFileName = downloadResponse.Value.Content.ToString().Trim();

            if (string.IsNullOrEmpty(activeLogoFileName))
            {
                return false;
            }

            var activeLogoBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/{activeLogoFileName}");

            if (!await activeLogoBlobClient.ExistsAsync())
            {
                return false;
            }

            await activeLogoBlobClient.DeleteAsync();

            await metadataBlobClient.UploadAsync(
                new MemoryStream([]),
                overwrite: true);

            return true;
        }
    }


    /// <summary>
    ///     Uploads a Organization Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="logoFile">The file to upload</param>
    /// <returns>The full path of the uploaded logo file</returns>
    public async Task<string> UploadOrganizationLogo(long organizationId, IFormFile logoFile)
    {
        // Validate the provided file
        if (logoFile == null || logoFile.Length == 0)
            throw new ArgumentException("Logo file is required and cannot be empty.");

        var allowedExtensions = new HashSet<string> { "png", "jpeg", "jpg", "webp", "gif", "svg" };
        var fileExtension = Path.GetExtension(logoFile.FileName).TrimStart('.').ToLower();

        if (!allowedExtensions.Contains(fileExtension))
            throw new ArgumentException($"Invalid file type. Allowed formats are: {string.Join(", ", allowedExtensions)}");

        if (!logoFile.ContentType.StartsWith("image/"))
            throw new ArgumentException("Invalid file type. Please upload a valid image.");

        const long maxFileSize = 5 * 1024 * 1024;
        if (logoFile.Length > maxFileSize)
            throw new ArgumentException("File size exceeds the 5MB limit.");

        var realObjectStorageId = await _objectStorageBusiness.GetDefaultObjectStorage(organizationId, null);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId.Id);

        if (objectStorage.Config.AzureObjectConfig != null)
        {
            var azureConfig = objectStorage.Config.AzureObjectConfig;

            if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is null or empty.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
                throw new ArgumentException("Azure container name is null or empty.");

            var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

            if (!SanitizeFilePath.IsValidFilePath(baseFilePath))
                throw new ArgumentException("Invalid Azure file path. Allowed characters are letters (a-z, A-Z), numbers (0-9), and '/'.");

            var newLogoFileId2 = $"logo_{Guid.NewGuid()}";
            var fileName2 = $"{newLogoFileId2}.{fileExtension}";

            var logosFolderPath2 = string.IsNullOrEmpty(baseFilePath)
            ? $"organization_{organizationId}/logos"
            : $"{baseFilePath.TrimEnd('/')}/logos";

            var filePath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/logos/{fileName2}"
                : $"{baseFilePath.TrimEnd('/')}/logos/{fileName2}";

            var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(filePath);

            await using (var stream = logoFile.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            var metadataBlobClient = containerClient.GetBlobClient($"{logosFolderPath2}/active_logo.txt");
            var activeLogoBytes = System.Text.Encoding.UTF8.GetBytes(fileName2);

            using (var ms = new MemoryStream(activeLogoBytes))
            {
                await metadataBlobClient.UploadAsync(ms, overwrite: true);
            }

            Console.WriteLine("uri clint: " + blobClient.Uri.ToString());

            return blobClient.Uri.ToString();
        }

        if (objectStorage.Config.MountPath == null)
            throw new Exception("File system mount path not set in object storage.");

        var logosFolderPath = Path.Combine(
            objectStorage.Config.MountPath,
            $"org_{organizationId}",
            "logos");

        Directory.CreateDirectory(logosFolderPath);

        var existingFiles = Directory.GetFiles(logosFolderPath)
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();

        string mostRecentFileId = "logo_0";

        if (existingFiles.Count > 0)
        {
            var mostRecentFileName = Path.GetFileNameWithoutExtension(existingFiles.First());

            var parts = mostRecentFileName.Split('_');
            if (parts.Length == 2 && parts[0] == "logo" && int.TryParse(parts[1], out int num))
            {
                mostRecentFileId = mostRecentFileName;
            }
            else
            {
                mostRecentFileId = "logo_0";
            }
        }

        int baseNumber = 0;
        var idParts = mostRecentFileId.Split('_');
        if (idParts.Length == 2 && int.TryParse(idParts[1], out int parsedNumber))
        {
            baseNumber = parsedNumber;
        }

        var newLogoFileId = $"logo_{baseNumber + 1}";
        var fileName = $"{newLogoFileId}.{fileExtension}";
        var logoFilePath = Path.Combine(logosFolderPath, fileName);

        await using (var stream = new FileStream(logoFilePath, FileMode.Create))
        {
            await logoFile.CopyToAsync(stream);
        }

        var metadataFilePath = Path.Combine(logosFolderPath, "active_logo.txt");
        File.WriteAllText(metadataFilePath, fileName);

        return logoFilePath;
    }

    /// <summary>
    ///     Get a Organization Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <returns>Record Id of Logo</returns>
    public async Task<(Stream Stream, string FullPath)?> GetOrganizationLogoStreamAsync(long organizationId)
    {
        var realObjectStorageId = await _objectStorageBusiness.GetDefaultObjectStorage(organizationId, null);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId.Id);

        if (objectStorage.Config.MountPath != null)
        {
            var logosFolderPath = Path.Combine(
                objectStorage.Config.MountPath,
                $"org_{organizationId}",
                "logos");

            if (!Directory.Exists(logosFolderPath))
            {
                return null;
            }

            var metadataFilePath = Path.Combine(logosFolderPath, "active_logo.txt");
            if (File.Exists(metadataFilePath))
            {
                var activeLogoFileName = await File.ReadAllTextAsync(metadataFilePath);
                activeLogoFileName = activeLogoFileName?.Trim();

                if (!string.IsNullOrEmpty(activeLogoFileName))
                {
                    var activeLogoFilePath = Path.Combine(logosFolderPath, activeLogoFileName);
                    if (File.Exists(activeLogoFilePath))
                    {
                        var activeFileStream = new FileStream(activeLogoFilePath, FileMode.Open, FileAccess.Read);
                        return (activeFileStream, activeLogoFilePath);
                    }
                }
            }

            var files = Directory.GetFiles(logosFolderPath).OrderByDescending(File.GetLastWriteTime).ToList();
            if (files.Count == 0)
            {
                return null;
            }

            var mostRecentFile = files.First();
            var fileStream = new FileStream(mostRecentFile, FileMode.Open, FileAccess.Read);

            return (fileStream, mostRecentFile);
        }
        else
        {
            var azureConfig = objectStorage.Config.AzureObjectConfig;

            if (azureConfig == null)
                throw new Exception("Azure Object Storage configuration is missing.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is null or empty.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
                throw new ArgumentException("Azure container name is null or empty.");

            var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

            var logosFolderPath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/logos"
                : $"{baseFilePath.TrimEnd('/')}/logos";

            var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);

            var metadataBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/active_logo.txt");

            if (!await metadataBlobClient.ExistsAsync())
            {
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: logosFolderPath))
                {
                    if (!blobItem.Name.EndsWith("active_logo.txt"))
                    {
                        var logoBlobClient = containerClient.GetBlobClient(blobItem.Name);
                        var stream = await logoBlobClient.OpenReadAsync();
                        return (stream, logoBlobClient.Uri.ToString());
                    }
                }

                return null;
            }

            var downloadResponse = await metadataBlobClient.DownloadContentAsync();
            var activeLogoFileName = downloadResponse.Value.Content.ToString().Trim();

            if (string.IsNullOrEmpty(activeLogoFileName))
            {
                return null;
            }

            var activeLogoBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/{activeLogoFileName}");

            if (!await activeLogoBlobClient.ExistsAsync())
            {
                return null;
            }

            var activeLogoStream = await activeLogoBlobClient.OpenReadAsync();

            return (activeLogoStream, activeLogoBlobClient.Uri.ToString());
        }
    }

    /// <summary>
    ///     Update a user's permissions within an Organization
    /// </summary>
    /// <param name="organizationId">ID of org in which to adjust user perms</param>
    /// <param name="userId">ID of user to adjust</param>
    /// <param name="isAdmin">Admin status to set user to within the org</param>
    /// <returns>True if permissions were updated successfully</returns>
    /// <exception cref="KeyNotFoundException">Returned if user doesn't already exist in org</exception>
    public async Task<bool> SetOrganizationAdminStatus(long organizationId, long userId, bool isAdmin = false)
    {
        // check if the user exists in the organization
        var existingOrgUser = await _context.OrganizationUsers
            .Include(ou => ou.User)
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == userId);

        if (existingOrgUser == null)
            throw new KeyNotFoundException($"User with id {userId} not found in Org with id {organizationId}");

        if (existingOrgUser.User.AccountType == AccountType.Service)
            throw new InvalidOperationException("Only standard user accounts can be granted org admin status.");

        // set is admin and save to DB
        existingOrgUser.IsOrgAdmin = isAdmin;
        _context.OrganizationUsers.Update(existingOrgUser);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Remove a user from an organization
    /// </summary>
    /// <param name="organizationId">ID of organization</param>
    /// <param name="userId">ID of user</param>
    /// <returns>True if user successfully removed</returns>
    /// <exception cref="KeyNotFoundException">Returned if user doesn't exist in organization</exception>
    public async Task<bool> RemoveUserFromOrganization(long organizationId, long userId)
    {
        // check if the user exists in the organization
        var existingOrgUser = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == userId);

        if (existingOrgUser == null)
            throw new KeyNotFoundException($"User with id {userId} not found in Org with id {organizationId}");

        _context.OrganizationUsers.Remove(existingOrgUser);
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task MakePreviousDefaultsFalse(long defaultOrganizationId)
    {
        var previousDefaults =
            await _context.Organizations
                .Where(o => o.DefaultOrg && o.Id != defaultOrganizationId)
                .ToListAsync();

        if (previousDefaults.Count > 0)
            foreach (var defaultOrg in previousDefaults)
            {
                defaultOrg.DefaultOrg = false;
                _context.Organizations.Update(defaultOrg);
            }

        await _context.SaveChangesAsync();
    }

    private async Task SetOrganizationDefaults(long currentUserId, long organizationId)
    {
        // ===============================
        // CREATE DEFAULT OBJECT STORAGE
        // ===============================
        Env.Load("../.env");
        var defaultObjectStorageMethod = Environment.GetEnvironmentVariable("FILE_STORAGE_METHOD");
        var configDto = new ObjectStorageConfigDto();
        if (defaultObjectStorageMethod == "filesystem")
        {
            var mountPath =
                Environment.GetEnvironmentVariable("STORAGE_DIRECTORY");

            if (string.IsNullOrWhiteSpace(mountPath))
                throw new ArgumentException($"STORAGE_DIRECTORY is null or white space, please check your environment variables.");

            configDto.MountPath = mountPath;
        }
        else if (defaultObjectStorageMethod == "azure_object")
        {
            var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_OBJECT_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(azureConnectionString))
                throw new ArgumentException("AZURE_OBJECT_CONNECTION_STRING is null or white space, please check your environment variables.");

            var azureContainerName = Environment.GetEnvironmentVariable("AZURE_CONTAINER_NAME");
            if (string.IsNullOrWhiteSpace(azureContainerName))
                throw new ArgumentException("AZURE_CONTAINER_NAME is null or white space, please check your environment variables.");

            configDto.AzureObjectConfig = new AzureObjectConfigDto()
            {
                AzureConnectionString = azureConnectionString,
                AzureContainerName = azureContainerName
            };
        }
        else if (defaultObjectStorageMethod == "aws_s3")
        {
            var awsConnectionString =
                Environment.GetEnvironmentVariable("AWS_S3_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(awsConnectionString))
                throw new ArgumentException("AWS_S3_CONNECTION_STRING is null or white space, please check your environment variables.");

            configDto.AwsConnectionString = awsConnectionString;
        }
        else
        {
            throw new NullReferenceException(
                "Unknown object storage method, make sure your environment variables are correctly set");
        }

        var objectStorageRequestDto = new CreateObjectStorageRequestDto
        {
            Name = "Instance Default",
            Config = configDto,
            Default = true
        };
        await _objectStorageBusiness.CreateObjectStorage(
            currentUserId, organizationId, null, objectStorageRequestDto);

        // ===============================
        // CREATE DEFAULT ROLES
        // ===============================
        var defaultRoles = new List<CreateRoleRequestDto>
        {
            new() { Name = "User", Description = "User role with limited permissions" }
        };
        var roles = await _roleBusiness.BulkCreateRoles(currentUserId, organizationId, null, defaultRoles);
        var userRoleId = roles.Single(r => r.Name == "User").Id;

        // set role permissions for user
        await _roleBusiness.SetPermissionsByPattern(userRoleId, DefaultRolePermissions.User.AllowedPermissions,
            organizationId, null);
    }

    private async Task<long> ResolveObjectStorageId(long organizationId, long projectId, long? objectStorageId)
    {
        if (objectStorageId.HasValue)
        {
            // object storage could be org-level so just return object storage, don't check for project existence
            return objectStorageId.Value;
        }

        var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(organizationId, projectId)
            ?? throw new KeyNotFoundException("Default object storage not found");
        return defaultObjectStorage.Id;
    }
}