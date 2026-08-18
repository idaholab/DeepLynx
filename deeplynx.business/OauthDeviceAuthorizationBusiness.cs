using System.Security.Cryptography;
using System.Text;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class OauthDeviceAuthorizationBusiness : IOauthDeviceAuthorizationBusiness
{
    private const int DeviceCodeBytes = 32;
    private const int ExpiresInSeconds = 900;
    private const int DefaultPollingIntervalSeconds = 5;
    private const int UserCodeLength = 8;
    private const int AccessTokenExpirationMinutes = 480;
    private const int RefreshTokenBytes = 64;
    private const int RefreshTokenExpirationDays = 30;
    private const int SlowDownIntervalSeconds = 5;
    private const string UserCodeAlphabet = "23456789BCDFGHJKLMNPQRSTVWXZ";

    private readonly DeeplynxContext _context;
    private readonly ILogger<OauthDeviceAuthorizationBusiness> _logger;
    private readonly ITokenBusiness _tokenBusiness;

    public OauthDeviceAuthorizationBusiness(
        DeeplynxContext context,
        ITokenBusiness tokenBusiness,
        ILogger<OauthDeviceAuthorizationBusiness> logger)
    {
        _context = context;
        _tokenBusiness = tokenBusiness;
        _logger = logger;
    }

    public async Task<DeviceAuthorizationResponseDto> CreateDeviceAuthorizationRequest(
        string? clientId,
        string? scope,
        string verificationUri)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new OauthException("invalid_request", "client_id is required", 400);
        }

        if (string.IsNullOrWhiteSpace(verificationUri))
        {
            throw new OauthException("invalid_request", "verification_uri is required", 400);
        }

        var application = await _context.OauthApplications
            .Where(application => application.ClientId == clientId)
            .Where(application => !application.IsArchived)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            throw new OauthException("invalid_client", $"OAuth application with ClientId '{clientId}' not found or has been archived.", 404);
        }

        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var expiresAt = nowWithoutTz.AddSeconds(ExpiresInSeconds);
        var deviceCode = GenerateDeviceCode();
        var userCode = await GenerateUniqueUserCode();
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim();

        _context.OauthDeviceAuthorizationRequests.Add(new OauthDeviceAuthorizationRequest
        {
            DeviceCodeHash = HashCode(deviceCode),
            UserCodeHash = HashCode(NormalizeUserCode(userCode)),
            ApplicationId = application.Id,
            Scope = normalizedScope,
            Status = OauthDeviceAuthorizationStatus.Pending,
            PollingIntervalSeconds = DefaultPollingIntervalSeconds,
            PollCount = 0,
            CreatedAt = nowWithoutTz,
            ExpiresAt = expiresAt
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created OAuth device authorization request for client {ClientId} expiring at {ExpiresAt}",
            clientId,
            expiresAt);

        return new DeviceAuthorizationResponseDto
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            VerificationUri = verificationUri,
            VerificationUriComplete = BuildVerificationUriComplete(verificationUri, userCode),
            ExpiresIn = ExpiresInSeconds,
            Interval = DefaultPollingIntervalSeconds
        };
    }

    public async Task<OauthTokenGrantResponseDto> ExchangeDeviceCodeForToken(string? deviceCode, string? clientId)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new OauthException("invalid_request", "device_code is required", 400);
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new OauthException("invalid_request", "client_id is required", 400);
        }

        var application = await _context.OauthApplications
            .Where(application => application.ClientId == clientId)
            .Where(application => !application.IsArchived)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            throw new OauthException("invalid_client", $"OAuth application with ClientId '{clientId}' not found or has been archived.", 404);
        }

        var deviceCodeHash = HashCode(deviceCode);
        var request = await _context.OauthDeviceAuthorizationRequests
            .FirstOrDefaultAsync(request => request.DeviceCodeHash == deviceCodeHash
                                            && request.ApplicationId == application.Id);

        if (request == null)
        {
            throw new OauthException("invalid_grant", "invalid_grant", 400);
        }

        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var polledTooSoon = request.LastPolledAt.HasValue
                            && request.LastPolledAt.Value.AddSeconds(request.PollingIntervalSeconds) > nowWithoutTz;

        request.PollCount++;
        request.LastPolledAt = nowWithoutTz;

        if (request.ExpiresAt <= nowWithoutTz || request.Status == OauthDeviceAuthorizationStatus.Expired)
        {
            request.Status = OauthDeviceAuthorizationStatus.Expired;
            await _context.SaveChangesAsync();
            await CleanupExpiredOrConsumedRequests();
            throw new OauthException("expired_token", "expired_token", 400);
        }

        if (request.Status == OauthDeviceAuthorizationStatus.Pending && polledTooSoon)
        {
            request.PollingIntervalSeconds += SlowDownIntervalSeconds;
            await _context.SaveChangesAsync();
            throw new OauthException("slow_down", "slow_down", 400);
        }

        if (request.Status == OauthDeviceAuthorizationStatus.Pending)
        {
            await _context.SaveChangesAsync();
            throw new OauthException("authorization_pending", "authorization_pending", 400);
        }

        if (request.Status == OauthDeviceAuthorizationStatus.Denied)
        {
            await _context.SaveChangesAsync();
            throw new OauthException("access_denied", "access_denied", 400);
        }

        if (request.Status != OauthDeviceAuthorizationStatus.Approved || !request.UserId.HasValue)
        {
            await _context.SaveChangesAsync();
            throw new OauthException("invalid_grant", "invalid_grant", 400);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var consumedRequestCount = await _context.OauthDeviceAuthorizationRequests
            .Where(deviceRequest => deviceRequest.Id == request.Id)
            .Where(deviceRequest => deviceRequest.Status == OauthDeviceAuthorizationStatus.Approved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(deviceRequest => deviceRequest.Status, OauthDeviceAuthorizationStatus.Consumed)
                .SetProperty(deviceRequest => deviceRequest.ConsumedAt, nowWithoutTz));

        if (consumedRequestCount != 1)
        {
            throw new OauthException("invalid_grant", "invalid_grant", 400);
        }

        var tokenKeys = await _tokenBusiness.CreateApiKey(request.UserId.Value, clientId);
        var token = await _tokenBusiness.CreateToken(
            tokenKeys.apiKey,
            tokenKeys.apiSecret,
            AccessTokenExpirationMinutes);
        var refreshToken = GenerateRefreshToken();

        _context.OauthRefreshTokens.Add(new OauthRefreshToken
        {
            TokenHash = HashCode(refreshToken),
            ApplicationId = application.Id,
            UserId = request.UserId.Value,
            Scope = request.Scope,
            ExpiresAt = nowWithoutTz.AddDays(RefreshTokenExpirationDays),
            Revoked = false,
            CreatedAt = nowWithoutTz
        });

        await _context.SaveChangesAsync();
        await CleanupExpiredOrConsumedRequests();
        await transaction.CommitAsync();

        return new OauthTokenGrantResponseDto
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenExpirationMinutes * 60
        };
    }

    public async Task<OauthTokenGrantResponseDto> ExchangeRefreshTokenForToken(string? refreshToken, string? clientId)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new OauthException("invalid_request", "refresh_token is required", 400);
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new OauthException("invalid_request", "client_id is required", 400);
        }

        var application = await _context.OauthApplications
            .Where(application => application.ClientId == clientId)
            .Where(application => !application.IsArchived)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            throw new OauthException("invalid_client", $"OAuth application with ClientId '{clientId}' not found or has been archived.", 404);
        }

        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var refreshTokenHash = HashCode(refreshToken);
        var storedRefreshToken = await _context.OauthRefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == refreshTokenHash
                                          && token.ApplicationId == application.Id);

        if (storedRefreshToken == null || storedRefreshToken.Revoked)
        {
            throw new OauthException("invalid_grant", "invalid_grant", 400);
        }

        if (storedRefreshToken.ExpiresAt <= nowWithoutTz)
        {
            storedRefreshToken.Revoked = true;
            storedRefreshToken.RevokedAt = nowWithoutTz;
            await _context.SaveChangesAsync();
            await CleanupExpiredOrConsumedRequests();
            throw new OauthException("invalid_grant", "invalid_grant", 400);
        }

        var tokenKeys = await _tokenBusiness.CreateApiKey(storedRefreshToken.UserId, clientId);
        var token = await _tokenBusiness.CreateToken(
            tokenKeys.apiKey,
            tokenKeys.apiSecret,
            AccessTokenExpirationMinutes);

        storedRefreshToken.LastUsedAt = nowWithoutTz;
        await _context.SaveChangesAsync();
        await CleanupExpiredOrConsumedRequests();

        return new OauthTokenGrantResponseDto
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenExpirationMinutes * 60
        };
    }

    public async Task<DeviceVerificationLookupResponseDto> GetDeviceAuthorizationRequest(string? userCode)
    {
        var request = await GetDeviceAuthorizationRequestByUserCode(userCode);
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await MarkExpiredIfNeeded(request, nowWithoutTz);

        return ToVerificationLookupResponse(request, userCode!);
    }

    public async Task<DeviceVerificationLookupResponseDto> SetDeviceAuthorizationDecision(
        string? userCode,
        bool approve,
        long userId)
    {
        var request = await GetDeviceAuthorizationRequestByUserCode(userCode);
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var isExpired = await MarkExpiredIfNeeded(request, nowWithoutTz);

        if (isExpired)
        {
            throw new OauthException("invalid_request", "Device authorization request has expired", 400);
        }

        if (request.Status != OauthDeviceAuthorizationStatus.Pending)
        {
            throw new OauthException("invalid_request", $"Device authorization request is {request.Status}", 400);
        }

        request.UserId = userId;

        if (approve)
        {
            request.Status = OauthDeviceAuthorizationStatus.Approved;
            request.ApprovedAt = nowWithoutTz;
        }
        else
        {
            request.Status = OauthDeviceAuthorizationStatus.Denied;
            request.DeniedAt = nowWithoutTz;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "OAuth device authorization request {RequestId} set to {Status} by user {UserId}",
            request.Id,
            request.Status,
            userId);

        return ToVerificationLookupResponse(request, userCode!);
    }

    public async Task<int> CleanupExpiredOrConsumedRequests()
    {
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var requests = await _context.OauthDeviceAuthorizationRequests
            .Where(request => request.ExpiresAt <= nowWithoutTz
                              || request.Status == OauthDeviceAuthorizationStatus.Consumed)
            .ToListAsync();
        var refreshTokens = await _context.OauthRefreshTokens
            .Where(token => token.ExpiresAt <= nowWithoutTz || token.Revoked)
            .ToListAsync();

        if (requests.Count == 0 && refreshTokens.Count == 0)
        {
            return 0;
        }

        _context.OauthDeviceAuthorizationRequests.RemoveRange(requests);
        _context.OauthRefreshTokens.RemoveRange(refreshTokens);

        var deleted = await _context.SaveChangesAsync();
        _logger.LogInformation(
            "Cleaned up {RequestCount} OAuth device authorization requests and {RefreshTokenCount} refresh tokens",
            requests.Count,
            refreshTokens.Count);

        return deleted;
    }

    private async Task<OauthDeviceAuthorizationRequest> GetDeviceAuthorizationRequestByUserCode(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
        {
            throw new OauthException("invalid_request", "user_code is required", 400);
        }

        var userCodeHash = HashCode(NormalizeUserCode(userCode));
        var request = await _context.OauthDeviceAuthorizationRequests
            .Include(request => request.OauthApplication)
            .FirstOrDefaultAsync(request => request.UserCodeHash == userCodeHash
                                            && !request.OauthApplication.IsArchived);

        if (request == null)
        {
            throw new OauthException("invalid_request", "Device authorization request not found", 404);
        }

        return request;
    }

    private async Task<bool> MarkExpiredIfNeeded(OauthDeviceAuthorizationRequest request, DateTime nowWithoutTz)
    {
        if (request.Status == OauthDeviceAuthorizationStatus.Expired)
        {
            return true;
        }

        if (request.ExpiresAt > nowWithoutTz || request.Status == OauthDeviceAuthorizationStatus.Consumed)
        {
            return false;
        }

        request.Status = OauthDeviceAuthorizationStatus.Expired;
        await _context.SaveChangesAsync();
        await CleanupExpiredOrConsumedRequests();

        return true;
    }

    private DeviceVerificationLookupResponseDto ToVerificationLookupResponse(
        OauthDeviceAuthorizationRequest request,
        string userCode)
    {
        return new DeviceVerificationLookupResponseDto
        {
            UserCode = FormatUserCode(userCode),
            ClientId = request.OauthApplication.ClientId,
            ApplicationName = request.OauthApplication.Name,
            Scope = request.Scope,
            ExpiresAt = request.ExpiresAt,
            Status = request.Status
        };
    }

    private async Task<string> GenerateUniqueUserCode()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var userCode = GenerateUserCode();
            var userCodeHash = HashCode(NormalizeUserCode(userCode));
            var exists = await _context.OauthDeviceAuthorizationRequests
                .AnyAsync(request => request.UserCodeHash == userCodeHash);

            if (!exists)
            {
                return userCode;
            }
        }

        throw new OauthException("server_error", "Unable to generate a unique user code", 500);
    }

    private string GenerateDeviceCode()
    {
        return KeyGenerator.GenerateKeyBase64(DeviceCodeBytes);
    }

    private string GenerateRefreshToken()
    {
        return KeyGenerator.GenerateKeyBase64(RefreshTokenBytes);
    }

    private string GenerateUserCode()
    {
        var chars = new char[UserCodeLength];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = UserCodeAlphabet[RandomNumberGenerator.GetInt32(UserCodeAlphabet.Length)];
        }

        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
    }

    private string HashCode(string code)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(code));

        return Convert.ToBase64String(hashBytes);
    }

    private string NormalizeUserCode(string userCode)
    {
        return userCode
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Trim()
            .ToUpperInvariant();
    }

    private string FormatUserCode(string userCode)
    {
        var normalized = NormalizeUserCode(userCode);

        if (normalized.Length != UserCodeLength)
        {
            return normalized;
        }

        return $"{normalized.Substring(0, 4)}-{normalized.Substring(4, 4)}";
    }

    private string BuildVerificationUriComplete(string verificationUri, string userCode)
    {
        var separator = verificationUri.Contains('?') ? "&" : "?";

        return $"{verificationUri}{separator}user_code={Uri.EscapeDataString(userCode)}";
    }
}
