using System.Reflection;
using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class AiModelConfigControllerV2Tests : IDisposable
{
    private const long AiModelConfigId = 19L;
    private const long OrgId = 1L;
    private const long UserId = 10L;
    private const string ModelType = "llm";

    private readonly AiModelConfigController _controller;
    private readonly Mock<IAiModelConfigBusiness> _mockAiModelConfigBusiness;

    public AiModelConfigControllerV2Tests()
    {
        _mockAiModelConfigBusiness = new Mock<IAiModelConfigBusiness>();
        var mockLogger = new Mock<ILogger<AiModelConfigController>>();
        _controller = new AiModelConfigController(_mockAiModelConfigBusiness.Object, mockLogger.Object);
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
    public async Task GetAllAiModelConfigsV2_ReturnsConfigsAndPassesArguments()
    {
        var expected = new List<AiModelConfigResponseDto> { new() { Id = AiModelConfigId } };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, null, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllAiModelConfigsV2(OrgId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.GetAllAiModelConfigs(OrgId, null, false),
            Times.Once);
    }

    [Fact]
    public async Task GetAiModelConfigV2_ReturnsConfigAndPassesArguments()
    {
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(OrgId, null, AiModelConfigId, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAiModelConfigV2(OrgId, AiModelConfigId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultAiModelConfigV2_ReturnsDefaultConfig()
    {
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(OrgId, null, ModelType))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetDefaultAiModelConfigV2(OrgId, ModelType);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task CreateAiModelConfigV2_ReturnsCreatedConfigAndPassesCurrentUser()
    {
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "http://localhost",
            ModelType = ModelType,
            ModelProvider = "local",
            ModelName = "test-model"
        };
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.CreateAiModelConfig(UserId, OrgId, null, dto))
            .ReturnsAsync(expected);

        var actionResult = await _controller.CreateAiModelConfigV2(OrgId, dto);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task UpdateAiModelConfigV2_ReturnsUpdatedConfigAndPassesCurrentUser()
    {
        var dto = new UpdateAiModelConfigDto { ModelName = "updated-model" };
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(UserId, OrgId, null, AiModelConfigId, dto))
            .ReturnsAsync(expected);

        var actionResult = await _controller.UpdateAiModelConfigV2(OrgId, AiModelConfigId, dto);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task ArchiveAiModelConfigV2_ArchivesAndReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.ArchiveAiModelConfig(UserId, OrgId, null, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveAiModelConfigV2(OrgId, AiModelConfigId, true);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.UnarchiveAiModelConfig(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveAiModelConfigV2_UnarchivesAndReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.UnarchiveAiModelConfig(UserId, OrgId, null, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveAiModelConfigV2(OrgId, AiModelConfigId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.ArchiveAiModelConfig(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAiModelConfigV2_ReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.DeleteAiModelConfig(OrgId, null, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.DeleteAiModelConfigV2(OrgId, AiModelConfigId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task GetAiModelConfigV2_PropagatesNotFoundException()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(OrgId, null, AiModelConfigId, true))
            .ThrowsAsync(new KeyNotFoundException("config not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.GetAiModelConfigV2(OrgId, AiModelConfigId));
    }

    [Fact]
    public async Task UpdateAiModelConfigV2_PropagatesInvalidOperationException()
    {
        var dto = new UpdateAiModelConfigDto();
        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(UserId, OrgId, null, AiModelConfigId, dto))
            .ThrowsAsync(new InvalidOperationException("config cannot be updated"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.UpdateAiModelConfigV2(OrgId, AiModelConfigId, dto));
    }

    [Fact]
    public void Controller_DeclaresV1AndV2()
    {
        var attributes = typeof(AiModelConfigController).GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "ApiVersionAttribute")
            .ToArray();

        Assert.Equal(2, attributes.Length);
        Assert.Contains(attributes, attribute => GetVersion(attribute) == "1");
        Assert.Contains(attributes, attribute => GetVersion(attribute) == "2");
        Assert.DoesNotContain(attributes, attribute => attribute.NamedArguments.Any(argument =>
            argument.MemberName == "Deprecated" && Equals(argument.TypedValue.Value, true)));
    }

    [Theory]
    [MemberData(nameof(ActionMetadata))]
    public void VersionedActions_HaveMatchingRoutesAndSecurityAttributes(
        string v1MethodName,
        string v2MethodName,
        string expectedHttpAttribute)
    {
        var v1Method = GetControllerMethod(v1MethodName);
        var v2Method = GetControllerMethod(v2MethodName);

        AssertMappedToVersion(v1Method, "1");
        AssertMappedToVersion(v2Method, "2");
        AssertHasAttribute(v2Method, "BadgeAttribute");
        AssertHasAttribute(v2Method, expectedHttpAttribute);
        Assert.Equal(
            GetAttributeSignatures(v1Method, HttpAttributeNames),
            GetAttributeSignatures(v2Method, HttpAttributeNames));
        Assert.Equal(
            GetAttributeSignatures(v1Method, SecurityAttributeNames),
            GetAttributeSignatures(v2Method, SecurityAttributeNames));
    }

    public static IEnumerable<object[]> ActionMetadata()
    {
        yield return [nameof(AiModelConfigController.GetAllAiModelConfigs), nameof(AiModelConfigController.GetAllAiModelConfigsV2), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigController.GetAiModelConfig), nameof(AiModelConfigController.GetAiModelConfigV2), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigController.GetDefaultAiModelConfig), nameof(AiModelConfigController.GetDefaultAiModelConfigV2), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigController.CreateAiModelConfig), nameof(AiModelConfigController.CreateAiModelConfigV2), nameof(HttpPostAttribute)];
        yield return [nameof(AiModelConfigController.UpdateAiModelConfig), nameof(AiModelConfigController.UpdateAiModelConfigV2), nameof(HttpPutAttribute)];
        yield return [nameof(AiModelConfigController.ArchiveAiModelConfig), nameof(AiModelConfigController.ArchiveAiModelConfigV2), nameof(HttpPatchAttribute)];
        yield return [nameof(AiModelConfigController.DeleteAiModelConfig), nameof(AiModelConfigController.DeleteAiModelConfigV2), nameof(HttpDeleteAttribute)];
    }

    private static readonly HashSet<string> HttpAttributeNames =
    [
        nameof(HttpDeleteAttribute),
        nameof(HttpGetAttribute),
        nameof(HttpPatchAttribute),
        nameof(HttpPostAttribute),
        nameof(HttpPutAttribute)
    ];

    private static readonly HashSet<string> SecurityAttributeNames = ["OrgAdminAttribute"];

    private static MethodInfo GetControllerMethod(string methodName)
    {
        return Assert.Single(
            typeof(AiModelConfigController).GetMethods(),
            method => method.Name == methodName);
    }

    private static void AssertMappedToVersion(MethodInfo method, string expectedVersion)
    {
        var attribute = Assert.Single(
            method.GetCustomAttributesData(),
            candidate => candidate.AttributeType.Name == "MapToApiVersionAttribute");
        Assert.Equal(expectedVersion, GetVersion(attribute));
    }

    private static string? GetVersion(CustomAttributeData attribute)
    {
        return attribute.ConstructorArguments[0].Value?.ToString();
    }

    private static void AssertHasAttribute(MethodInfo method, string attributeName)
    {
        Assert.Contains(
            method.GetCustomAttributesData(),
            attribute => attribute.AttributeType.Name == attributeName);
    }

    private static string[] GetAttributeSignatures(
        MethodInfo method,
        IReadOnlySet<string> includedAttributeNames)
    {
        return method.GetCustomAttributesData()
            .Where(attribute => includedAttributeNames.Contains(attribute.AttributeType.Name))
            .Select(attribute =>
                $"{attribute.AttributeType.Name}:" +
                string.Join(",", attribute.ConstructorArguments.Select(argument => argument.Value?.ToString())) +
                ":" +
                string.Join(",", attribute.NamedArguments.Select(argument =>
                    $"{argument.MemberName}={argument.TypedValue.Value}")))
            .OrderBy(signature => signature)
            .ToArray();
    }
}
