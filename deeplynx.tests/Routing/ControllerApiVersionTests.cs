using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V1;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.tests.Routing;

public class ControllerApiVersionTests
{
    [Theory]
    [InlineData("deeplynx.api.Controllers.V1", 1)]
    [InlineData("deeplynx.api.Controllers.V2", 2)]
    public void Controllers_DeclareOnlyTheirFolderVersion(string controllerNamespace, int expectedMajorVersion)
    {
        var controllerTypes = typeof(EdgeController).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.Namespace == controllerNamespace
                && typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(controllerTypes);

        foreach (var controllerType in controllerTypes)
        {
            var versionAttribute = Assert.Single(
                controllerType.GetCustomAttributes<ApiVersionAttribute>());
            var version = Assert.Single(versionAttribute.Versions);

            Assert.Equal(expectedMajorVersion, version.MajorVersion);
        }
    }
}
