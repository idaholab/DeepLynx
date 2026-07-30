using System.Net.Http.Headers;
using deeplynx.mcp;
using Microsoft.AspNetCore.Http;

namespace deeplynx.mcp.helpers;

/// <summary>
/// Factory for creating HttpClient instances with automatic JWT authentication.
/// Extracts API key/secret from incoming MCP request headers and obtains a valid token.
/// </summary>
public interface IAuthenticatedHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient with a valid Bearer token attached.
    /// Token is automatically obtained/refreshed using the API key and secret
    /// from the incoming request headers.
    /// </summary>
    Task<HttpClient> CreateClientAsync();
}

public class AuthenticatedHttpClientFactory : IAuthenticatedHttpClientFactory
{
    private const string VersionedApiPath = "api/" + NexusApiVersions.Target + "/";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Uri _baseAddress;
    private readonly HttpMessageHandler? _httpMessageHandler;

    public AuthenticatedHttpClientFactory(IHttpContextAccessor httpContextAccessor)
        : this(
            httpContextAccessor,
            EnvironmentHelper.GetRequiredEnvironmentVariable("NEXUS_API_URL"))
    {
    }

    public AuthenticatedHttpClientFactory(
        IHttpContextAccessor httpContextAccessor,
        string nexusApiUrl,
        HttpMessageHandler? httpMessageHandler = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _baseAddress = BuildBaseAddress(nexusApiUrl);
        _httpMessageHandler = httpMessageHandler;
    }

    /// <summary>
    /// Appends the MCP server's reviewed API-version pin to the configured Nexus
    /// origin. A legacy trailing /api/v1 is accepted, while another embedded
    /// version is rejected to prevent an environment-only contract cutover.
    /// </summary>
    public static Uri BuildBaseAddress(string nexusApiUrl)
    {
        var configuredUrl = nexusApiUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            || (configuredUri.Scheme != Uri.UriSchemeHttp && configuredUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "NEXUS_API_URL must be an absolute http(s) URL.");
        }

        var path = configuredUri.AbsolutePath.TrimEnd('/');
        var versionMarker = path.LastIndexOf("/api/v", StringComparison.OrdinalIgnoreCase);
        if (versionMarker >= 0 && versionMarker + 1 < path.Length)
        {
            var embeddedApiPath = path[(versionMarker + 1)..];
            if (!embeddedApiPath.Equals(
                    VersionedApiPath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"NEXUS_API_URL embeds '{embeddedApiPath}'; deeplynx.mcp is pinned to {NexusApiVersions.Target}.");
            }

            return new Uri(configuredUrl + "/");
        }

        return new Uri($"{configuredUrl}/{VersionedApiPath}");
    }

    public Task<HttpClient> CreateClientAsync()
    {
        var context = _httpContextAccessor.HttpContext 
            ?? throw new InvalidOperationException("No HTTP context available");

        // Get Bearer token from Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader))
            throw new UnauthorizedAccessException("Missing Authorization header. Send: Authorization: Bearer <your-token>");
        
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid auth format. Expected: Bearer <your-token>");

        var token = authHeader.Substring(7).Trim(); // 7 = "Bearer ".Length
        
        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException("Empty Bearer token");

        var client = _httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(_httpMessageHandler, disposeHandler: false);
        client.BaseAddress = _baseAddress;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return Task.FromResult(client);
    }
}
