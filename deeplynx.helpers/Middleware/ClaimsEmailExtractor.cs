using System.Security.Claims;

namespace deeplynx.helpers;

public static class ClaimsEmailExtractor
{
    public static string? ExtractEmail(ClaimsPrincipal principal)
    {
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

        foreach (var (_, raw) in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var candidate = raw.Trim().ToLowerInvariant();

            // Let's treat these as the same
            if (candidate.EndsWith("@azuregov.inl.gov", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Replace("@azuregov.inl.gov", "@inl.gov");

            // sanity: must contain '@' and '.' in domain
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