using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using deeplynx.api.Routing;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace deeplynx.tests.Routing;

public class DefaultApiVersionConventionTests
{
    [Fact]
    public void Apply_ReturnsTrue_ForControllerApiVersionConventionBuilder()
    {
        var controller = new ControllerModel(
            typeof(TestController).GetTypeInfo(),
            attributes: [])
        {
            ControllerName = nameof(TestController)
        };
        var builder = new ControllerApiVersionConventionBuilder(typeof(TestController));
        var convention = new DefaultApiVersionConvention(
            supportedApiVersions: [new ApiVersion(2)],
            deprecatedApiVersions: [new ApiVersion(1)]);

        var applied = convention.Apply(builder, controller);

        Assert.True(applied);
    }

    [Fact]
    public void Constructor_Throws_WhenNoApiVersionsAreConfigured()
    {
        Assert.Throws<ArgumentException>(() => new DefaultApiVersionConvention([], []));
    }

    private sealed class TestController
    {
    }
}
