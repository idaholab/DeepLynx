using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class UserModelTokenBusiness : IUserModelTokenBusiness
{
    private readonly DeeplynxContext _context;
    private readonly EncryptionHelper _encryptionHelper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserModelTokenBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for operations.</param>
    public UserModelTokenBusiness(DeeplynxContext context, EncryptionHelper encryptionHelper)
    {
        _context = context;
        _encryptionHelper = encryptionHelper;
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

        foreach (var token in userModelTokens)
        {
            if (!string.IsNullOrEmpty(token.Token))
                token.Token = _encryptionHelper.Decrypt(token.Token);
        }

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
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId && x.UserId == currentUserId);

        if (userModelToken is null)
        {
            throw new KeyNotFoundException($"No user model token was found");
        }

        if (!string.IsNullOrEmpty(userModelToken.Token))
            userModelToken.Token = _encryptionHelper.Decrypt(userModelToken.Token);

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

        var aiModelConfig = await _context.AiModelConfigs.FirstOrDefaultAsync(c => c.Id == dto.AiModelConfigId);

        if (aiModelConfig is null)
            throw new KeyNotFoundException($"No AI Model Config found with ID: {dto.AiModelConfigId}");

        var tokenExists = await _context.UserModelTokens
            .AnyAsync(x => x.UserId == currentUserId && x.AiModelConfigId == dto.AiModelConfigId);

        if (tokenExists)
            throw new InvalidOperationException("A token for this AI Model Config already exists.");

        if (dto.Token.Length <= 8)
            throw new InvalidOperationException("Token length looks too short to be valid.");

        var encryptedToken = _encryptionHelper.Encrypt(dto.Token);

        var userModelToken = new UserModelToken
        {
            UserId = currentUserId,
            AiModelConfigId = aiModelConfig.Id,
            Token = encryptedToken,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        };
        _context.UserModelTokens.Add(userModelToken);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(userModelToken.Token))
            userModelToken.Token = _encryptionHelper.Decrypt(userModelToken.Token);

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
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId && x.UserId == currentUserId);

        if (userModelToken is null)
        {
            throw new KeyNotFoundException($"No user model token was found with the ID: {userModelTokenId}");
        }

        var encryptedToken = _encryptionHelper.Encrypt(dto.Token);

        userModelToken.Token = encryptedToken;
        userModelToken.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(userModelToken.Token))
            userModelToken.Token = _encryptionHelper.Decrypt(userModelToken.Token);

        return MapToDto(userModelToken);
    }

    /// <summary>
    ///     Permanently delete a User Model Token.
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the token belongs.</param>
    /// <param name="userModelTokenId">The ID of the User Model Token to delete.</param>
    /// <returns>A boolean value determining if the User Model Token was successfully deleted.</returns>
    public async Task<bool> DeleteUserModelToken(long currentUserId, long userModelTokenId)
    {
        var tokenToBeDeleted = await _context.UserModelTokens
            .FirstOrDefaultAsync(x => x.Id == userModelTokenId && x.UserId == currentUserId);

        if (tokenToBeDeleted is null)
            throw new KeyNotFoundException($"User Model Token with ID: {userModelTokenId} not found");

        _context.Remove(tokenToBeDeleted);
        await _context.SaveChangesAsync();

        return true;
    }

    private static UserModelTokenResponseDto MapToDto(UserModelToken x) => new()
    {
        Id = x.Id,
        UserId = x.UserId,
        AiModelConfigId = x.AiModelConfigId,
        Token = TokenHelper.MaskToken(x.Token),
        LastUpdatedAt = x.LastUpdatedAt
    };
}

public static class TokenHelper
{
    public static string MaskToken(string token)
    {
        return new string('*', token.Length - 4) + token[^4..];
    }
}