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
        var convention = new DefaultApiVersionConvention(new ApiVersion(1));

        var applied = convention.Apply(builder, controller);

        Assert.True(applied);
    }

    private sealed class TestController
    {
    }
}
