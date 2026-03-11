using deeplynx.models;

namespace deeplynx.interfaces;

public interface IUserModelTokenBusiness
{
    Task<List<UserModelTokenResponseDto>> GetUserTokens(long userId, long? aiModelConfigId = null);

    Task<UserModelTokenResponseDto> GetTokenById(long currentUserId, long userModelTokenId);

    Task<UserModelTokenResponseDto> CreateUserModelToken(long currentUserId, CreateUserModelTokenRequestDto dto);

    Task<UserModelTokenResponseDto> UpdateUserModelToken(long currentUserId, long userModelTokenId,
        UpdateUserModelTokenRequestDto dto);

    Task<bool> DeleteUserModelToken(long currentUserId, long userModelTokenId);
}