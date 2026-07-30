using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class OauthApplicationControllerV2Tests : IDisposable
{
    private const long CurrentUserId = 10L;
    private const long ApplicationId = 20L;

    private readonly Mock<IOauthApplicationBusiness> _mockBusiness = new();
    private readonly OauthApplicationController _controller;

    public OauthApplicationControllerV2Tests()
    {
        _controller = new OauthApplicationController(
            _mockBusiness.Object,
            Mock.Of<ILogger<OauthApplicationController>>());

        UserContextStorage.UserId = CurrentUserId;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    [Fact]
    public async Task GetAllOauthApplicationsV2_ReturnsApplicationsAndForwardsFilter()
    {
        var expected = new List<OauthApplicationResponseDto>
        {
            CreateApplicationResponse()
        };
        _mockBusiness
            .Setup(business => business.GetAllOauthApplications(false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllOauthApplicationsV2(hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetAllOauthApplications(false),
            Times.Once);
    }

    [Fact]
    public async Task GetOauthApplicationV2_ReturnsApplicationAndForwardsFilter()
    {
        var expected = CreateApplicationResponse();
        _mockBusiness
            .Setup(business => business.GetOauthApplication(ApplicationId, false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetOauthApplicationV2(
            ApplicationId,
            hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetOauthApplication(ApplicationId, false),
            Times.Once);
    }

    [Fact]
    public async Task CreateOauthApplicationV2_ReturnsSecureResponseAndUsesCurrentUser()
    {
        var request = new CreateOauthApplicationRequestDto
        {
            Name = "Application",
            CallbackUrl = "https://example.test/callback"
        };
        var expected = new OauthApplicationSecureResponseDto
        {
            Name = request.Name,
            ClientId = "client-id",
            ClientSecretRaw = "one-time-client-secret"
        };
        _mockBusiness
            .Setup(business => business.CreateOauthApplication(request, CurrentUserId))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateOauthApplicationV2(request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.CreateOauthApplication(request, CurrentUserId),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOauthApplicationV2_ReturnsApplicationAndUsesCurrentUser()
    {
        var request = new UpdateOauthApplicationRequestDto { Name = "Updated Application" };
        var expected = CreateApplicationResponse(request.Name);
        _mockBusiness
            .Setup(business => business.UpdateOauthApplication(
                ApplicationId,
                request,
                CurrentUserId))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateOauthApplicationV2(ApplicationId, request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.UpdateOauthApplication(ApplicationId, request, CurrentUserId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteOauthApplicationV2_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockBusiness
            .Setup(business => business.DeleteOauthApplication(ApplicationId, CurrentUserId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteOauthApplicationV2(ApplicationId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockBusiness.Verify(
            business => business.DeleteOauthApplication(ApplicationId, CurrentUserId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveOauthApplicationV2_WhenArchiveIsTrue_ReturnsBooleanAndArchives()
    {
        _mockBusiness
            .Setup(business => business.ArchiveOauthApplication(ApplicationId, CurrentUserId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveOauthApplicationV2(ApplicationId, archive: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockBusiness.Verify(
            business => business.ArchiveOauthApplication(ApplicationId, CurrentUserId),
            Times.Once);
        _mockBusiness.Verify(
            business => business.UnarchiveOauthApplication(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveOauthApplicationV2_WhenArchiveIsFalse_ReturnsBooleanAndUnarchives()
    {
        _mockBusiness
            .Setup(business => business.UnarchiveOauthApplication(ApplicationId, CurrentUserId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveOauthApplicationV2(ApplicationId, archive: false);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockBusiness.Verify(
            business => business.UnarchiveOauthApplication(ApplicationId, CurrentUserId),
            Times.Once);
        _mockBusiness.Verify(
            business => business.ArchiveOauthApplication(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task V1Action_StillConvertsBusinessExceptionToLegacy500Response()
    {
        _mockBusiness
            .Setup(business => business.GetOauthApplication(ApplicationId, true))
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var result = (await _controller.GetOauthApplication(ApplicationId)).Result;

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.IsType<string>(error.Value);
    }

    [Fact]
    public async Task V2ReadAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockBusiness
            .Setup(business => business.GetOauthApplication(ApplicationId, true))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.GetOauthApplicationV2(ApplicationId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task V2WriteAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var request = new CreateOauthApplicationRequestDto
        {
            Name = "Application",
            CallbackUrl = "https://example.test/callback"
        };
        var expected = new InvalidOperationException("database failure");
        _mockBusiness
            .Setup(business => business.CreateOauthApplication(request, CurrentUserId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.CreateOauthApplicationV2(request));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(OauthApplicationController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("oauth/applications", route.Template);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.False(version.Deprecated));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 1));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 2));
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<SysAdminAttribute>());
    }

    public static TheoryData<string, string> VersionedActionPairs => new()
    {
        {
            nameof(OauthApplicationController.GetAllOauthApplications),
            nameof(OauthApplicationController.GetAllOauthApplicationsV2)
        },
        {
            nameof(OauthApplicationController.GetOauthApplication),
            nameof(OauthApplicationController.GetOauthApplicationV2)
        },
        {
            nameof(OauthApplicationController.CreateOauthApplication),
            nameof(OauthApplicationController.CreateOauthApplicationV2)
        },
        {
            nameof(OauthApplicationController.UpdateOauthApplication),
            nameof(OauthApplicationController.UpdateOauthApplicationV2)
        },
        {
            nameof(OauthApplicationController.DeleteOauthApplication),
            nameof(OauthApplicationController.DeleteOauthApplicationV2)
        },
        {
            nameof(OauthApplicationController.ArchiveOauthApplication),
            nameof(OauthApplicationController.ArchiveOauthApplicationV2)
        }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void VersionedActions_PreserveRouteMetadata(string v1Name, string v2Name)
    {
        var v1 = GetAction(v1Name);
        var v2 = GetAction(v2Name);

        AssertMappedVersion(v1, 1D);
        AssertMappedVersion(v2, 2D);
        Assert.NotNull(v2.GetCustomAttribute<BadgeAttribute>());
        Assert.Equal(GetHttpMetadata(v1), GetHttpMetadata(v2));
    }

    private static OauthApplicationResponseDto CreateApplicationResponse(
        string name = "Application")
    {
        return new OauthApplicationResponseDto
        {
            Id = ApplicationId,
            ClientId = "client-id",
            Name = name,
            CallbackUrl = "https://example.test/callback"
        };
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(
            typeof(OauthApplicationController).GetMethods(),
            method => method.Name == name);
    }

    private static void AssertMappedVersion(MethodInfo method, double expectedVersion)
    {
        var attribute = Assert.Single(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.Equal(expectedVersion, Assert.IsType<double>(attribute.ConstructorArguments[0].Value));
    }

    private static (string Attribute, string? Template, string? Name) GetHttpMetadata(MethodInfo method)
    {
        var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        return (attribute.GetType().Name, attribute.Template, attribute.Name);
    }

    private static void AssertOkObject<T>(IActionResult? result, T expected)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }
}
