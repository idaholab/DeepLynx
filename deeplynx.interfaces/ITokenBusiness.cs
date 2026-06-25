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
        long currentUserId,
        long serviceAccountId);
    Task<TokenResponseDto> GenerateTestAccountApiKey(long currentUserId, long testAccountId);
    Task<ApiKey> GetApiKey(string apiKey);
    Task<bool> DeleteApiKey(long currentUserId, string key);
    Task<List<string>> GetAllUserKeys(long currentUserId);
    string HashApiSecret(string rawSecret);
    bool VerifyApiSecret(string providedKey, string storedHash);
    Task<bool> RevokeToken(string jti);
    Task<bool> IsTokenRevoked(string jti);
    Task<int> RevokeAllUserTokens(long currentUserId);
}