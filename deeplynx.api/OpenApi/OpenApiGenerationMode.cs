using System.Reflection;

namespace deeplynx.api.OpenApi;

internal static class OpenApiGenerationMode
{
    public static bool IsActive()
    {
        return string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.OrdinalIgnoreCase);
    }
}
