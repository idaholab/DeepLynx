using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class UserModelTokenBusiness : IUserModelTokenBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserModelTokenBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for operations.</param>
    public UserModelTokenBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Get all User Model Tokens for a user, optionally filtered by AI Model Configuration.
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user and whose tokens are being retrieved.</param>
    /// <param name="aiModelConfigId">Optional. When provided, filters results to only tokens associated with the specified AI Model Configuration.</param>
    /// <returns>A list of User Model Token DTOs belonging to the specified user.</returns>
    public async Task<List<UserModelTokenResponseDto>> GetUserTokens(long currentUserId, long? aiModelConfigId = null)
    {
        var query = _context.UserModelTokens
            .Where(x => x.UserId == currentUserId);

        if (aiModelConfigId.HasValue)
        {
            query = query.Where(x => x.AiModelConfigId == aiModelConfigId.Value);
        }

        var userModelTokens = await query.ToListAsync();

        return userModelTokens.Select(MapToDto).ToList();
    }

    /// <summary>
    ///     Get a single User Model Token by ID.
    /// </summary>
    /// <param name="currentUserId">The ID of requesting user and to whom the token belongs.</param>
    /// <param name="userModelTokenId">The ID of the User Model Token to retrieve.</param>
    /// <returns>The User Model Token DTO matching the specified ID.</returns>
    public async Task<UserModelTokenResponseDto> GetTokenById(long currentUserId, long userModelTokenId)
    {
        var userModelToken = await _context.UserModelTokens
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId);

        if (userModelToken is null)
        {
            throw new KeyNotFoundException($"No user model token was found with the ID: {userModelTokenId}");
        }

        if (currentUserId != userModelToken.UserId)
        {
            throw new UnauthorizedAccessException("Action denied, cannot access another users Tokens");
        }

        return MapToDto(userModelToken);
    }

    /// <summary>
    ///     Create a new User Model Token for a user.
    /// </summary>
    /// <param name="currentUserId">The ID of the user for whom the token is being created.</param>
    /// <param name="dto">The data transfer object containing the details of the User Model Token to create.</param>
    /// <returns>The newly created User Model Token DTO.</returns>
    public async Task<UserModelTokenResponseDto> CreateUserModelToken(long currentUserId, CreateUserModelTokenRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var userModelToken = new UserModelToken
        {
            UserId = currentUserId,
            AiModelConfigId = dto.AiModelConfigId,
            Token = dto.Token,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        };
        _context.UserModelTokens.Add(userModelToken);
        await _context.SaveChangesAsync();

        return MapToDto(userModelToken);
    }
    
    /// <summary>
    ///     Update the token string of an existing User Model Token.
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the token belongs.</param>
    /// <param name="userModelTokenId">The ID of the User Model Token to update.</param>
    /// <param name="dto">The data transfer object containing the updated token string.</param>
    /// <returns>The updated User Model Token DTO.</returns>
    public async Task<UserModelTokenResponseDto> UpdateUserModelToken(long currentUserId, long userModelTokenId, UpdateUserModelTokenRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var userModelToken = await _context.UserModelTokens
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId);

        if (userModelToken is null)
        {
            throw new KeyNotFoundException($"No user model token was found with the ID: {userModelTokenId}");
        }

        if (currentUserId != userModelToken.UserId)
        {
            throw new UnauthorizedAccessException("Action denied, cannot modify another user's tokens");
        }

        userModelToken.Token = dto.Token;
        userModelToken.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        return MapToDto(userModelToken);
    }
    
    /// <summary>
    ///     Permanently delete a User Model Token.
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the token belongs.</param>
    /// <param name="userModelTokenId">The ID of the User Model Token to delete.</param>
    /// <returns>A message confirming the User Model Token was successfully deleted.</returns>
    public async Task<bool> DeleteUserModelToken(long currentUserId, long userModelTokenId)
    {
        var tokenToBeDeleted = await _context.UserModelTokens
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId);

        if (tokenToBeDeleted is null)
            throw new KeyNotFoundException($"User Model Token with ID: {userModelTokenId} not found");

        if (tokenToBeDeleted.UserId != currentUserId)
        {
            throw new UnauthorizedAccessException("Action denied, cannot delete another user's tokens");
        }

        _context.Remove(tokenToBeDeleted);
        await _context.SaveChangesAsync();

        return true;
    }

    private static UserModelTokenResponseDto MapToDto(UserModelToken x) => new()
    {
        Id = x.Id,
        UserId = x.UserId,
        AiModelConfigId = x.AiModelConfigId,
        Token = x.Token,
        LastUpdatedAt = x.LastUpdatedAt
    };
}