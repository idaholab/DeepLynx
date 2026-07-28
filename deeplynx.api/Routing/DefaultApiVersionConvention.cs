using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.api.Routing;

internal sealed class DefaultApiVersionConvention : IControllerConvention
{
    private readonly IReadOnlyCollection<ApiVersion> _supportedApiVersions;
    private readonly IReadOnlyCollection<ApiVersion> _deprecatedApiVersions;

    public DefaultApiVersionConvention(
        IReadOnlyCollection<ApiVersion> supportedApiVersions,
        IReadOnlyCollection<ApiVersion> deprecatedApiVersions)
    {
        ArgumentNullException.ThrowIfNull(supportedApiVersions);
        ArgumentNullException.ThrowIfNull(deprecatedApiVersions);

        if (supportedApiVersions.Count == 0 && deprecatedApiVersions.Count == 0)
            throw new ArgumentException("At least one API version must be configured.");

        _supportedApiVersions = supportedApiVersions;
        _deprecatedApiVersions = deprecatedApiVersions;
    }

    public bool Apply(IControllerConventionBuilder builder, ControllerModel controller)
    {
        if (builder is not ControllerApiVersionConventionBuilder controllerBuilder)
            return false;

        foreach (var apiVersion in _supportedApiVersions)
            controllerBuilder.HasApiVersion(apiVersion);

        foreach (var apiVersion in _deprecatedApiVersions)
            controllerBuilder.HasDeprecatedApiVersion(apiVersion);

        return true;
    }
}
