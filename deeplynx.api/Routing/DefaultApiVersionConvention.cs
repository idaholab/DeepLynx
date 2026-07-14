using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.api.Routing;

internal sealed class DefaultApiVersionConvention : IControllerConvention
{
    private readonly IReadOnlyCollection<ApiVersion> _apiVersions;

    public DefaultApiVersionConvention(params ApiVersion[] apiVersions)
    {
        ArgumentNullException.ThrowIfNull(apiVersions);

        if (apiVersions.Length == 0)
            throw new ArgumentException("At least one API version must be configured.", nameof(apiVersions));

        _apiVersions = apiVersions;
    }

    public bool Apply(IControllerConventionBuilder builder, ControllerModel controller)
    {
        if (builder is not ControllerApiVersionConventionBuilder controllerBuilder)
            return false;

        foreach (var apiVersion in _apiVersions)
            controllerBuilder.HasApiVersion(apiVersion);

        return true;
    }
}
