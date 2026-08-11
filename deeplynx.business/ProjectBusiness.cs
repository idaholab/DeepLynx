using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.Configuration;
using DotNetEnv;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace deeplynx.business;

public class ProjectBusiness : IProjectBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly IEventBusiness _eventBusiness;
    private readonly IFileBusiness _fileAzureBusiness;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<ProjectBusiness> _logger;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly INotificationBusiness _notificationBusiness;
    private readonly IOrganizationBusiness _organizationBusiness;
    private readonly IRoleBusiness _roleBusiness;
    private readonly TimeSpan cacheTTL = TimeSpan.FromHours(1);
    private readonly string ProjectsCacheKey = "projects";

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the project operations.</param>
    /// <param name="classBusiness">Used to create default classes automatically on project creation.</param>
    /// <param name="roleBusiness">Used to create default roles automatically on project creation.</param>
    /// <param name="dataSourceBusiness">Used to create a default datasource on project creation.</param>
    /// <param name="notificationBusiness">The business logic interface for handling notification operations.</param>
    /// <param name="organizationBusiness">The business logic interface for handling organization operations.</param>
    /// <param name="eventBusiness">Used for logging events during create and update Operations.</param>
    /// <param name="logger">Used for uniformity in logging</param>
    /// <param name="objectStorageBusiness">Used to create a default object storage upon project creation.</param>
    /// <param name="fileAzureBusiness">Used to manage Azure operations.</param>
    public ProjectBusiness(
        DeeplynxContext context, ILogger<ProjectBusiness> logger,
        IClassBusiness classBusiness, IRoleBusiness roleBusiness, IDataSourceBusiness dataSourceBusiness,
        IObjectStorageBusiness objectStorageBusiness, IEventBusiness eventBusiness,
        IOrganizationBusiness organizationBusiness, INotificationBusiness notificationBusiness,
        IFileBusiness fileAzureBusiness)
    {
        _context = context;
        _logger = logger;
        _classBusiness = classBusiness;
        _roleBusiness = roleBusiness;
        _dataSourceBusiness = dataSourceBusiness;
        _notificationBusiness = notificationBusiness;
        _objectStorageBusiness = objectStorageBusiness;
        _eventBusiness = eventBusiness;
        _organizationBusiness = organizationBusiness;
        _fileAzureBusiness = fileAzureBusiness;
    }

    /// <summary>
    ///     Retrieves all projects
    /// </summary>
    /// <param name="userId">ID of user querying projects</param>
    /// <param name="organizationId">(Required)Organization ID within which to constrain returned projects</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result</param>
    /// <returns>A list of projects</returns>
    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjects(
        long userId,
        long organizationId,
        bool hideArchived = true)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new ArgumentException($"User with id {userId} not found.");

        var isOrgAdmin = await _context.OrganizationUsers
            .AnyAsync(ou => ou.UserId == userId
                            && ou.OrganizationId == organizationId
                            && ou.IsOrgAdmin);

        var projectQuery = _context.Projects
            .Where(p => p.OrganizationId == organizationId
                        && (!hideArchived || !p.IsArchived));

        if (!user.IsSysAdmin && !isOrgAdmin)
            projectQuery = projectQuery.Where(p =>
                p.ProjectMembers.Any(pm =>
                    pm.UserId == userId ||
                    (pm.GroupId.HasValue && pm.Group != null && pm.Group.Users.Any(u => u.Id == userId))
                )
            );

        return await projectQuery.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Abbreviation = p.Abbreviation,
            LastUpdatedAt = p.LastUpdatedAt,
            LastUpdatedBy = p.LastUpdatedBy,
            IsArchived = p.IsArchived,
            OrganizationId = p.OrganizationId,
            Banner = p.Banner
        })
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a new project based on the data transfer object supplied.
    /// </summary>
    /// <param name="userId">Name of user creating the project</param>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="dto">A data transfer object with details on the new project to be created.</param>
    /// <returns>The new project which was just created.</returns>
    public async Task<ProjectResponseDto> CreateProject(
        long userId, long organizationId, CreateProjectRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
            Abbreviation = dto.Abbreviation,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = userId,
            Banner = dto.Banner,
            RequireSensitivityLabel = dto.RequireSensitivityLabel ?? false
        };

        _context.Projects.Add(project);

        await _context.SaveChangesAsync();
        var projectId = project.Id;

        var projectResponseDto = new ProjectResponseDto
        {
            Id = projectId,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            LastUpdatedBy = project.LastUpdatedBy,
            LastUpdatedAt = project.LastUpdatedAt,
            OrganizationId = project.OrganizationId,
            Banner = project.Banner,
            RequireSensitivityLabel = dto.RequireSensitivityLabel
        };

        var organization = await _context.Organizations
            .Where(org => org.Id == organizationId)
            .Select(org => new { org.Id, org.CreateContainerPerProject })
            .FirstOrDefaultAsync() ?? throw new Exception("Organization not found.");

        if (organization.CreateContainerPerProject)
        {
            var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, null);
            var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

            if (objectStorage.Config != null && objectStorage.Config.AzureObjectConfig != null)
            {
                string containerName = ContainerName.UniqueContainerNameFromString(dto.Name);

                var newObjectStorageDto = await _fileAzureBusiness.CreateContainer(
                    organizationId: organizationId,
                    containerName: containerName,
                    connectionString: null,
                    isDefault: true,
                    existingContainer: true);

                var objectStorageResponse = await _objectStorageBusiness.CreateObjectStorage(
                    currentUserId: userId,
                    organizationId: organizationId,
                    projectId: projectId,
                    dto: newObjectStorageDto);

                projectResponseDto.AssociatedObjectStorage = new ObjectStorageResponseDto
                {
                    Id = objectStorageResponse.Id,
                    Name = objectStorageResponse.Name,
                    Type = objectStorageResponse.Type,
                    ProjectId = objectStorageResponse.ProjectId,
                    OrganizationId = objectStorageResponse.OrganizationId
                };
            }
        }

        // Update the Project Cache List
        var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

        if (cachedProjectList == null) cachedProjectList = new List<ProjectResponseDto>();

        // add the new project to the project list and set the cache
        cachedProjectList.Add(projectResponseDto);
        await CacheService.Instance.SetAsync(ProjectsCacheKey, cachedProjectList, cacheTTL);

        // If project cache count differs from the database refresh it to match the database and return
        if (cachedProjectList.Count != _context.Projects.Count()) await RefreshProjectsCache();

        // Log create Project event
        var eventLog = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "project",
            EntityId = projectId,
            EntityName = project.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { project.Name })
        };

        await _eventBusiness.CreateEvent(userId, organizationId, projectId, eventLog);

        await SetProjectDefaults(userId, organizationId, projectId);

        return projectResponseDto;
    }


    /// <summary>
    ///     Uploads a Project Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to which the file belongs</param>
    /// <param name="logoFile">The file to upload</param>
    /// <returns>The full path of the uploaded logo file</returns>
    public async Task<string> UploadProjectLogo(
        long organizationId,
        long projectId,
        long? objectStorageId,
        IFormFile logoFile)
    {
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

        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId) ?? throw new Exception("Object storage not found or failed to decrypt.");
        if (objectStorage.Config == null)
            throw new Exception("Object storage config is null.");

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

            var newLogoFileId = $"logo_{Guid.NewGuid()}";
            var fileName = $"{newLogoFileId}.{fileExtension}";

            var logosFolderPath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/project_{projectId}/logos"
                : $"{baseFilePath.TrimEnd('/')}/project_{projectId}/logos";

            var filePath = $"{logosFolderPath}/{fileName}";

            var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(filePath);

            await using (var stream = logoFile.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            var metadataBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/active_logo.txt");
            var activeLogoBytes = System.Text.Encoding.UTF8.GetBytes(fileName);

            using (var ms = new MemoryStream(activeLogoBytes))
            {
                await metadataBlobClient.UploadAsync(ms, overwrite: true);
            }

            return blobClient.Uri.ToString();
        }

        if (string.IsNullOrEmpty(objectStorage.Config.MountPath))
            throw new Exception("File system mount path not set in object storage.");

        var logosFolderPathFs = Path.Combine(
            objectStorage.Config.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            "logos");

        Directory.CreateDirectory(logosFolderPathFs);

        var existingFiles = Directory.GetFiles(logosFolderPathFs)
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

        var newLogoFileIdFs = $"logo_{baseNumber + 1}";
        var fileNameFs = $"{newLogoFileIdFs}.{fileExtension}";
        var logoFilePath = Path.Combine(logosFolderPathFs, fileNameFs);

        await using (var stream = new FileStream(logoFilePath, FileMode.Create))
        {
            await logoFile.CopyToAsync(stream);
        }

        var metadataFilePath = Path.Combine(logosFolderPathFs, "active_logo.txt");
        File.WriteAllText(metadataFilePath, fileNameFs);

        return logoFilePath;
    }

    /// <summary>
    ///     Removes a logo file and updates the active logo metadata.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs.</param>
    /// <param name="projectId">The ID of the project to which the logo belongs.</param>
    /// <param name="objectStorageId">The ID of the object storage to which the logo belongs.</param>
    /// <returns>True if the file is successfully removed, false otherwise.</returns>
    public async Task<bool> RemoveLogoFileAsync(
        long organizationId,
        long projectId,
        long? objectStorageId)
    {
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

        if (objectStorage.Config == null)
            throw new Exception("Object storage config is null.");

        if (objectStorage.Config.AzureObjectConfig != null)
        {
            var azureConfig = objectStorage.Config.AzureObjectConfig;

            if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is null or empty.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
                throw new ArgumentException("Azure container name is null or empty.");

            var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

            var logosFolderPath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/project_{projectId}/logos"
                : $"{baseFilePath.TrimEnd('/')}/project_{projectId}/logos";

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

        if (string.IsNullOrEmpty(objectStorage.Config.MountPath))
            throw new Exception("File system mount path not set in object storage.");

        var logosFolderPathFs = Path.Combine(
            objectStorage.Config.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            "logos");

        if (!Directory.Exists(logosFolderPathFs))
            throw new DirectoryNotFoundException($"Logos folder not found for project {projectId}");

        var metadataFilePath = Path.Combine(logosFolderPathFs, "active_logo.txt");

        if (!File.Exists(metadataFilePath))
            return false;

        var activeLogoFileNameFs = await File.ReadAllTextAsync(metadataFilePath);
        activeLogoFileNameFs = activeLogoFileNameFs?.Trim();

        if (string.IsNullOrEmpty(activeLogoFileNameFs))
            return false;

        var activeLogoFilePath = Path.Combine(logosFolderPathFs, activeLogoFileNameFs);

        if (!File.Exists(activeLogoFilePath))
            return false;

        File.Delete(activeLogoFilePath);

        var remainingFiles = Directory.GetFiles(logosFolderPathFs).OrderByDescending(File.GetLastWriteTime).ToList();

        if (remainingFiles.Count != 0)
        {
            var newActiveLogoFile = Path.GetFileName(remainingFiles.First());
            File.WriteAllText(metadataFilePath, newActiveLogoFile);
        }
        else
        {
            File.WriteAllText(metadataFilePath, string.Empty);
        }

        return true;
    }


    /// <summary>
    ///     Get a Project Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to which the file belongs</param>
    /// <returns>Record Id of Logo</returns>
    public async Task<(Stream Stream, string FullPath)?> GetProjectLogoStreamAsync(
        long organizationId,
        long projectId,
        long? objectStorageId)
    {
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

        if (objectStorage.Config == null)
            throw new Exception("Object storage config is null.");

        if (objectStorage.Config.AzureObjectConfig != null)
        {
            var azureConfig = objectStorage.Config.AzureObjectConfig;

            if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
                throw new ArgumentException("Azure connection string is null or empty.");

            if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
                throw new ArgumentException("Azure container name is null or empty.");

            var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

            var logosFolderPath = string.IsNullOrEmpty(baseFilePath)
                ? $"organization_{organizationId}/project_{projectId}/logos"
                : $"{baseFilePath.TrimEnd('/')}/project_{projectId}/logos";

            var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);

            var metadataBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/active_logo.txt");

            if (await metadataBlobClient.ExistsAsync())
            {
                var downloadResponse = await metadataBlobClient.DownloadContentAsync();
                var activeLogoFileName = downloadResponse.Value.Content.ToString().Trim();

                if (!string.IsNullOrEmpty(activeLogoFileName))
                {
                    var activeLogoBlobClient = containerClient.GetBlobClient($"{logosFolderPath}/{activeLogoFileName}");

                    if (await activeLogoBlobClient.ExistsAsync())
                    {
                        var stream = await activeLogoBlobClient.OpenReadAsync();
                        return (stream, activeLogoBlobClient.Uri.ToString());
                    }
                }
            }

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: logosFolderPath))
            {
                if (blobItem.Name.EndsWith("active_logo.txt"))
                    continue;

                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var stream = await blobClient.OpenReadAsync();
                return (stream, blobClient.Uri.ToString());
            }

            return null;
        }

        if (string.IsNullOrEmpty(objectStorage.Config.MountPath))
            throw new Exception("File system mount path not set in object storage.");

        var logosFolderPathFs = Path.Combine(
            objectStorage.Config.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            "logos");

        if (!Directory.Exists(logosFolderPathFs))
            return null;

        var metadataFilePath = Path.Combine(logosFolderPathFs, "active_logo.txt");
        if (File.Exists(metadataFilePath))
        {
            var activeLogoFileName = await File.ReadAllTextAsync(metadataFilePath);
            activeLogoFileName = activeLogoFileName?.Trim();

            if (!string.IsNullOrEmpty(activeLogoFileName))
            {
                var activeLogoFilePath = Path.Combine(logosFolderPathFs, activeLogoFileName);
                if (File.Exists(activeLogoFilePath))
                {
                    var activeFileStream = new FileStream(activeLogoFilePath, FileMode.Open, FileAccess.Read);
                    return (activeFileStream, activeLogoFilePath);
                }
            }
        }

        var files = Directory.GetFiles(logosFolderPathFs).OrderByDescending(File.GetLastWriteTime).ToList();
        if (files.Count == 0)
            return null;

        var mostRecentFile = files.First();
        var fileStream = new FileStream(mostRecentFile, FileMode.Open, FileAccess.Read);

        return (fileStream, mostRecentFile);
    }


    /// <summary>
    ///     Retrieves a specific project by ID
    /// </summary>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">The ID by which to retrieve the project</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result</param>
    /// <returns>The given project to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if project not found or is archived</exception>
    public async Task<ProjectResponseDto> GetProject(long organizationId, long projectId, bool hideArchived = true)
    {
        var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

        // If no projects are cached update the Cache
        if (cachedProjectList == null || !cachedProjectList.Any())
        {
            await RefreshProjectsCache();
            cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

            if (cachedProjectList == null) cachedProjectList = new List<ProjectResponseDto>();
        }

        var cachedProject =
            cachedProjectList.FirstOrDefault(p => p.Id == projectId && p.OrganizationId == organizationId);

        if (hideArchived && cachedProject != null)
            if (cachedProject.IsArchived)
                cachedProject = null;

        if (cachedProject == null) throw new KeyNotFoundException($"Project with id {projectId} not found");

        return cachedProject;
    }

    /// <summary>
    ///     Updates an existing project by ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to update</param>
    /// <param name="dto">A data transfer object with details on the project to be updated.</param>
    /// <returns>The project which was just updated.</returns>
    /// <exception cref="KeyNotFoundException">Returned if the project was not found.</exception>
    public async Task<ProjectResponseDto> UpdateProject(long currentUserId, long organizationId, long projectId,
        UpdateProjectRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var project = await _context.Projects
            .Where(p => p.Id == projectId
                        && p.OrganizationId == organizationId
                        && !p.IsArchived)
            .FirstOrDefaultAsync();

        if (project == null)
            throw new KeyNotFoundException(
                $"Project with id {projectId} not found or does not belong to the specified organization context");

        // Validate that if the RequireSensitivityLabel is enabled all existing records have labels
        if (!project.RequireSensitivityLabel && dto.RequireSensitivityLabel == true)
        {
            var hasUnlabeledRecords = await _context.Records
                .Include(r => r.Labels)
                .Where(r => r.ProjectId == projectId)
                .AnyAsync(r => !r.Labels.Any());

            if (hasUnlabeledRecords)
                throw new InvalidOperationException(
                    "Cannot require sensitivity labels: project contains records without labels. " +
                    "Please label all existing records before enabling this requirement.");
        }

        if (dto.RequireSensitivityLabel != null)
            project.RequireSensitivityLabel = dto.RequireSensitivityLabel.Value;

        project.Name = dto.Name ?? project.Name;
        project.Description = dto.Description ?? project.Description;
        project.Abbreviation = dto.Abbreviation ?? project.Abbreviation;
        project.LastUpdatedBy = currentUserId;
        project.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        project.Banner = dto.Banner;
        project.FilePath = dto.FilePath;

        _context.Projects.Update(project);
        await _context.SaveChangesAsync();

        // Log update Project event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, null, new CreateEventRequestDto
        {
            Operation = "update",
            EntityType = "project",
            EntityId = project.Id,
            EntityName = project.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { project.Name })
        });

        var updatedProject = new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            IsArchived = project.IsArchived,
            LastUpdatedAt = project.LastUpdatedAt,
            LastUpdatedBy = project.LastUpdatedBy,
            OrganizationId = project.OrganizationId,
            Banner = project.Banner,
            RequireSensitivityLabel = project.RequireSensitivityLabel,
        };

        // Update the Project Cache List
        var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

        // If cache list is empty, refresh it to match the database and return
        if (cachedProjectList == null)
        {
            await RefreshProjectsCache();
            return updatedProject;
        }

        // If cache exists, update the project in the list
        var projectIndex = cachedProjectList.FindIndex(p => p.Id == updatedProject.Id);
        if (projectIndex != -1) cachedProjectList[projectIndex] = updatedProject;

        // Set the updated list back to the cache
        await CacheService.Instance.SetAsync(ProjectsCacheKey, cachedProjectList, cacheTTL);

        return updatedProject;
    }

    /// <summary>
    ///     Delete a project by id.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to delete.</param>
    /// <returns>Boolean true on successful deletion.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if project is not found.</exception>
    public async Task<bool> DeleteProject(long currentUserId, long organizationId, long projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId
                        && p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

        if (project == null)
            throw new KeyNotFoundException($"Project with id {projectId} not found.");

        var projectName = project.Name;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        // Update the Project Cache List
        var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

        // If cache list is empty, refresh it to match the database and return
        if (cachedProjectList == null)
        {
            await RefreshProjectsCache();
            return true;
        }

        var projectIndex = cachedProjectList.FindIndex(p => p.Id == projectId);
        if (projectIndex != -1) cachedProjectList.RemoveAt(projectIndex);

        await CacheService.Instance.SetAsync(ProjectsCacheKey, cachedProjectList, cacheTTL);

        return true;
    }

    /// <summary>
    ///     Archive (soft delete) a project by id. This also archives downstream dependents.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to archive.</param>
    /// <returns>Boolean true on successful archival.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if project is not found.</exception>
    /// <exception cref="DependencyDeletionException">Thrown if archival fails.</exception>
    public async Task<bool> ArchiveProject(long currentUserId, long organizationId, long projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId
                        && p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

        if (project == null || project.IsArchived)
            throw new KeyNotFoundException($"Project with id {projectId} not found or is already archived");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run archive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the archive project procedure, which archives this project
                // and all child objects with project_id as a foreign key
                var archived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.archive_project({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE, {2}::INTEGER)",
                    projectId, lastUpdatedAt, currentUserId
                );

                if (archived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to archive project {projectId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to archive project {projectId} or its downstream dependents: {exc}");
            }
        }

        // Refresh the entity from the database to get updated values
        await _context.Entry(project).ReloadAsync();

        var projectResponse = new ProjectResponseDto
        {
            Id = project.Id,
            OrganizationId = organizationId,
            Name = project.Name,
            Description = project.Description,
            Abbreviation = project.Abbreviation,
            LastUpdatedAt = project.LastUpdatedAt,
            LastUpdatedBy = project.LastUpdatedBy,
            IsArchived = project.IsArchived,
            Banner = project.Banner
        };

        // Update the Project Cache List
        var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

        // If cache list is empty, refresh it to match the database and return
        if (cachedProjectList == null)
        {
            await RefreshProjectsCache();
        }
        else
        {
            // If cache exists, update the project in the list
            var projectIndex = cachedProjectList.FindIndex(p => p.Id == projectResponse.Id);
            if (projectIndex != -1) cachedProjectList[projectIndex] = projectResponse;

            // Set the updated list back to the cache
            await CacheService.Instance.SetAsync(ProjectsCacheKey, cachedProjectList, cacheTTL);
        }

        // Log the archive event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "project",
            EntityId = project.Id,
            EntityName = project.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { project.Name })
        });

        return true;
    }

    /// <summary>
    ///     Unarchive a project by id. This also unarchives downstream dependents.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to unarchive.</param>
    /// <returns>Boolean true when successfully unarchived.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if project is not found.</exception>
    /// <exception cref="DependencyDeletionException">Thrown if unarchive action fails.</exception>
    public async Task<bool> UnarchiveProject(long currentUserId, long organizationId, long projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId
                        && p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

        if (project == null || !project.IsArchived)
            throw new KeyNotFoundException("Project not found or is not archived.");

        // set lastUpdatedAt timestamp
        var lastUpdatedAt = DateTime.UtcNow;

        // run unarchive procedure in a transaction to roll back any errors
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // run the unarchive project procedure, which unarchives this project
                // and all child objects with project_id as a foreign key
                var unarchived = await _context.Database.ExecuteSqlRawAsync(
                    "CALL deeplynx.unarchive_project({0}::INTEGER, {1}::TIMESTAMP WITHOUT TIME ZONE,  {2}::INTEGER)",
                    projectId, lastUpdatedAt, currentUserId
                );

                if (unarchived == 0) // if 0 records were updated, assume a failure
                    throw new DependencyDeletionException(
                        $"unable to unarchive project {projectId} or its downstream dependents.");

                await transaction.CommitAsync();
            }
            catch (Exception exc)
            {
                await transaction.RollbackAsync();
                throw new DependencyDeletionException(
                    $"unable to unarchive project {projectId} or its downstream dependents: {exc}");
            }

            // Refresh the entity from the database to get updated values
            await _context.Entry(project).ReloadAsync();

            var projectResponse = new ProjectResponseDto
            {
                Id = project.Id,
                OrganizationId = organizationId,
                Name = project.Name,
                Description = project.Description,
                Abbreviation = project.Abbreviation,
                LastUpdatedAt = project.LastUpdatedAt,
                LastUpdatedBy = project.LastUpdatedBy,
                IsArchived = project.IsArchived,
                Banner = project.Banner
            };

            // Update the Project Cache List
            var cachedProjectList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(ProjectsCacheKey);

            // If cache list is empty, refresh it to match the database and return
            if (cachedProjectList == null)
            {
                await RefreshProjectsCache();
            }
            else
            {
                // If cache exists, update the project in the list
                var projectIndex = cachedProjectList.FindIndex(p => p.Id == projectResponse.Id);
                if (projectIndex != -1) cachedProjectList[projectIndex] = projectResponse;

                // Set the updated list back to the cache
                await CacheService.Instance.SetAsync(ProjectsCacheKey, cachedProjectList, cacheTTL);
            }

            // Log the unarchive event
            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
            {
                Operation = "unarchive",
                EntityType = "project",
                EntityId = project.Id,
                EntityName = project.Name,
                DataSourceId = null,
                Properties = JsonSerializer.Serialize(new { project.Name })
            });

            return true;
        }
    }

    /// <summary>
    ///     Retrieves project stats
    /// </summary>
    /// <param name="organizationId">Name of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to check stats for</param>
    /// <returns>A list of project stats</returns>
    public async Task<ProjectStatResponseDto> GetProjectStats(long organizationId, long projectId)
    {
        var classes = await _context.Classes
            .Where(p => !p.IsArchived && p.ProjectId == projectId && p.OrganizationId == organizationId)
            .CountAsync();

        var records = await _context.Records
            .Where(p => !p.IsArchived && p.ProjectId == projectId && p.OrganizationId == organizationId)
            .CountAsync();

        var datasources = await _context.DataSources
            .Where(p => !p.IsArchived && p.ProjectId == projectId && p.OrganizationId == organizationId)
            .CountAsync();

        return new ProjectStatResponseDto
        {
            classes = classes,
            records = records,
            datasources = datasources
        };
    }


    /// <summary>
    ///     List the users and groups in a given project, along with their roles
    /// </summary>
    /// <param name="projectId">ID of the project to get members for</param>
    /// <returns></returns>
    public async Task<IEnumerable<ProjectMemberResponseDto>> GetProjectMembers(long projectId)
    {
        var users = _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId != null)
            .Select(pm => new ProjectMemberResponseDto
            {
                Name = pm.User.Name,
                MemberId = pm.UserId,
                Email = pm.User.Email,
                Role = pm.Role.Name,
                RoleId = pm.Role.Id,
                IsProjectAdmin = pm.IsProjectAdmin
            });

        var groups = _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.GroupId != null)
            .Select(pm => new ProjectMemberResponseDto
            {
                Name = pm.Group.Name,
                MemberId = pm.GroupId,
                Email = string.Empty,
                Role = pm.Role.Name,
                RoleId = pm.Role.Id,
                IsProjectAdmin = pm.IsProjectAdmin
            });

        return await users.Union(groups).ToListAsync();
    }

    /// <summary>
    ///     Add a user or a group to a project
    /// </summary>
    /// <param name="projectId">Project to which to add member</param>
    /// <param name="roleId">(optional) Role which member will be added under</param>
    /// <param name="userId">(optional) ID of user to be added</param>
    /// <param name="groupId">(optional) ID of group to be added</param>
    /// <param name="makeProjectAdmin">(optional) Make new member a project admin. Defaults to false</param>
    /// <param name="allowServiceAccount">Bypass for service accounts to be added (only used internally)</param>
    /// <returns>True if user or group successfully added to project</returns>
    /// <returns>False if user or group already exists in project</returns>
    /// <exception cref="ArgumentException">Returned if none or both of userID/groupID supplied</exception>
    /// <exception cref="KeyNotFoundException">Returned if user, group, role or project not found</exception>
    public async Task<bool> AddMemberToProject(long projectId, long? roleId, long? userId,
        long? groupId, bool makeProjectAdmin = false, bool allowServiceAccount = false)
    {
        // ensure one and only one of userID or groupID is supplied
        if (!userId.HasValue && !groupId.HasValue)
            throw new ArgumentException("One of User ID or Group ID must be provided");
        if (userId.HasValue && groupId.HasValue)
            throw new ArgumentException("Please provide only one of User ID or Group ID, not both");

        // check if the group or user is already in the project
        var existingProjectMember = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && (
                (userId != null && pm.UserId == userId) ||
                (groupId != null && pm.GroupId == groupId)));
        if (existingProjectMember != null)
            return false; // group or user is already present in the project

        // TODO: determine if user account discovery/creation is required
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (userId.HasValue && (user == null || user.IsArchived))
            throw new KeyNotFoundException($"User with id {userId} not found");


        // Service accounts cannot be invited to other projects. Limited to the project where they are created.
        if (userId.HasValue && user.AccountType == AccountType.Service && !allowServiceAccount)
            throw new InvalidOperationException("Service accounts cannot be added to a project directly. Use CreateAndAddServiceAccountToProject.");

        var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (groupId.HasValue && (group == null || group.IsArchived))
            throw new KeyNotFoundException($"Group with id {groupId} not found");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (roleId.HasValue && (role == null || role.IsArchived))
            throw new KeyNotFoundException($"Role with id {roleId} not found");

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null || project.IsArchived)
            throw new KeyNotFoundException($"Project with id {projectId} not found");

        // add member to project and assign role
        var projMember = new ProjectMember
        {
            ProjectId = projectId,
            RoleId = roleId,
            UserId = userId,
            GroupId = groupId,
            IsProjectAdmin = makeProjectAdmin
        };

        _context.ProjectMembers.Add(projMember);
        await _context.SaveChangesAsync();

        if (userId.HasValue && userId != UserContextStorage.UserId)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                try
                {
                    await _notificationBusiness!.SendEmail(user.Email, user.Name, false, null, projectId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send notification email to user {user.Email} after adding to project {projectId}");
                }

                return true;
            }
        }

        return true;
    }

    /// <summary>
    ///     Update a user or group's role within a project
    /// </summary>
    /// <param name="projectId">ID of project in which to adjust role</param>
    /// <param name="roleId">ID of role to adjust</param>
    /// <param name="userId">(optional) ID of user to adjust</param>
    /// <param name="groupId">(optional) ID of group to adjust</param>
    /// <param name="isProjectAdmin">(optional) project admin status to set; left unchanged when null</param>
    /// <returns>True if user or group role adjusted</returns>
    /// <exception cref="ArgumentException">Returned if none or both of userID/groupID supplied</exception>
    /// <exception cref="KeyNotFoundException">Returned if member doesn't exist in project</exception>
    public async Task<bool> UpdateProjectMemberRole(long projectId, long roleId, long? userId,
        long? groupId, bool? isProjectAdmin = null)
    {
        // ensure one and only one of userID or groupID is supplied
        if (!userId.HasValue && !groupId.HasValue)
            throw new ArgumentException("One of User ID or Group ID must be provided");
        if (userId.HasValue && groupId.HasValue)
            throw new ArgumentException("Please provide only one of User ID or Group ID, not both");

        // ensure role exists
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (role == null || role.IsArchived)
            throw new KeyNotFoundException($"Role with id {roleId} not found");

        // Find the existing project member to update
        var existingProjectMember = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId &&
                                       ((userId.HasValue && pm.UserId == userId) ||
                                        (groupId.HasValue && pm.GroupId == groupId)));
        if (existingProjectMember == null)
        {
            var memberType = userId.HasValue ? "User" : "Group";
            var memberId = userId ?? groupId;
            throw new KeyNotFoundException($"{memberType} with id {memberId} is not a member of project {projectId}");
        }

        // Update the role, and the admin flag when explicitly supplied
        existingProjectMember.RoleId = roleId;
        if (isProjectAdmin.HasValue)
            existingProjectMember.IsProjectAdmin = isProjectAdmin.Value;
        _context.ProjectMembers.Update(existingProjectMember);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Set a user or group's project admin status within a project
    /// </summary>
    /// <param name="projectId">ID of project in which to adjust admin status</param>
    /// <param name="userId">(optional) ID of user to adjust</param>
    /// <param name="groupId">(optional) ID of group to adjust</param>
    /// <param name="isAdmin">Project admin status to set the member to</param>
    /// <returns>True if the member's admin status was updated</returns>
    /// <exception cref="ArgumentException">Returned if none or both of userID/groupID supplied</exception>
    /// <exception cref="KeyNotFoundException">Returned if member doesn't exist in project</exception>
    public async Task<bool> SetProjectAdminStatus(long projectId, long? userId, long? groupId,
        bool isAdmin = false)
    {
        // ensure one and only one of userID or groupID is supplied
        if (!userId.HasValue && !groupId.HasValue)
            throw new ArgumentException("One of User ID or Group ID must be provided");
        if (userId.HasValue && groupId.HasValue)
            throw new ArgumentException("Please provide only one of User ID or Group ID, not both");

        // Find the existing project member to update
        var existingProjectMember = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId &&
                                       ((userId.HasValue && pm.UserId == userId) ||
                                        (groupId.HasValue && pm.GroupId == groupId)));
        if (existingProjectMember == null)
        {
            var memberType = userId.HasValue ? "User" : "Group";
            var memberId = userId ?? groupId;
            throw new KeyNotFoundException($"{memberType} with id {memberId} is not a member of project {projectId}");
        }

        // set admin status and save to DB
        existingProjectMember.IsProjectAdmin = isAdmin;
        _context.ProjectMembers.Update(existingProjectMember);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Remove a user or group from a project
    /// </summary>
    /// <param name="projectId">ID of the project</param>
    /// <param name="userId">(optional) ID of the user</param>
    /// <param name="groupId">(optional) ID of the group</param>
    /// <returns>True if member successfully removed</returns>
    /// <exception cref="ArgumentException">Returned if none or both of userID/groupID supplied</exception>
    /// <exception cref="KeyNotFoundException">Returned if member doesn't exist in project</exception>
    public async Task<bool> RemoveMemberFromProject(long projectId, long? userId, long? groupId)
    {
        // ensure one and only one of userID or groupID is supplied
        if (!userId.HasValue && !groupId.HasValue)
            throw new ArgumentException("One of either User ID or Group ID must be provided");
        if (userId.HasValue && groupId.HasValue)
            throw new ArgumentException("Please provide only one of User ID or Group ID, not both");

        // Service Users should not exist without scope. Must Archive or Delete
        if (userId.HasValue)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.AccountType == AccountType.Service)
                throw new InvalidOperationException("Service accounts cannot be removed from a project. Archive or delete the account instead.");
        }

        // Find the existing project member to update
        var existingProjectMember = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId &&
                                       ((userId.HasValue && pm.UserId == userId) ||
                                        (groupId.HasValue && pm.GroupId == groupId)));

        if (existingProjectMember == null)
        {
            var memberType = userId.HasValue ? "User" : "Group";
            var memberId = userId ?? groupId;
            throw new KeyNotFoundException($"{memberType} with id {memberId} is not a member of project {projectId}");
        }

        // remove project member
        _context.ProjectMembers.Remove(existingProjectMember);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Retrieves all records for multiple projects.
    /// </summary>
    /// <param name="projects">Array of project ids whose records are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>A list of records based on the applied filters.</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetMultiProjectRecords(
        long[] projects, bool hideArchived)
    {
        var recordQuery = _context.HistoricalRecords
            .Where(r => projects.Contains(r.ProjectId));

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        var records = await recordQuery
            .GroupBy(e => e.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).FirstOrDefault())
            .ToListAsync();

        return records
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Description = r.Description,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                ClassId = r.ClassId,
                ClassName = r.ClassName,
                DataSourceId = r.DataSourceId,
                ProjectId = r.ProjectId,
                LastUpdatedAt = r.LastUpdatedAt,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
                Tags = r.Tags
            });
    }

    /// <summary>
    ///     Create an Azure Container for a Project
    /// </summary>
    /// <param name="userId">ID of the user performing the operation.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs.</param>
    /// <param name="projectId">The ID of the project to create the container for.</param>
    /// <param name="containerName">The name of the container</param>
    /// <param name="existingContainer">A bool for an existing container</param>
    /// <returns>The newly created object storage</returns>
    public async Task<ObjectStorageResponseDto?> CreateProjectAzureContainer(
    long userId, long organizationId, long projectId, string? containerName, bool existingContainer = false)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

        if (project == null || project.IsArchived)
            throw new KeyNotFoundException($"Project with id {projectId} not found or is archived");

        string containerNameToUse;

        if (!string.IsNullOrWhiteSpace(containerName))
        {
            containerNameToUse = !existingContainer
                ? ContainerName.UniqueContainerNameFromString(containerName)
                : containerName;
        }
        else
        {
            containerNameToUse = ContainerName.UniqueContainerNameFromString(project.Name);
        }

        var newObjectStorageDto = await _fileAzureBusiness.CreateContainer(
            organizationId: organizationId,
            containerName: containerNameToUse,
            connectionString: null,
            isDefault: false,
            existingContainer: existingContainer);

        return await _objectStorageBusiness.CreateObjectStorage(
            currentUserId: userId,
            organizationId: organizationId,
            projectId: projectId,
            dto: newObjectStorageDto,
            createContainer: false);
    }

    // PRIVATE HELPER FUNCTIONS //
    private async Task<bool> RefreshProjectsCache()
    {
        var dbProjects = await _context.Projects.ToListAsync();
        var projectResponseDtoList = MapProjectsToResponseDto(dbProjects);
        await CacheService.Instance.SetAsync(ProjectsCacheKey, projectResponseDtoList, cacheTTL);
        return true;
    }

    private List<ProjectResponseDto> MapProjectsToResponseDto(List<Project> projects)
    {
        return projects.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Abbreviation = p.Abbreviation,
            LastUpdatedBy = p.LastUpdatedBy,
            LastUpdatedAt = p.LastUpdatedAt,
            IsArchived = p.IsArchived,
            OrganizationId = p.OrganizationId
        }).ToList();
    }

    private async Task SetProjectDefaults(long currentUserId, long organizationId, long projectId)
    {
        // ===============================
        // CREATE DEFAULT CLASSES
        // ===============================
        var defaultClasses = new List<CreateClassRequestDto>
        {
            new() { Name = "Timeseries" },
            new() { Name = "Report" },
            new() { Name = "File" }
        };
        await _classBusiness.BulkCreateClasses(
            currentUserId, organizationId, projectId, defaultClasses);

        // ===============================
        // CREATE DEFAULT DATA SOURCE
        // ===============================
        var defaultDataSource = new CreateDataSourceRequestDto
        {
            Name = "Default Data Source",
            Description = "This data source was created alongside the project for ease of use.",
            Default = true
        };
        await _dataSourceBusiness.CreateDataSource(organizationId, projectId, currentUserId, defaultDataSource);

        // ===============================
        // Add current user as admin to project
        // ===============================
        await AddMemberToProject(projectId, null, currentUserId, null, makeProjectAdmin: true);
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