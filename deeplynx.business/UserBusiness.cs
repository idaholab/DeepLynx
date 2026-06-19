using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class UserBusiness : IUserBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the user operations.</param>
    public UserBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Retrieves all users
    /// </summary>
    /// <param name="projectId">Optional ID for project</param>
    /// <param name="organizationId">Optional ID for organization</param>
    /// <returns>A list of users, optionally filtered by project or organization</returns>
    public async Task<IEnumerable<UserResponseDto>> GetAllUsers(long? projectId, long? organizationId, bool includeArchived = false)
    {
        var users = includeArchived
        ? _context.Users.AsQueryable()
        : _context.Users.Where(p => !p.IsArchived);

        if (projectId != null)
            users = users.Where(u =>
                u.ProjectMembers.Any(p => p.ProjectId == projectId && p.UserId == u.Id) ||
                u.Groups.Any(g => g.ProjectMembers.Any(pm => pm.ProjectId == projectId && pm.GroupId == g.Id))
            );

        if (organizationId != null)
            users = users.Where(u =>
                u.OrganizationUsers.Any(ou => ou.OrganizationId == organizationId && ou.UserId == u.Id) ||
                u.Groups.Any(g => g.OrganizationId == organizationId)
            );

        return users.Select(p => new UserResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Username = p.Username,
            Email = p.Email,
            IsSysAdmin = p.IsSysAdmin,
            IsOrgAdmin = organizationId != null
                ? p.OrganizationUsers.Any(ou => ou.OrganizationId == organizationId && ou.IsOrgAdmin)
                : null,
            IsArchived = p.IsArchived,
            IsActive = p.IsActive,
            LastLogin = p.LastLogin
        });
    }

    /// <summary>
    ///     Retrieves a specific user by ID
    /// </summary>
    /// <param name="userId">The ID by which to retrieve the user</param>
    /// <returns>The given user to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if user not found</exception>
    public async Task<UserResponseDto> GetUser(long userId)
    {
        var user = await _context.Users
            .Where(p => p.Id == userId && !p.IsArchived)
            .FirstOrDefaultAsync();

        if (user == null) throw new KeyNotFoundException($"User with id {userId} not found");

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin
        };
    }

    /// <summary>
    ///     Retrieves user info and domain admin info by user ID
    /// </summary>
    /// <param name="userId">The ID by which to retrieve the user</param>
    /// <param name="organizationId">Returns info on whether a user is an admin of this org if specified</param>
    /// <param name="projectId">Returns info on whether a user is an admin of this project if specified</param>
    /// <returns>The given user to return</returns>
    /// <exception cref="KeyNotFoundException">Returned if user not found</exception>
    public async Task<UserAdminInfoDto> GetUserAdminInfo(
        long userId,
        long? organizationId = null,
        long? projectId = null)
    {
        var sql = @"
        SELECT * FROM deeplynx.get_user_admin_info(
            @p_user_id, 
            @p_organization_id, 
            @p_project_id
        )";

        var user = await _context.Database
            .SqlQueryRaw<UserAdminInfoDto>(
                sql,
                new NpgsqlParameter("@p_user_id", userId),
                new NpgsqlParameter("@p_organization_id", (object?)organizationId ?? DBNull.Value),
                new NpgsqlParameter("@p_project_id", (object?)projectId ?? DBNull.Value)
            )
            .FirstOrDefaultAsync();

        if (user == null || user.IsArchived)
            throw new KeyNotFoundException($"User with id {userId} not found");

        return user;
    }

    /// <summary>
    ///     Retrieves the local dev user
    /// </summary>
    /// <returns>Information for the local dev user</returns>
    /// <exception cref="InvalidOperationException">Returned if DISABLE_BACKEND_AUTHENTICATION != true</exception>
    /// <exception cref="KeyNotFoundException">Returned if user not found</exception>
    public async Task<UserResponseDto> GetLocalDevUser()
    {
        var auth_disabled = Environment.GetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION");
        if (auth_disabled != "true")
            throw new InvalidOperationException(
                "Local Dev User cannot be used unless backend authentication is disabled");

        var user = await _context.Users
            .Where(p => p.Email == "developer@localhost")
            .FirstOrDefaultAsync();

        if (user == null) throw new KeyNotFoundException("Local Dev User not found");

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin
        };
    }

    /// <summary>
    ///     Creates a new user based on the data transfer object supplied.
    /// </summary>
    /// <param name="dto">A data transfer object with details on the new user to be created.</param>
    /// <param name="isSysAdmin">Optional Boolean Value indicating that the user is a system admin.</param>
    /// <param name="isOrgAdmin">Optional Boolean Value indicating that the user is an organization admin.</param>
    /// <param name="isProjectAdmin">Optional Boolean Value indicating that the user is a project admin.</param>
    /// <returns>The new user which was just created.</returns>
    public async Task<UserResponseDto> CreateUser(CreateUserRequestDto dto, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        // TODO: adjusting is_sys_admin is currently disabled. Enable once route permission protections are in place

        // if not specified accounts will always be human
        var isServiceAccount = dto.IsServiceAccount ?? false;
        var isAdmin = isSysAdmin || isOrgAdmin || isProjectAdmin;

        var username = dto.Username;
        var email = dto.Email;

        if (!isServiceAccount)
        {
            // require email for human accounts
            if (email is null)
                throw new ArgumentException("Email is required for human accounts");

            // verify email is unique only with human accounts
            var otherUserHasEmail = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
            if (otherUserHasEmail)
                throw new ArgumentException("User with email already exists");
        }
        else
        {
            // service accounts are an admin feature only
            if (!isAdmin)
                throw new UnauthorizedAccessException("Unauthorized: Only Admins can create service accounts");

            // Service accounts don't authenticate as people; their email and username are
            // generated, not caller-supplied. Reject contradictory input rather than silently
            // discarding it so the caller knows these fields are ignored.
            if (!string.IsNullOrWhiteSpace(dto.Email) || !string.IsNullOrWhiteSpace(dto.Username))
                throw new ArgumentException("Service accounts cannot be assigned an email or username; these identifiers are generated automatically.");

            // The admin only supplies a display Name; we generate a unique identifier for both
            // username and email so the account is guaranteed unique without requiring the
            // caller to pick a unique name.
            var serviceIdentifier = $"service_{Guid.NewGuid()}";
            username = serviceIdentifier; // Note: we do NOT allow username to be changed for any user- only name
            email = serviceIdentifier;
        }

        var user = new User
        {
            Name = dto.Name,
            Email = email,
            Username = username,
            IsActive = dto.IsActive ?? false,
            IsArchived = dto.IsArchived ?? false,
            IsServiceAccount = isServiceAccount,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            IsServiceAccount = user.IsServiceAccount,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin,
        };
    }

    /// <summary>
    ///     Updates an existing user by ID
    /// </summary>
    /// <param name="userId">The ID of the user to update</param>
    /// <param name="dto">A data transfer object with details on the user to be updated.</param>
    /// <returns>The user which was just updated.</returns>
    /// <exception cref="KeyNotFoundException">Returned if the user was not found.</exception>
    public async Task<UserResponseDto> UpdateUser(long userId, UpdateUserRequestDto dto)
    {
        var user = await _context.Users
            .Where(p => p.Id == userId && !p.IsArchived)
            .FirstOrDefaultAsync();

        if (user == null)
            throw new KeyNotFoundException("User not found.");

        user.Name = dto.Name ?? user.Name;
        user.Username = dto.Username ?? user.Username;
        user.IsArchived = dto.IsArchived ?? user.IsArchived;
        user.IsActive = dto.IsActive ?? user.IsActive;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin
        };
    }

    /// <summary>
    ///     Delete a user by id.
    /// </summary>
    /// <param name="userId">ID of the user to delete.</param>
    /// <returns>Boolean true on successful deletion.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if user is not found.</exception>
    public async Task<bool> DeleteUser(long userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            throw new KeyNotFoundException($"User with id {userId} not found.");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Archive a user by id.
    /// </summary>
    /// <param name="userId">ID of the user to archive.</param>
    /// <returns>Boolean true on successful archival.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if user is not found.</exception>
    public async Task<bool> ArchiveUser(long userId)
    {
        var user = await _context.Users
            .Where(p => p.Id == userId && !p.IsArchived)
            .FirstOrDefaultAsync();

        if (user == null)
            throw new KeyNotFoundException("User not found.");

        user.IsArchived = true;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Unarchive a user by id.
    /// </summary>
    /// <param name="userId">ID of the user to unarchive.</param>
    /// <returns>Boolean true when successfully unarchived.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if user is not found.</exception>
    public async Task<bool> UnarchiveUser(long userId)
    {
        var user = await _context.Users
            .Where(p => p.Id == userId && p.IsArchived)
            .FirstOrDefaultAsync();

        if (user == null)
            throw new KeyNotFoundException("Archived user not found.");

        user.IsArchived = false;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Set a user to sysAdmin. Only works if the user granting admin privilege is also a sysAdmin.
    /// </summary>
    /// <param name="authorizerId">ID of the user who is granting admin privileges</param>
    /// <param name="candidateId">ID of the user who is being granted admin privileges</param>
    /// <returns>Boolean true if successful</returns>
    /// <exception cref="KeyNotFoundException">Returned if authorizer or candidate is not found or lacks privileges</exception>
    public async Task<bool> SetSysAdmin(long authorizerId, long candidateId, bool? isAdmin = true)
    {
        var userIsAdmin = isAdmin ?? true;

        var authorizer = await _context.Users
            .Where(a => a.Id == authorizerId && !a.IsArchived && a.IsSysAdmin)
            .FirstOrDefaultAsync();
        if (authorizer == null)
            throw new KeyNotFoundException($"User with ID {authorizerId} not found or cannot grant admin privileges.");

        var candidate = await _context.Users
            .Where(c => c.Id == candidateId && !c.IsArchived)
            .FirstOrDefaultAsync();
        if (candidate == null)
            throw new KeyNotFoundException($"User with ID {candidateId} not found.");

        if (authorizerId == candidateId && !userIsAdmin)
            throw new InvalidOperationException("You cannot remove your own system administrator access.");

        candidate.IsSysAdmin = userIsAdmin;

        _context.Users.Update(candidate);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Retrieves data overview counts for a user
    /// </summary>
    /// ///
    /// <param name="userId">user id</param>
    /// <returns>Data overview object</returns>
    public async Task<DataOverviewDto> GetUserOverview(long userId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists) throw new KeyNotFoundException($"User with ID {userId} not found.");

        // Filtering projects by a user
        var projectsTotal = _context.ProjectMembers
            .Count(p => p.UserId == userId);

        var datasources = _context.DataSources
            .Where(d => !d.IsArchived)
            .Count(d => d.Project.ProjectMembers.Any(u => u.UserId == userId));

        var records = _context.Records
            .Where(d => !d.IsArchived)
            .Count(d => d.Project.ProjectMembers.Any(u => u.UserId == userId));

        var tags = _context.Tags
            .Where(d => !d.IsArchived)
            .Count(d => d.Project.ProjectMembers.Any(u => u.UserId == userId));

        return new DataOverviewDto
        {
            Projects = projectsTotal,
            Connections = datasources,
            Records = records,
            Tags = tags
        };
    }

    /// <summary>
    ///     Retrieves a user by their SSO ID (Okta ID)
    /// </summary>
    /// <param name="ssoId">The SSO ID (subject claim from JWT token)</param>
    /// <returns>User response DTO if found, null otherwise</returns>
    public async Task<UserResponseDto> GetUserBySsoId(string ssoId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.SsoId == ssoId);

        if (user == null) return null;

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Username = user.Username,
            IsServiceAccount = user.IsServiceAccount,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin,
        };
    }

    /// <summary>
    ///     Retrieves a user by their email address
    /// </summary>
    /// <param name="email">The email address to search for</param>
    /// <returns>User response DTO if found, null otherwise</returns>
    public async Task<UserResponseDto> GetUserByEmail(string email)
    {
        var user = await _context.Users
            .Where(u => u.Email.ToLower() == email.ToLower() && !u.IsArchived)
            .FirstOrDefaultAsync();

        if (user == null) return null;

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Username = user.Username,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin
        };
    }

    /// <summary>
    ///     Retrieves rolling active user counts using the users' most recent successful login timestamp.
    /// </summary>
    /// <param name="projectId">Optional ID for project</param>
    /// <param name="organizationId">Optional ID for organization</param>
    /// <returns>Counts for users active within 24 hours, 7 days, and 30 days</returns>
    public async Task<UserActivityCountsDto> GetActiveUserCounts(long? projectId, long? organizationId)
    {
        var users = BuildActiveUsersQuery(projectId, organizationId);

        var now = UtcNowWithoutTimezone();
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var counts = await users
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ActiveLast24Hours = g.Count(u => u.LastLogin.HasValue && u.LastLogin.Value >= last24Hours),
                ActiveLast7Days = g.Count(u => u.LastLogin.HasValue && u.LastLogin.Value >= last7Days),
                ActiveLast30Days = g.Count(u => u.LastLogin.HasValue && u.LastLogin.Value >= last30Days)
            })
            .FirstOrDefaultAsync();

        return new UserActivityCountsDto
        {
            ActiveLast24Hours = counts?.ActiveLast24Hours ?? 0,
            ActiveLast7Days = counts?.ActiveLast7Days ?? 0,
            ActiveLast30Days = counts?.ActiveLast30Days ?? 0,
            GeneratedAt = now
        };
    }

    /// <summary>
    ///     Retrieves rolling active user counts and users active in the 30-day window.
    /// </summary>
    /// <param name="projectId">Optional ID for project</param>
    /// <param name="organizationId">Optional ID for organization</param>
    /// <returns>Counts and active user details for the requested scope</returns>
    public async Task<UserActivityUsersDto> GetActiveUsers(long? projectId, long? organizationId)
    {
        var counts = await GetActiveUserCounts(projectId, organizationId);
        var last30Days = counts.GeneratedAt.AddDays(-30);

        var users = await BuildActiveUsersQuery(projectId, organizationId)
            .Where(u => u.LastLogin.HasValue && u.LastLogin.Value >= last30Days)
            .OrderByDescending(u => u.LastLogin)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Name = u.Name,
                Username = u.Username,
                Email = u.Email,
                IsSysAdmin = u.IsSysAdmin,
                IsOrgAdmin = organizationId != null
                    ? u.OrganizationUsers.Any(ou => ou.OrganizationId == organizationId && ou.IsOrgAdmin)
                    : null,
                IsArchived = u.IsArchived,
                IsActive = u.IsActive,
                LastLogin = u.LastLogin
            })
            .ToListAsync();

        return new UserActivityUsersDto
        {
            ActiveLast24Hours = counts.ActiveLast24Hours,
            ActiveLast7Days = counts.ActiveLast7Days,
            ActiveLast30Days = counts.ActiveLast30Days,
            GeneratedAt = counts.GeneratedAt,
            Users = users
        };
    }

    private IQueryable<User> BuildActiveUsersQuery(long? projectId, long? organizationId)
    {
        var users = _context.Users.Where(u => !u.IsArchived && u.IsActive);

        if (projectId != null)
            users = users.Where(u =>
                u.ProjectMembers.Any(p => p.ProjectId == projectId && p.UserId == u.Id) ||
                u.Groups.Any(g => g.ProjectMembers.Any(pm => pm.ProjectId == projectId && pm.GroupId == g.Id))
            );

        if (organizationId != null)
            users = users.Where(u =>
                u.OrganizationUsers.Any(ou => ou.OrganizationId == organizationId && ou.UserId == u.Id) ||
                u.Groups.Any(g => g.OrganizationId == organizationId)
            );

        return users;
    }

    private static DateTime UtcNowWithoutTimezone()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }
}
