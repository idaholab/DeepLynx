using System.Net.Http.Headers;
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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _baseUrl;

    public AuthenticatedHttpClientFactory(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _baseUrl = EnvironmentHelper.GetRequiredEnvironmentVariable("NEXUS_API_URL").TrimEnd('/') + "/";
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

        var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return Task.FromResult(client);
    }
}