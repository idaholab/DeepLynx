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
            Theme = organization.Theme
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
            Theme = "default"
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
            Theme = organization.Theme
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
            Theme = organization.Theme
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
    /// <returns>False if user is already in org, True upon successfully adding user</returns>
    /// <exception cref="KeyNotFoundException">Returned if user or org does not exist</exception>
    public async Task<bool> AddUserToOrganization(long organizationId, long userId, bool isAdmin = false)
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
        if (user.AccountType == AccountType.Service)
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
}
