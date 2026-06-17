using System.Security.Cryptography;
using System.Text;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
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
    private const string UserCodeAlphabet = "BCDFGHJKLMNPQRSTVWXZ";

    private readonly DeeplynxContext _context;
    private readonly ILogger<OauthDeviceAuthorizationBusiness> _logger;

    public OauthDeviceAuthorizationBusiness(
        DeeplynxContext context,
        ILogger<OauthDeviceAuthorizationBusiness> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DeviceAuthorizationResponseDto> CreateDeviceAuthorizationRequest(
        string? clientId,
        string? scope,
        string verificationUri)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("client_id is required");
        }

        if (string.IsNullOrWhiteSpace(verificationUri))
        {
            throw new ArgumentException("verification_uri is required");
        }

        var application = await _context.OauthApplications
            .Where(application => application.ClientId == clientId)
            .Where(application => !application.IsArchived)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            throw new KeyNotFoundException($"OAuth application with ClientId '{clientId}' not found or has been archived.");
        }

        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var expiresAt = nowWithoutTz.AddSeconds(ExpiresInSeconds);
        var deviceCode = await GenerateUniqueDeviceCode();
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

    public async Task<int> CleanupExpiredOrConsumedRequests()
    {
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var requests = await _context.OauthDeviceAuthorizationRequests
            .Where(request => request.ExpiresAt <= nowWithoutTz
                              || request.Status == OauthDeviceAuthorizationStatus.Consumed)
            .ToListAsync();

        if (requests.Count == 0)
        {
            return 0;
        }

        _context.OauthDeviceAuthorizationRequests.RemoveRange(requests);

        var deleted = await _context.SaveChangesAsync();
        _logger.LogInformation("Cleaned up {RequestCount} OAuth device authorization requests", requests.Count);

        return deleted;
    }

    private async Task<string> GenerateUniqueDeviceCode()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var deviceCode = GenerateDeviceCode();
            var deviceCodeHash = HashCode(deviceCode);
            var exists = await _context.OauthDeviceAuthorizationRequests
                .AnyAsync(request => request.DeviceCodeHash == deviceCodeHash);

            if (!exists)
            {
                return deviceCode;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique device code");
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

        throw new InvalidOperationException("Unable to generate a unique user code");
    }

    private string GenerateDeviceCode()
    {
        return KeyGenerator.GenerateKeyBase64(DeviceCodeBytes);
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

    private string BuildVerificationUriComplete(string verificationUri, string userCode)
    {
        var separator = verificationUri.Contains('?') ? "&" : "?";

        return $"{verificationUri}{separator}user_code={Uri.EscapeDataString(userCode)}";
    }
}
