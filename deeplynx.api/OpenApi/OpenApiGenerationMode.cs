using System.Reflection;

namespace deeplynx.api.OpenApi;

internal static class OpenApiGenerationMode
{
    public static bool IsActive()
    {
        return string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Environment.GetEnvironmentVariable("NEXUS_OPENAPI_GENERATION"), "true", StringComparison.OrdinalIgnoreCase);
    }
}
