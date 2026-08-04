using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V2;
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

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class UserModelTokenControllerTests : IDisposable
{
    private const long UserId = 10L;
    private const long UserModelTokenId = 20L;
    private const long AiModelConfigId = 30L;

    private readonly Mock<IUserModelTokenBusiness> _mockBusiness = new();
    private readonly UserModelTokenController _controller;

    public UserModelTokenControllerTests()
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
    public async Task GetUserTokens_ReturnsTokensAndForwardsFilter()
    {
        var expected = new List<UserModelTokenResponseDto> { new() };
        _mockBusiness
            .Setup(business => business.GetUserTokens(UserId, AiModelConfigId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetUserTokens(AiModelConfigId)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetUserTokens(UserId, AiModelConfigId),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenById_ReturnsTokenAndUsesCurrentUser()
    {
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.GetTokenById(UserId, UserModelTokenId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetTokenById(UserModelTokenId)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.GetTokenById(UserId, UserModelTokenId),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserModelToken_ReturnsTokenAndUsesCurrentUser()
    {
        var request = new CreateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.CreateUserModelToken(UserId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateUserModelToken(request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.CreateUserModelToken(UserId, request),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserModelToken_ReturnsTokenAndForwardsArguments()
    {
        var request = new UpdateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();
        _mockBusiness
            .Setup(business => business.UpdateUserModelToken(UserId, UserModelTokenId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateUserModelToken(
            UserModelTokenId,
            request)).Result;

        AssertOkObject(result, expected);
        _mockBusiness.Verify(
            business => business.UpdateUserModelToken(UserId, UserModelTokenId, request),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUserModelToken_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockBusiness
            .Setup(business => business.DeleteUserModelToken(UserId, UserModelTokenId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteUserModelToken(UserModelTokenId);

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
    public async Task GetTokenById_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new UnauthorizedAccessException("access denied");
        _mockBusiness
            .Setup(business => business.GetTokenById(UserId, UserModelTokenId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.GetTokenById(UserModelTokenId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DeleteUserModelToken_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("token not found");
        _mockBusiness
            .Setup(business => business.DeleteUserModelToken(UserId, UserModelTokenId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.DeleteUserModelToken(UserModelTokenId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(UserModelTokenController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("model-tokens", route.Template);
        var version = Assert.Single(versions);
        Assert.False(version.Deprecated);
        Assert.Equal(2, Assert.Single(version.Versions).MajorVersion);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Contains(controller.GetCustomAttributes<TagsAttribute>(), tags =>
            tags.Tags.Contains("User Model Token"));
    }

    public static TheoryData<string> VersionedActionPairs => new()
    {
        { nameof(UserModelTokenController.GetUserTokens) },
        { nameof(UserModelTokenController.GetTokenById) },
        { nameof(UserModelTokenController.CreateUserModelToken) },
        { nameof(UserModelTokenController.UpdateUserModelToken) },
        { nameof(UserModelTokenController.DeleteUserModelToken) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void Version2Actions_HaveExpectedMetadata(string methodName)
    {
        var method = GetAction(methodName);

        Assert.DoesNotContain(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.NotNull(method.GetCustomAttribute<BadgeAttribute>());
        Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(
            typeof(UserModelTokenController).GetMethods(),
            method => method.Name == name);
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
