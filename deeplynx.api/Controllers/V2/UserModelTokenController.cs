using Asp.Versioning;
using deeplynx.business;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing User Model Tokens
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, delete, and retrieve User Model Tokens
///     scoped to the currently authenticated user.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("model-tokens")]
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
    ///     Get all User Model Tokens for the current user, optionally filtered by AI Model Configuration.
    /// </summary>
    /// <param name="aiModelConfigId">Optional AI Model Configuration ID used to filter the results.</param>
    /// <returns>A list of User Model Token DTOs belonging to the current user.</returns>
    [HttpGet(Name = "api_get_user_model_tokens")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<IEnumerable<UserModelTokenResponseDto>>> GetUserTokens(
        [FromQuery] long? aiModelConfigId = null)
    {
        var currentUserId = UserContextStorage.UserId;
        var tokens = await _userModelTokenBusiness.GetUserTokens(currentUserId, aiModelConfigId);
        return Ok(tokens);
    }



    /// <summary>
    ///     Get a single User Model Token by ID.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to retrieve.</param>
    /// <returns>The User Model Token DTO matching the specified ID.</returns>
    [HttpGet("{userModelTokenId:long}", Name = "api_get_user_model_token")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserModelTokenResponseDto>> GetTokenById(
        long userModelTokenId)
    {
        var currentUserId = UserContextStorage.UserId;
        var token = await _userModelTokenBusiness.GetTokenById(currentUserId, userModelTokenId);
        return Ok(token);
    }



    /// <summary>
    ///     Create a new User Model Token for the current user.
    /// </summary>
    /// <param name="dto">The data transfer object containing the User Model Token details.</param>
    /// <returns>The newly created User Model Token DTO.</returns>
    [HttpPost(Name = "api_create_user_model_token")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserModelTokenResponseDto>> CreateUserModelToken(
        [FromBody] CreateUserModelTokenRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var newToken = await _userModelTokenBusiness.CreateUserModelToken(currentUserId, dto);
        return Ok(newToken);
    }



    /// <summary>
    ///     Update the token string of an existing User Model Token.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to update.</param>
    /// <param name="dto">The data transfer object containing the updated token string.</param>
    /// <returns>The updated User Model Token DTO.</returns>
    [HttpPut("{userModelTokenId:long}", Name = "api_update_user_model_token")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<UserModelTokenResponseDto>> UpdateUserModelToken(
        long userModelTokenId,
        [FromBody] UpdateUserModelTokenRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var updatedToken = await _userModelTokenBusiness.UpdateUserModelToken(
            currentUserId,
            userModelTokenId,
            dto);
        return Ok(updatedToken);
    }



    /// <summary>
    ///     Permanently delete a User Model Token.
    /// </summary>
    /// <param name="userModelTokenId">The ID of the User Model Token to delete.</param>
    /// <returns>A boolean indicating whether the User Model Token was deleted.</returns>
    [HttpDelete("{userModelTokenId:long}", Name = "api_delete_user_model_token")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> DeleteUserModelToken(long userModelTokenId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _userModelTokenBusiness.DeleteUserModelToken(currentUserId, userModelTokenId);
        return Ok(response);
    }
}
