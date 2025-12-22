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
    public async Task<IEnumerable<UserResponseDto>> GetAllUsers(long? projectId, long? organizationId)
    {
        var users = _context.Users.Where(p => !p.IsArchived);

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
            IsArchived = p.IsArchived,
            IsActive = p.IsActive
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
            IsActive = user.IsActive
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
            IsActive = user.IsActive
        };
    }

    /// <summary>
    ///     Creates a new user based on the data transfer object supplied.
    /// </summary>
    /// <param name="dto">A data transfer object with details on the new user to be created.</param>
    /// <returns>The new user which was just created.</returns>
    public async Task<UserResponseDto> CreateUser(CreateUserRequestDto dto)
    {
        // TODO: adjusting is_sys_admin is currently disabled. Enable once route permission protections are in place
        var otherUserHasEmail = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        if (otherUserHasEmail) throw new ArgumentException("User with email already exists");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            Username = dto.Username,
            IsActive = dto.IsActive ?? false,
            IsArchived = dto.IsArchived ?? false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive
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
            IsActive = user.IsActive
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
    public async Task<bool> SetSysAdmin(long authorizerId, long candidateId)
    {
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

        candidate.IsSysAdmin = true;

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
            IsSysAdmin = user.IsSysAdmin,
            IsArchived = user.IsArchived,
            IsActive = user.IsActive
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
            IsActive = user.IsActive
        };
    }
}