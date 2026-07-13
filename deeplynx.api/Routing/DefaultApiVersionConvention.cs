using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.api.Routing;

internal sealed class DefaultApiVersionConvention : IControllerConvention
{
    private readonly ApiVersion _apiVersion;

    public DefaultApiVersionConvention(ApiVersion apiVersion)
    {
        _apiVersion = apiVersion;
    }

    public bool Apply(IControllerConventionBuilder builder, ControllerModel controller)
    {
        if (builder is not ControllerApiVersionConventionBuilder controllerBuilder)
            return false;

        controllerBuilder.HasApiVersion(_apiVersion);
        return true;
    }
}
