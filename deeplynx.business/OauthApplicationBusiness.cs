using Microsoft.EntityFrameworkCore;
using deeplynx.models;
using deeplynx.interfaces;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace deeplynx.business;

public class OauthApplicationBusiness : IOauthApplicationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ILogger<OauthApplicationBusiness> _logger;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    /// Initializes a new instance of the <see cref="OauthApplicationBusiness"/> class. 
    /// </summary>
    /// <param name="context">The database context used for oauth application CRUD operations</param>
    /// <param name="logger">Used for logging</param>
    /// <param name="eventBusiness">Used for recording events during CRUD operations</param>
    public OauthApplicationBusiness(
        DeeplynxContext context,
        ILogger<OauthApplicationBusiness> logger,
        IEventBusiness eventBusiness
    )
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all oauth applications
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived applications from the result</param>
    /// <returns>List of applications</returns>
    public async Task<IEnumerable<OauthApplicationResponseDto>> GetAllOauthApplications(bool hideArchived = true)
    {
        var applicationQuery = _context.OauthApplications.AsQueryable();

        if (hideArchived)
        {
            applicationQuery = applicationQuery.Where(x => !x.IsArchived);
        }

        var applications = await applicationQuery.ToListAsync();

        return applications
            .Select(a => new OauthApplicationResponseDto()
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                AppOwnerEmail = a.AppOwnerEmail,
                CallbackUrl = a.CallbackUrl,
                BaseUrl = a.BaseUrl,
                ClientId = a.ClientId,
                IsArchived = a.IsArchived,
                LastUpdatedAt = a.LastUpdatedAt,
                LastUpdatedBy = a.LastUpdatedBy
            });
    }

    /// <summary>
    /// Retrieve a specific application by ID
    /// </summary>
    /// <param name="applicationId">The ID of the requested application</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived applications from the result</param>
    /// <returns>The requested application if exists</returns>
    /// <exception cref="KeyNotFoundException">Returned if app does not exist or is archived</exception>
    public async Task<OauthApplicationResponseDto> GetOauthApplication(
        long applicationId, bool hideArchived = true)
    {
        var application = await _context.OauthApplications
            .Where(a => a.Id == applicationId)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            throw new KeyNotFoundException($"Oauth application with id {applicationId} not found");
        }

        if (hideArchived && application.IsArchived)
        {
            throw new KeyNotFoundException($"Oauth application with id {applicationId} is archived");
        }

        return new OauthApplicationResponseDto
        {
            Id = application.Id,
            Name = application.Name,
            Description = application.Description,
            AppOwnerEmail = application.AppOwnerEmail,
            CallbackUrl = application.CallbackUrl,
            BaseUrl = application.BaseUrl,
            ClientId = application.ClientId,
            IsArchived = application.IsArchived,
            LastUpdatedAt = application.LastUpdatedAt,
            LastUpdatedBy = application.LastUpdatedBy
        };
    }

    /// <summary>
    /// Create a new oauth application to represent an ecosystem app that uses Nexus as a provider
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="dto">The data transfer object with details on the application to be created</param>
    /// <param name="userId">The ID of the requesting user</param>
    /// <returns>The client ID and secret of the newly created application</returns>
    public async Task<OauthApplicationSecureResponseDto> CreateOauthApplication(
        CreateOauthApplicationRequestDto dto, long userId)
    {
        ValidationHelper.ValidateModel(dto);

        // generate client ID and secret
        var clientId = GenerateClientId();
        var clientSecret = GenerateClientSecret();
        var clientSecretHash = HashSecret(clientSecret);

        var application = new OauthApplication
        {
            Name = dto.Name,
            Description = dto.Description,
            ClientId = clientId,
            ClientSecretHash = clientSecretHash,
            CallbackUrl = dto.CallbackUrl,
            BaseUrl = dto.BaseUrl,
            AppOwnerEmail = dto.AppOwnerEmail,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = userId
        };

        _context.OauthApplications.Add(application);
        await _context.SaveChangesAsync();

        return new OauthApplicationSecureResponseDto
        {
            Name = application.Name,
            ClientId = application.ClientId,
            ClientSecretRaw = clientSecret,
        };
    }

    /// <summary>
    /// Update an existing oauth application by ID
    /// </summary>
    /// <param name="applicationId">The ID of the application to be updated</param>
    /// <param name="dto">A data transfer object with details on the application to be updated</param>
    /// <param name="userId">The ID of the requesting user</param>
    /// <returns>The updated application</returns>
    /// <exception cref="KeyNotFoundException">Returned if application was not found or is archived</exception>
    public async Task<OauthApplicationResponseDto> UpdateOauthApplication(
        long applicationId, UpdateOauthApplicationRequestDto dto, long userId)
    {
        var application = await _context.OauthApplications.FindAsync(applicationId);

        if (application == null || application.IsArchived)
        {
            throw new KeyNotFoundException($"Oauth application with id {applicationId} not found");
        }

        application.Name = dto.Name ?? application.Name;
        application.Description = dto.Description ?? application.Description;
        application.CallbackUrl = dto.CallbackUrl ?? application.CallbackUrl;
        application.BaseUrl = dto.BaseUrl ?? application.BaseUrl;
        application.AppOwnerEmail = dto.AppOwnerEmail ?? application.AppOwnerEmail;
        application.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        application.LastUpdatedBy = userId;

        _context.OauthApplications.Update(application);
        await _context.SaveChangesAsync();

        return new OauthApplicationResponseDto
        {
            Id = application.Id,
            Name = application.Name,
            Description = application.Description,
            AppOwnerEmail = application.AppOwnerEmail,
            CallbackUrl = application.CallbackUrl,
            BaseUrl = application.BaseUrl,
            ClientId = application.ClientId,
            IsArchived = application.IsArchived,
            LastUpdatedAt = application.LastUpdatedAt,
            LastUpdatedBy = application.LastUpdatedBy
        };
    }

    /// <summary>
    /// Archive an application by ID
    /// </summary>
    /// <param name="applicationId">The ID of the app to be archived</param>
    /// <param name="userId">The ID of the requesting user</param>
    /// <returns>Boolean true on success</returns>
    /// <exception cref="KeyNotFoundException">Returned if application not found or already archived</exception>
    public async Task<bool> ArchiveOauthApplication(long applicationId, long userId)
    {
        var application = await _context.OauthApplications.FindAsync(applicationId);

        if (application == null || application.IsArchived)
            throw new KeyNotFoundException($"Oauth application with id {applicationId} not found");

        application.IsArchived = true;
        application.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        application.LastUpdatedBy = userId;
        _context.OauthApplications.Update(application);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Unarchive an application by ID
    /// </summary>
    /// <param name="applicationId">The ID of the app to be unarchived</param>
    /// <param name="userId">The ID of the requesting user</param>
    /// <returns>Boolean true on success</returns>
    /// <exception cref="KeyNotFoundException">Returned if application not found or not yet archived</exception>
    public async Task<bool> UnarchiveOauthApplication(long applicationId, long userId)
    {
        var application = await _context.OauthApplications.FindAsync(applicationId);

        if (application == null || !application.IsArchived)
            throw new KeyNotFoundException($"Oauth application with id {applicationId} not found or is not archived");

        application.IsArchived = false;
        application.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        application.LastUpdatedBy = userId;
        _context.OauthApplications.Update(application);
        await _context.SaveChangesAsync();

        return true;
    }
    
    /// <summary>
    /// Delete an application by ID
    /// </summary>
    /// <param name="applicationId">The ID of the app to be deleted</param>
    /// <param name="userId">The ID of the requesting user</param>
    /// <returns>Boolean true on success</returns>
    /// <exception cref="KeyNotFoundException">Returned if application not found</exception>
    public async Task<bool> DeleteOauthApplication(long applicationId, long userId)
    {
        var application = await _context.OauthApplications.FindAsync(applicationId);
        
        if (application == null || application.IsArchived)
            throw new KeyNotFoundException($"Oauth application with id {applicationId} not found");

        // grab event-relevant details before deletion
        var appId = application.Id;
        var appName = application.Name;
        var appClientId = application.ClientId;
        var lastUpdatedBy = application.LastUpdatedBy;

        _context.OauthApplications.Remove(application);
        await _context.SaveChangesAsync();

        // null is passed for organizationId and projectId, because an OAuth application is a system scoped entity and doesn't belong to either
        await _eventBusiness.CreateEvent(userId, null, null, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "oauth_application",
            EntityId = appId,
            EntityName = appName,
            Properties = JsonSerializer.Serialize(new
            {
                AppName = appName,
                AppClientId = appClientId,
                LastUpdatedBy = lastUpdatedBy
            })
        });

        return true;
    }

    /// <summary>
    /// Used to generate the client ID for the app. Client ID + Secret will be used for token creation
    /// </summary>
    /// <returns></returns>
    private string GenerateClientId()
    {
        var bytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    /// <summary>
    /// Used to create the raw client secret for the app. This will be returned to the user in its raw form only once.
    /// </summary>
    /// <returns></returns>
    private string GenerateClientSecret()
    {
        var bytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    /// <summary>
    /// Used to hash the client secret using BCrypt. This hashed version will be stored in the database.
    /// </summary>
    /// <param name="secret"></param>
    /// <returns></returns>
    private string HashSecret(string secret)
    {
        return BCrypt.Net.BCrypt.HashPassword(secret);
    }
}