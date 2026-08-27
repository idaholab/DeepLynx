using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

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
    /// <param name="includeArchived">(Optional) Boolean determining if archived accounts will be included (default: false)</param>
    /// <param name="includeServiceAccounts">(Optional) Boolean determining if service accounts will be included (default: false)</param>
    /// <param name="includeTestAccounts">(Optional) Boolean determining if test accounts will be included (default: false)</param>
    /// <param name="activeOnly">(Optional) Boolean determining if only users where IsActive is true will be included (default: false)</param>
    /// <param name="recentLoginOnly">(Optional) Boolean determining if only users who have logged in within the last 30 days will be included (default: false)</param>
    /// <param name="paginatedRequestDto">Pagination parameters</param>
    /// <returns>A list of users matching the requested filters.</returns>
    [HttpGet(Name = "api_get_all_users")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<PaginatedResponse<UserResponseDto>>> GetAllUsers(
        [FromQuery] long? projectId,
        [FromQuery] long? organizationId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] bool includeServiceAccounts = false,
        [FromQuery] bool includeTestAccounts = false,
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool recentLoginOnly = false,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var users = await _userBusiness.GetAllUsersPaginated(
            paginatedRequestDto,
            projectId,
            organizationId,
            includeArchived,
            includeServiceAccounts,
            includeTestAccounts,
            activeOnly,
            recentLoginOnly);
        return Ok(users);
    }



    /// <summary>
    ///     Get a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <returns>The requested user.</returns>
    [HttpGet("{userId:long}", Name = "api_get_a_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserResponseDto>> GetUser(long userId)
    {
        var user = await _userBusiness.GetUser(userId);
        return Ok(user);
    }



    /// <summary>
    ///     Get the Local Development User
    /// </summary>
    /// <returns>The local development user.</returns>
    [HttpGet("superuser", Name = "api_get_local_dev_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserResponseDto>> GetLocalDevUser()
    {
        var user = await _userBusiness.GetLocalDevUser();
        return Ok(user);
    }



    /// <summary>
    ///     Create a User
    /// </summary>
    /// <param name="dto">User request DTO</param>
    /// <returns>The newly created user.</returns>
    [HttpPost(Name = "api_create_a_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserRequestDto dto)
    {
        var newUser = await _userBusiness.CreateUser(dto);
        return Ok(newUser);
    }



    /// <summary>
    ///     Create a Test Account
    /// </summary>
    /// <param name="name">Display name for the test account</param>
    /// <returns>The newly created test account.</returns>
    [Tags("Test Accounts")]
    [HttpPost("test", Name = "api_create_test_account")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> CreateTestAccount([FromQuery] string name)
    {
        var newUser = await _userBusiness.CreateTestAccount(name);
        return Ok(newUser);
    }



    /// <summary>
    ///     Update a User
    /// </summary>
    /// <param name="userId">ID of user</param>
    /// <param name="dto">User request DTO</param>
    /// <returns>The updated user.</returns>
    [HttpPut("{userId:long}", Name = "api_update_a_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin(unscoped: true)]
    [ForbidServiceAccounts]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(
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
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{userId:long}", Name = "api_delete_a_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> DeleteUser(long userId)
    {
        var response = await _userBusiness.DeleteUser(userId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a User
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="archive">True to archive the user, false to unarchive it.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{userId:long}", Name = "api_archive_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<IActionResult> ArchiveUser(
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
    ///     Grant or Remove System Admin Rights
    /// </summary>
    /// <param name="userId">ID of user whose sysadmin rights will be updated</param>
    /// <param name="isAdmin">True to grant sysadmin rights; false to remove them.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{userId:long}/admin", Name = "api_set_sys_admin")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    [ForbidServiceAccounts]
    public async Task<ActionResult> SetSysAdmin(
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
    /// <returns>The requested user's data overview.</returns>
    [HttpGet("{userId:long}/overview", Name = "api_get_a_user_overview")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<DataOverviewDto>> GetDataOverview(long userId)
    {
        var user = await _userBusiness.GetUserOverview(userId);
        return Ok(user);
    }



    /// <summary>
    ///     Get the Current Authenticated User
    /// </summary>
    /// <param name="organizationId">If specified, return whether the user is an admin of this organization.</param>
    /// <param name="projectId">If specified, return whether the user is an admin of this project.</param>
    /// <returns>The current user's account and administrator information.</returns>
    [HttpGet("current", Name = "api_get_current_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserAdminInfoDto>> GetCurrentUser(
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
    /// <returns>Active user counts for 24-hour, 7-day, and 30-day windows.</returns>
    [HttpGet("active-counts", Name = "api_get_active_user_counts")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserActivityCountsDto>> GetActiveUserCounts(
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
    /// <returns>Active user counts and users active in the 30-day window.</returns>
//     [HttpGet("active-users", Name = "api_get_active_users")]
//     [Badge("V2", BadgePosition.Before, "#72e6a1")]
//     public async Task<ActionResult<UserActivityUsersDto>> GetActiveUsers(
//         [FromQuery] long? projectId,
//         [FromQuery] long? organizationId,
//         [FromQuery] bool includeServiceAccounts = false)
//     {
//         var activity = await _userBusiness.GetActiveUsers(projectId, organizationId, includeServiceAccounts);
//         return Ok(activity);
//     }
}
