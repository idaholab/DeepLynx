using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class DeviceAuthorizationRequestDto
{
    [Required]
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = null!;

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

public class DeviceAuthorizationResponseDto
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = null!;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = null!;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = null!;

    [JsonPropertyName("verification_uri_complete")]
    public string VerificationUriComplete { get; set; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

public class DeviceCodeTokenRequestDto
{
    [Required]
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = null!;

    [Required]
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = null!;

    [Required]
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = null!;
}

public class DeviceVerificationLookupResponseDto
{
    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = null!;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = null!;

    [JsonPropertyName("application_name")]
    public string ApplicationName { get; set; } = null!;

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;
}

public class DeviceAuthorizationDecisionRequestDto
{
    [Required]
    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = null!;

    [JsonPropertyName("approve")]
    public bool Approve { get; set; }
}

public class RefreshTokenGrantRequestDto
{
    [Required]
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = null!;

    [Required]
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    [Required]
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = null!;
}

public class OauthTokenGrantResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class OauthErrorResponseDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = null!;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
