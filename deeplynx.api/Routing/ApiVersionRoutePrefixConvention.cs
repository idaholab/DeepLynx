using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.api.Routing;

internal sealed class ApiVersionRoutePrefixConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _routePrefix;

    public ApiVersionRoutePrefixConvention(string routePrefix)
    {
        _routePrefix = new AttributeRouteModel(new RouteAttribute(routePrefix));
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var selector in controller.Selectors)
            {
                if (selector.AttributeRouteModel is null)
                {
                    selector.AttributeRouteModel = new AttributeRouteModel(_routePrefix);
                    continue;
                }

                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(
                    _routePrefix,
                    NormalizeRouteTemplate(selector.AttributeRouteModel));
            }

            foreach (var action in controller.Actions)
            {
                foreach (var selector in action.Selectors)
                {
                    if (selector.AttributeRouteModel?.Template is null ||
                        !AttributeRouteModel.IsOverridePattern(selector.AttributeRouteModel.Template))
                        continue;

                    selector.AttributeRouteModel = PrefixOverrideRouteTemplate(selector.AttributeRouteModel);
                }
            }
        }
    }

    private static AttributeRouteModel NormalizeRouteTemplate(AttributeRouteModel route)
    {
        if (route.Template is null || !AttributeRouteModel.IsOverridePattern(route.Template))
            return route;

        var normalized = new AttributeRouteModel(route)
        {
            Template = route.Template.TrimStart('~', '/')
        };

        return normalized;
    }

    private AttributeRouteModel PrefixOverrideRouteTemplate(AttributeRouteModel route)
    {
        var normalizedTemplate = route.Template?.TrimStart('~', '/');
        var routePrefix = _routePrefix.Template?.TrimEnd('/');

        return new AttributeRouteModel(route)
        {
            Template = $"~/{routePrefix}/{normalizedTemplate}"
        };
    }
}
