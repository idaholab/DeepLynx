using deeplynx.business;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing User Model Tokens
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, delete, and retrieve User Model Tokens
///     scoped to the currently authenticated user.
/// </remarks>
[ApiController]
[Route("users/{userId:long}/model-tokens")]
[Authorize]
[Tags("User Model Token")]
public class UserModelTokenController : ControllerBase
{
    private readonly IUserModelTokenBusiness _userModelTokenBusiness;
    private readonly ILogger<UserModelTokenController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserModelTokenController" />
    /// </summary>
    /// <param name="userModelTokenBusiness">The business layer used for User Model Token operations.</param>
    /// <param name="logger">The logger for this controller.</param>
    public UserModelTokenController(IUserModelTokenBusiness userModelTokenBusiness,
        ILogger<UserModelTokenController> logger)
    {
        _userModelTokenBusiness = userModelTokenBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get all User Model Tokens for a user, optionally filtered by AI Model Configuration.
    /// </summary>
    /// <param name="aiModelConfigId">Optional. When provided, filters results to only tokens associated with the specified AI Model Configuration.</param>
    /// <returns>A list of User Model Token DTOs belonging to the specified user.</returns>
    [HttpGet(Name = "api_get_user_model_tokens")]
    public async Task<ActionResult<IEnumerable<UserModelTokenResponseDto>>> GetUserTokens(
        [FromQuery] long? aiModelConfigId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var tokens = await _userModelTokenBusiness.GetUserTokens(currentUserId, aiModelConfigId);
            return Ok(tokens);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching User Model Tokens: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a single User Model Token by ID.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to retrieve.</param>
    /// <returns>The User Model Token DTO matching the specified ID.</returns>
    [HttpGet("{userModelTokenId:long}", Name = "api_get_user_model_token")]
    public async Task<ActionResult<UserModelTokenResponseDto>> GetTokenById(
        long userModelTokenId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var token = await _userModelTokenBusiness.GetTokenById(currentUserId, userModelTokenId);
            return Ok(token);
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(new { message = exc.Message });
        }
        catch (UnauthorizedAccessException exc)
        {
            _logger.LogWarning(exc.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exc.Message });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching User Model Token {userModelTokenId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a new User Model Token for a user.
    /// </summary>
    /// <param name="dto">The data transfer object containing the details of the User Model Token to create.</param>
    /// <returns>The newly created User Model Token DTO.</returns>
    [HttpPost(Name = "api_create_user_model_token")]
    public async Task<ActionResult<UserModelTokenResponseDto>> CreateUserModelToken(
        [FromBody] CreateUserModelTokenRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var newToken = await _userModelTokenBusiness.CreateUserModelToken(currentUserId, dto);
            return Ok(newToken);
        }
        catch (UnauthorizedAccessException exc)
        {
            _logger.LogWarning(exc.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exc.Message });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while creating a User Model Token: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Update the token string of an existing User Model Token.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to update.</param>
    /// <param name="dto">The data transfer object containing the updated token string.</param>
    /// <returns>The updated User Model Token DTO.</returns>
    [HttpPut("{userModelTokenId:long}", Name = "api_update_user_model_token")]
    public async Task<ActionResult<UserModelTokenResponseDto>> UpdateUserModelToken(
        long userModelTokenId,
        [FromBody] UpdateUserModelTokenRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedToken = await _userModelTokenBusiness.UpdateUserModelToken(currentUserId, userModelTokenId, dto);
            return Ok(updatedToken);
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(new { message = exc.Message });
        }
        catch (UnauthorizedAccessException exc)
        {
            _logger.LogWarning(exc.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exc.Message });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while updating User Model Token {userModelTokenId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Permanently delete a User Model Token.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to delete.</param>
    /// <returns>A message confirming the User Model Token was successfully deleted.</returns>
    [HttpDelete("{userModelTokenId:long}", Name = "api_delete_user_model_token")]
    public async Task<IActionResult> DeleteUserModelToken(
        long userModelTokenId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _userModelTokenBusiness.DeleteUserModelToken(currentUserId, userModelTokenId);
            return Ok(new { message = $"Deleted User Model Token {userModelTokenId}" });
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(new { message = exc.Message });
        }
        catch (UnauthorizedAccessException exc)
        {
            _logger.LogWarning(exc.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exc.Message });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while deleting User Model Token {userModelTokenId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}