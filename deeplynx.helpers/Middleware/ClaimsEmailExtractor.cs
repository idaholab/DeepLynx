using System.Security.Claims;
using System.Text.RegularExpressions;

namespace deeplynx.helpers;

public static class ClaimsEmailExtractor
{
    // Matches service_<guid> and test_<guid> identifiers stored in the email column
    private static readonly Regex NonEmailAccountPattern = new(
        @"^(service|test)_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractEmail(ClaimsPrincipal principal)
    {
        var disableAuth = Environment.GetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION");
        var candidates = new (string claimType, string? value)[]
        {
            // Tests expect this to win if it's an email-like value
            ("nameidentifier",
                principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value),

            // Then ClaimTypes.Email should win over "email"
            (ClaimTypes.Email, principal.FindFirst(ClaimTypes.Email)?.Value),
            ("email", principal.FindFirst("email")?.Value),

            // Other common options
            ("preferred_username", principal.FindFirst("preferred_username")?.Value),
            ("upn", principal.FindFirst("upn")?.Value),
            ("mail", principal.FindFirst("mail")?.Value),
            ("unique_name", principal.FindFirst("unique_name")?.Value),
            ("username", principal.FindFirst("username")?.Value),

            // Last resort: only if it actually looks like an email
            ("sub", principal.FindFirst("sub")?.Value),
            ("name", principal.FindFirst("name")?.Value)
        };

        if (disableAuth == "true")
        {
            // Prioritize actual email claims first
            var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
                             ?? principal.FindFirst("email")?.Value;

            if (!string.IsNullOrWhiteSpace(emailClaim))
                return emailClaim.Trim().ToLowerInvariant();

            // Then fall back to any non-empty claim
            foreach (var (_, raw) in candidates)
                if (!string.IsNullOrWhiteSpace(raw))
                    return raw.Trim().ToLowerInvariant();
            return null;
        }

        // Auth enabled - validate email format
        foreach (var (_, raw) in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var candidate = raw.Trim().ToLowerInvariant();

            // Service/test account identifiers are stored in the email column;
            // accept them as-is without email-format validation
            if (NonEmailAccountPattern.IsMatch(candidate))
                return candidate;

            // Normalize domain
            if (candidate.EndsWith("@azuregov.inl.gov", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Replace("@azuregov.inl.gov", "@inl.gov");

            // Validate email format
            var atIndex = candidate.IndexOf('@');
            if (atIndex <= 0) continue;

            var domainPart = candidate[(atIndex + 1)..];
            if (string.IsNullOrWhiteSpace(domainPart) || !domainPart.Contains('.')) continue;

            return candidate;
        }

        return null;
    }

    public static string? ExtractSsoId(ClaimsPrincipal principal)
    {
        // Keep identity separate from email
        return principal.FindFirst("oid")?.Value
               ?? principal.FindFirst("uid")?.Value
               ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? principal.FindFirst("sub")?.Value;
    }
}