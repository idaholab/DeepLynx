using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class UserModelTokenControllerV2Tests : IDisposable
{
    private const long UserId = 10L;
    private const long UserModelTokenId = 20L;
    private const long AiModelConfigId = 30L;

    private readonly Mock<IUserModelTokenBusiness> _mockBusiness = new();
    private readonly UserModelTokenController _controller;

    public UserModelTokenControllerV2Tests()
    {
        _controller = new UserModelTokenController(
            _mockBusiness.Object,
            Mock.Of<ILogger<UserModelTokenController>>());

        UserContextStorage.UserId = UserId;
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
    public async Task GetUserTokensV2_ReturnsTokensAndForwardsFilter()
    {
        var expected = new List<UserModelTokenResponseDto> { new() };
        _mockBusiness
            .Setup(business => business.GetUserTokens(UserId, AiModelConfigId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetUserTokensV2(AiModelConfigId)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetUserTokens(UserId, AiModelConfigId),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenByIdV2_ReturnsTokenAndUsesCurrentUser()
    {
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.GetTokenById(UserId, UserModelTokenId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetTokenByIdV2(UserModelTokenId)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetTokenById(UserId, UserModelTokenId),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserModelTokenV2_ReturnsTokenAndUsesCurrentUser()
    {
        var request = new CreateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.CreateUserModelToken(UserId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateUserModelTokenV2(request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.CreateUserModelToken(UserId, request),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserModelTokenV2_ReturnsTokenAndForwardsArguments()
    {
        var request = new UpdateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.UpdateUserModelToken(UserId, UserModelTokenId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateUserModelTokenV2(
            UserModelTokenId,
            request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.UpdateUserModelToken(UserId, UserModelTokenId, request),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUserModelTokenV2_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockBusiness
            .Setup(business => business.DeleteUserModelToken(UserId, UserModelTokenId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteUserModelTokenV2(UserModelTokenId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(true, ok.Value);
        _mockBusiness.Verify(
            business => business.DeleteUserModelToken(UserId, UserModelTokenId),
            Times.Once);
    }

    [Fact]
    public async Task V1Action_StillReturnsLegacyForbiddenResponse()
    {
        _mockBusiness
            .Setup(business => business.GetTokenById(UserId, UserModelTokenId))
            .ThrowsAsync(new UnauthorizedAccessException("access denied"));

        var result = (await _controller.GetTokenById(UserModelTokenId)).Result;

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetTokenByIdV2_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new UnauthorizedAccessException("access denied");
        _mockBusiness
            .Setup(business => business.GetTokenById(UserId, UserModelTokenId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.GetTokenByIdV2(UserModelTokenId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DeleteUserModelTokenV2_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("token not found");
        _mockBusiness
            .Setup(business => business.DeleteUserModelToken(UserId, UserModelTokenId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.DeleteUserModelTokenV2(UserModelTokenId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(UserModelTokenController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("model-tokens", route.Template);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.False(version.Deprecated));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 1));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 2));
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Contains(controller.GetCustomAttributes<TagsAttribute>(), tags =>
            tags.Tags.Contains("User Model Token"));
    }

    public static TheoryData<string, string> VersionedActionPairs => new()
    {
        {
            nameof(UserModelTokenController.GetUserTokens),
            nameof(UserModelTokenController.GetUserTokensV2)
        },
        {
            nameof(UserModelTokenController.GetTokenById),
            nameof(UserModelTokenController.GetTokenByIdV2)
        },
        {
            nameof(UserModelTokenController.CreateUserModelToken),
            nameof(UserModelTokenController.CreateUserModelTokenV2)
        },
        {
            nameof(UserModelTokenController.UpdateUserModelToken),
            nameof(UserModelTokenController.UpdateUserModelTokenV2)
        },
        {
            nameof(UserModelTokenController.DeleteUserModelToken),
            nameof(UserModelTokenController.DeleteUserModelTokenV2)
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

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(
            typeof(UserModelTokenController).GetMethods(),
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
