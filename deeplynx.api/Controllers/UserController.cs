using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

[ApiController]
[ApiVersion(1, Deprecated = true)]
[ApiVersion(2)]
[Route("users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly IUserBusiness _userBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserController" /> class
    /// </summary>
    /// <param name="userBusiness">The business logic interface for handling user operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public UserController(IUserBusiness userBusiness, ILogger<UserController> logger)
    {
        _userBusiness = userBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Users
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeArchived">(Optional) Beelean determining if archived accounts will be included (default: false)</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <param name="includeTestAccounts">(Optional) Boolean determining if test accounts will be included (default: false)</param>
    /// <returns>List of user response DTOs</returns>
    [HttpGet(Name = "api_get_all_users")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] bool includeServiceAccounts = false,
        [FromQuery] bool includeTestAccounts = false)
    {
        try
        {
            var users = await _userBusiness.GetAllUsers(projectId, organizationId, includeArchived, includeServiceAccounts, includeTestAccounts);
            return Ok(users);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching all users.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get All Users
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeArchived">(Optional) Boolean determining if archived accounts will be included (default: false)</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <param name="includeTestAccounts">(Optional) Boolean determining if test accounts will be included (default: false)</param>
    /// <returns>A list of users matching the requested filters.</returns>
    [HttpGet(Name = "api_get_all_users")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsersV2(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] bool includeServiceAccounts = false,
        [FromQuery] bool includeTestAccounts = false)
    {
        var users = await _userBusiness.GetAllUsers(
            projectId,
            organizationId,
            includeArchived,
            includeServiceAccounts,
            includeTestAccounts);
        return Ok(users);
    }

    /// <summary>
    ///     Get a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>User response DTO</returns>
    [HttpGet("{userId:long}", Name = "api_get_a_user")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<UserResponseDto>> GetUser(long userId)
    {
        try
        {
            var user = await _userBusiness.GetUser(userId);
            return Ok(user);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching user {userId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>The requested user.</returns>
    [HttpGet("{userId:long}", Name = "api_get_a_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserResponseDto>> GetUserV2(long userId)
    {
        var user = await _userBusiness.GetUser(userId);
        return Ok(user);
    }

    /// <summary>
    ///     Get the Local Development User
    /// </summary>
    /// <returns>User response DTO with the local dev user info</returns>
    [HttpGet("superuser", Name = "api_get_local_dev_user")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<UserResponseDto>> GetLocalDevUser()
    {
        try
        {
            var user = await _userBusiness.GetLocalDevUser();
            return Ok(user);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while fetching local dev user: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get the Local Development User
    /// </summary>
    /// <returns>The local development user.</returns>
    [HttpGet("superuser", Name = "api_get_local_dev_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserResponseDto>> GetLocalDevUserV2()
    {
        var user = await _userBusiness.GetLocalDevUser();
        return Ok(user);
    }

    /// <summary>
    ///     Create a User
    /// </summary>
    /// <param name="dto">User request DTO</param>
    /// <returns>User response DTO</returns>
    [HttpPost(Name = "api_create_a_user")]
    [MapToApiVersion(1)]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserRequestDto dto)
    {
        try
        {
            var newUser = await _userBusiness.CreateUser(dto);
            return Ok(newUser);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while creating this user.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a User
    /// </summary>
    /// <param name="dto">User request DTO</param>
    /// <returns>The newly created user.</returns>
    [HttpPost(Name = "api_create_a_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateUserV2([FromBody] CreateUserRequestDto dto)
    {
        var newUser = await _userBusiness.CreateUser(dto);
        return Ok(newUser);
    }

    /// <summary>
    ///     Create a Test Account
    /// </summary>
    /// <param name="name">Display name for the test account</param>
    /// <returns>User response DTO</returns>
    [Tags("Test Accounts")]
    [HttpPost("test", Name = "api_create_test_account")]
    [MapToApiVersion(1)]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateTestAccount([FromQuery] string name)
    {
        try
        {
            var newUser = await _userBusiness.CreateTestAccount(name);
            return Ok(newUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test account");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An unexpected error occurred while creating the test account." });
        }
    }

    /// <summary>
    ///     Create a Test Account
    /// </summary>
    /// <param name="name">Display name for the test account</param>
    /// <returns>The newly created test account.</returns>
    [Tags("Test Accounts")]
    [HttpPost("test", Name = "api_create_test_account")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateTestAccountV2([FromQuery] string name)
    {
        var newUser = await _userBusiness.CreateTestAccount(name);
        return Ok(newUser);
    }

    /// <summary>
    ///     Update a User
    /// </summary>
    /// ///
    /// <param name="userId">ID of user</param>
    /// <param name="dto">User request DTO</param>
    /// <returns>User response DTO</returns>
    [HttpPut("{userId:long}", Name = "api_update_a_user")]
    [MapToApiVersion(1)]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(long userId, [FromBody] UpdateUserRequestDto dto)
    {
        try
        {
            var updatedUser = await _userBusiness.UpdateUser(userId, dto);
            return Ok(updatedUser);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while updating this user {userId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <param name="dto">User request DTO</param>
    /// <returns>The updated user.</returns>
    [HttpPut("{userId:long}", Name = "api_update_a_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> UpdateUserV2(
        long userId,
        [FromBody] UpdateUserRequestDto dto)
    {
        var updatedUser = await _userBusiness.UpdateUser(userId, dto);
        return Ok(updatedUser);
    }

    /// <summary>
    ///     Deletes a User
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <returns>A message stating the user was successfully deleted.</returns>
    [HttpDelete("{userId:long}", Name = "api_delete_a_user")]
    [MapToApiVersion(1)]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> DeleteUser(long userId)
    {
        try
        {
            await _userBusiness.DeleteUser(userId);
            return Ok(new { message = $"Deleted user {userId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting user {userId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Deletes a User
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{userId:long}", Name = "api_delete_a_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> DeleteUserV2(long userId)
    {
        var response = await _userBusiness.DeleteUser(userId);
        return Ok(response);
    }

    /// <summary>
    ///     Archive or Unarchive a User
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="archive">True to archive the user, false to unarchive it.</param>
    /// <returns>A message stating the user was successfully archived or unarchived.</returns>
    [HttpPatch("{userId:long}", Name = "api_archive_user")]
    [MapToApiVersion(1)]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> ArchiveUser(
        long userId,
        [FromQuery] bool archive)
    {
        try
        {
            if (archive)
            {
                await _userBusiness.ArchiveUser(userId);
                return Ok(new { message = $"Archived user {userId}" });
            }

            await _userBusiness.UnarchiveUser(userId);
            return Ok(new { message = $"Unarchived user {userId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} user {userId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a User
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="archive">True to archive the user, false to unarchive it.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{userId:long}", Name = "api_archive_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> ArchiveUserV2(
        long userId,
        [FromQuery] bool archive)
    {
        if (archive)
        {
            var archiveResponse = await _userBusiness.ArchiveUser(userId);
            return Ok(archiveResponse);
        }

        var response = await _userBusiness.UnarchiveUser(userId);
        return Ok(response);
    }

    /// <summary>
    ///     Grant System Admin Rights
    /// </summary>
    /// <param name="userId">ID of user to grant the sysadmin rights to </param>
    /// <returns>User response DTO</returns>
    [HttpPatch("{userId:long}/admin", Name = "api_set_sys_admin")]
    [MapToApiVersion(1)]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> SetSysAdmin(
        long userId,
        [FromQuery] bool? isAdmin = true
    )
    {
        try
        {
            // get the authorizer ID from the middleware context
            var authorizerId = UserContextStorage.UserId;
            var granted = await _userBusiness.SetSysAdmin(authorizerId, userId, isAdmin);
            var userIsAdmin = isAdmin ?? true;
            return Ok(new
            {
                message = userIsAdmin
                    ? $"Granted sysadmin rights to user {userId}"
                    : $"Removed sysAdmin rights from user {userId}"
            });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while setting user {userId} as admin: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Grant or Remove System Admin Rights
    /// </summary>
    /// <param name="userId">ID of user whose sysadmin rights will be updated</param>
    /// <param name="isAdmin">True to grant sysadmin rights; false to remove them.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{userId:long}/admin", Name = "api_set_sys_admin")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult> SetSysAdminV2(
        long userId,
        [FromQuery] bool? isAdmin = true)
    {
        var authorizerId = UserContextStorage.UserId;
        var response = await _userBusiness.SetSysAdmin(authorizerId, userId, isAdmin);
        return Ok(response);
    }

    /// <summary>
    ///     Get Data Overview for User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>Data overview DTO</returns>
    [HttpGet("{userId:long}/overview", Name = "api_get_a_user_overview")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<DataOverviewDto>> GetDataOverview(long userId)
    {
        try
        {
            var user = await _userBusiness.GetUserOverview(userId);
            return Ok(user);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching user {userId} data overview: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Data Overview for User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>The requested user's data overview.</returns>
    [HttpGet("{userId:long}/overview", Name = "api_get_a_user_overview")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<DataOverviewDto>> GetDataOverviewV2(long userId)
    {
        var user = await _userBusiness.GetUserOverview(userId);
        return Ok(user);
    }

    /// <summary>
    ///     Get the Current Authenticated User
    /// </summary>
    /// <param name="organizationId">If specified, return boolean if user is admin of this org</param>
    /// <param name="projectId">If specified, return boolean if user is admin of this project</param>
    /// <returns>User response DTO</returns>
    [HttpGet("current", Name = "api_get_current_user")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<UserAdminInfoDto>> GetCurrentUser(
        [FromQuery] long? organizationId,
        [FromQuery] long? projectId)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            var user = await _userBusiness.GetUserAdminInfo(userId, organizationId, projectId);
            return Ok(user);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching current user: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get the Current Authenticated User
    /// </summary>
    /// <param name="organizationId">If specified, return whether the user is an admin of this organization.</param>
    /// <param name="projectId">If specified, return whether the user is an admin of this project.</param>
    /// <returns>The current user's account and administrator information.</returns>
    [HttpGet("current", Name = "api_get_current_user")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserAdminInfoDto>> GetCurrentUserV2(
        [FromQuery] long? organizationId,
        [FromQuery] long? projectId)
    {
        var userId = UserContextStorage.UserId;
        var user = await _userBusiness.GetUserAdminInfo(userId, organizationId, projectId);
        return Ok(user);
    }


    /// <summary>
    ///     Get rolling active user counts
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <returns>Active user counts for 24-hour, 7-day, and 30-day windows</returns>
    [HttpGet("active-counts", Name = "api_get_active_user_counts")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<UserActivityCountsDto>> GetActiveUserCounts(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeServiceAccounts = false)
    {
        try
        {
            var counts = await _userBusiness.GetActiveUserCounts(projectId, organizationId, includeServiceAccounts);
            return Ok(counts);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching active user counts.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get rolling active user counts
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <returns>Active user counts for 24-hour, 7-day, and 30-day windows.</returns>
    [HttpGet("active-counts", Name = "api_get_active_user_counts")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserActivityCountsDto>> GetActiveUserCountsV2(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeServiceAccounts = false)
    {
        var counts = await _userBusiness.GetActiveUserCounts(projectId, organizationId, includeServiceAccounts);
        return Ok(counts);
    }

    /// <summary>
    ///     Get rolling active user counts and active user details
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <returns>Active user counts and users active in the 30-day window</returns>
    [HttpGet("active-users", Name = "api_get_active_users")]
    [MapToApiVersion(1)]
    public async Task<ActionResult<UserActivityUsersDto>> GetActiveUsers(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeServiceAccounts = false)
    {
        try
        {
            var activity = await _userBusiness.GetActiveUsers(projectId, organizationId, includeServiceAccounts);
            return Ok(activity);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching active users.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get rolling active user counts and active user details
    /// </summary>
    /// <param name="projectId">(Optional) ID of project that users are associated with</param>
    /// <param name="organizationId">(Optional) ID of organization that users are associated with</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <returns>Active user counts and users active in the 30-day window.</returns>
    [HttpGet("active-users", Name = "api_get_active_users")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserActivityUsersDto>> GetActiveUsersV2(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeServiceAccounts = false)
    {
        var activity = await _userBusiness.GetActiveUsers(projectId, organizationId, includeServiceAccounts);
        return Ok(activity);
    }
}
