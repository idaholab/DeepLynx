using System.Reflection;
using deeplynx.api.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.tests.Routing;

public class ApiVersionRoutePrefixConventionTests
{
    [Theory]
    [InlineData("organizations", "api/v{version:apiVersion}/organizations")]
    [InlineData(
        "organizations/{organizationId:long}/projects",
        "api/v{version:apiVersion}/organizations/{organizationId:long}/projects")]
    [InlineData(
        "organizations/{organizationId:long}/projects/{projectId:long}/records",
        "api/v{version:apiVersion}/organizations/{organizationId:long}/projects/{projectId:long}/records")]
    [InlineData(
        "/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger",
        "api/v{version:apiVersion}/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger")]
    [InlineData(
        "~/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/record-collections",
        "api/v{version:apiVersion}/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/record-collections")]
    public void Apply_PrependsApiVersionPrefix_ToExistingControllerRoutes(
        string controllerRoute,
        string expectedRoute)
    {
        var application = CreateApplicationModel(controllerRoute);

        var convention = new ApiVersionRoutePrefixConvention("api/v{version:apiVersion}");
        convention.Apply(application);

        var routeTemplate = application.Controllers
            .Single()
            .Selectors
            .Single()
            .AttributeRouteModel?
            .Template;

        Assert.Equal(expectedRoute, routeTemplate);
    }

    [Fact]
    public void Apply_AddsApiVersionPrefix_WhenControllerHasNoAttributeRoute()
    {
        var application = CreateApplicationModel(routeTemplate: null);

        var convention = new ApiVersionRoutePrefixConvention("api/v{version:apiVersion}");
        convention.Apply(application);

        var routeTemplate = application.Controllers
            .Single()
            .Selectors
            .Single()
            .AttributeRouteModel?
            .Template;

        Assert.Equal("api/v{version:apiVersion}", routeTemplate);
    }

    [Theory]
    [InlineData(
        "/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger",
        "~/api/v{version:apiVersion}/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger")]
    [InlineData(
        "~/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/record-collections",
        "~/api/v{version:apiVersion}/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/record-collections")]
    public void Apply_PrependsApiVersionPrefix_ToAbsoluteActionRoutes(
        string actionRoute,
        string expectedRoute)
    {
        var application = CreateApplicationModel(
            "organizations/{organizationId:long}/projects/{projectId:long}/records",
            actionRoute);

        var convention = new ApiVersionRoutePrefixConvention("api/v{version:apiVersion}");
        convention.Apply(application);

        var routeTemplate = application.Controllers
            .Single()
            .Actions
            .Single()
            .Selectors
            .Single()
            .AttributeRouteModel?
            .Template;

        Assert.Equal(expectedRoute, routeTemplate);
    }

    private static ApplicationModel CreateApplicationModel(string? routeTemplate, string? actionRouteTemplate = null)
    {
        var controller = new ControllerModel(
            typeof(TestController).GetTypeInfo(),
            attributes: [])
        {
            ControllerName = nameof(TestController)
        };

        var selector = new SelectorModel
        {
            AttributeRouteModel = routeTemplate is null
                ? null
                : new AttributeRouteModel(new RouteAttribute(routeTemplate))
        };

        controller.Selectors.Add(selector);

        if (actionRouteTemplate is not null)
        {
            var action = new ActionModel(
                typeof(TestController).GetMethod(nameof(TestController.TestAction))!,
                attributes: [])
            {
                ActionName = nameof(TestController.TestAction)
            };

            action.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(actionRouteTemplate))
            });

            controller.Actions.Add(action);
        }

        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        return application;
    }

    private sealed class TestController
    {
        public void TestAction()
        {
        }
    }
}
