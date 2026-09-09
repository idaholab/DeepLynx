using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.interfaces;

public interface ITokenBusiness
{
    Task<string> CreateToken(string apiKey, string apiSecret, double? expirationMinutes);

    Task<TokenResponseDto> CreateApiKey(long currentUserId, string? clientId = null, long? createdByUserId = null,
        bool allowServiceAccount = false);
    Task<TokenResponseDto> GenerateServiceAccountApiKey(
        long currentUserId, long organizationId, long projectId, long serviceAccountId);
    Task<TokenResponseDto> GenerateTestAccountApiKey(long currentUserId, long testAccountId);
    Task<ApiKey> GetApiKey(string apiKey);
    Task<bool> DeleteApiKey(long currentUserId, string key);
    [Obsolete("V1-only. Used by deprecated v1 token endpoints. Superseded by GetAllUserKeysPaginated. " +
              "Remove once v1 token endpoints are sunset.", error: false)]
    Task<List<string>> GetAllUserKeys(long currentUserId);
    Task<PaginatedResponse<string>> GetAllUserKeysPaginated(long currentUserId, PaginatedRequestDto paginatedRequestDto);
    string HashApiSecret(string rawSecret);
    bool VerifyApiSecret(string providedKey, string storedHash);
    Task<bool> RevokeToken(string jti);
    Task<bool> IsTokenRevoked(string jti);
    Task<int> RevokeAllUserTokens(long currentUserId);
}