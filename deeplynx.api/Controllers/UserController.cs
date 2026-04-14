using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
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
    /// <returns>List of user response DTOs</returns>
    [HttpGet(Name = "api_get_all_users")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId)
    {
        try
        {
            var users = await _userBusiness.GetAllUsers(projectId, organizationId);
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
    ///     Get a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>User response DTO</returns>
    [HttpGet("{userId:long}", Name = "api_get_a_user")]
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
    ///     Get the Local Development User
    /// </summary>
    /// <returns>User response DTO with the local dev user info</returns>
    [HttpGet("superuser", Name = "api_get_local_dev_user")]
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
    ///     Create a User
    /// </summary>
    /// <param name="dto">User request DTO</param>
    /// <returns>User response DTO</returns>
    [HttpPost(Name = "api_create_a_user")]
    [OrgAdmin]
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
    ///     Update a User
    /// </summary>
    /// ///
    /// <param name="userId">ID of user</param>
    /// <param name="dto">User request DTO</param>
    /// <returns>User response DTO</returns>
    [HttpPut("{userId:long}", Name = "api_update_a_user")]
    [OrgAdmin]
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
    ///     Deletes a User
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <returns>A message stating the user was successfully deleted.</returns>
    [HttpDelete("{userId:long}", Name = "api_delete_a_user")]
    [SysAdmin]
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
    ///     Archive or Unarchive a User
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="archive">True to archive the user, false to unarchive it.</param>
    /// <returns>A message stating the user was successfully archived or unarchived.</returns>
    [HttpPatch("{userId:long}", Name = "api_archive_user")]
    [SysAdmin]
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
    ///     Grant System Admin Rights
    /// </summary>
    /// <param name="userId">ID of user to grant the sysadmin rights to </param>
    /// <returns>User response DTO</returns>
    [HttpPatch("{userId:long}/admin", Name = "api_set_sys_admin")]
    [SysAdmin]
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
    ///     Get Data Overview for User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>Data overview DTO</returns>
    [HttpGet("{userId:long}/overview", Name = "api_get_a_user_overview")]
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
    ///     Get the Current Authenticated User
    /// </summary>
    /// <param name="organizationId">If specified, return boolean if user is admin of this org</param>
    /// <param name="projectId">If specified, return boolean if user is admin of this project</param>
    /// <returns>User response DTO</returns>
    [HttpGet("current", Name = "api_get_current_user")]
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
}
