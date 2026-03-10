using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class UserModelTokenBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///  Initializes a new instance of the UserModelToken class
    /// </summary>
    /// <param name="context">The database context used for operations</param>
    public UserModelTokenBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///  Retrieves a list of User Model Tokens by UserId, optionally filtered by AiModelConfigId
    /// </summary>
    /// <param name="userId">The Id of the user whose User Model Tokens will be retrieved</param>
    /// <param name="aiModelConfigId">Optional. When provided, filters results to only tokens for the specified model config</param>
    public async Task<List<UserModelTokenResponseDto>> GetUserTokens(long userId, long? aiModelConfigId = null)
    {
        var query = _context.UserModelTokens
            .Where(x => x.UserId == userId);

        if (aiModelConfigId.HasValue)
        {
            query = query.Where(x => x.AiModelConfigId == aiModelConfigId.Value);
        }

        var userModelTokens = await query.ToListAsync();

        return userModelTokens.Select(MapToDto).ToList();
    }

    /// <summary>
    ///  Retrieves a User Model Token by its Id
    /// </summary>
    /// <param name="currentUserId">The Id of the User to which this Token belongs</param>
    /// <param name="userModelTokenId">The Id of the User Model Token that will be retrieved</param>
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
    ///  Creates a new User Model Token
    /// </summary>
    /// <param name="currentUserId">The Id of the User to which this Token belongs</param>
    /// <param name="dto">The DTO containing the properties needed for a User Model Token</param>
    public async Task<UserModelTokenResponseDto> CreateUserModelToken(long currentUserId, CreateUserModelTokenRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        if (currentUserId != dto.UserId)
        {
            throw new UnauthorizedAccessException("Action denied, cannot configure a token for another user");
        }

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
    ///  Deletes a User Model Token
    /// </summary>
    /// <param name="currentUserId">The Id of the requesting User and to which this Token belongs</param>
    /// <param name="userModelTokenId">The Id of the User Model Token to be updated</param>
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
        Token = x.Token,
        LastUpdatedAt = x.LastUpdatedAt
    };
}