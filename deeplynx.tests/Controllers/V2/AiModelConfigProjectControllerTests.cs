using System.Reflection;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class AiModelConfigProjectControllerTests : IDisposable
{
    private const long AiModelConfigId = 19L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const string ModelType = "llm";

    private readonly AiModelConfigProjectController _controller;
    private readonly Mock<IAiModelConfigBusiness> _mockAiModelConfigBusiness;

    public AiModelConfigProjectControllerTests()
    {
        _mockAiModelConfigBusiness = new Mock<IAiModelConfigBusiness>();
        var mockLogger = new Mock<ILogger<AiModelConfigProjectController>>();
        _controller = new AiModelConfigProjectController(
            _mockAiModelConfigBusiness.Object,
            mockLogger.Object);
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
    public async Task GetAllAiModelConfigs_ReturnsConfigsAndPassesProjectScope()
    {
        var expected = new List<AiModelConfigResponseDto> { new() { Id = AiModelConfigId } };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, ProjectId, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllAiModelConfigs(OrgId, ProjectId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.GetAllAiModelConfigs(OrgId, ProjectId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetAiModelConfig_ReturnsConfigAndPassesProjectScope()
    {
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(OrgId, ProjectId, AiModelConfigId, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAiModelConfig(
            OrgId, ProjectId, AiModelConfigId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_ReturnsDefaultConfig()
    {
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(OrgId, ProjectId, ModelType))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetDefaultAiModelConfig(OrgId, ProjectId, ModelType);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task CreateAiModelConfig_ReturnsCreatedConfigAndPassesCurrentUser()
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
            .Setup(b => b.CreateAiModelConfig(UserId, OrgId, ProjectId, dto))
            .ReturnsAsync(expected);

        var actionResult = await _controller.CreateAiModelConfig(OrgId, ProjectId, dto);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task UpdateAiModelConfig_ReturnsUpdatedConfigAndPassesCurrentUser()
    {
        var dto = new UpdateAiModelConfigDto { ModelName = "updated-model" };
        var expected = new AiModelConfigResponseDto { Id = AiModelConfigId };
        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(UserId, OrgId, ProjectId, AiModelConfigId, dto))
            .ReturnsAsync(expected);

        var actionResult = await _controller.UpdateAiModelConfig(
            OrgId, ProjectId, AiModelConfigId, dto);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_ArchivesAndReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.ArchiveAiModelConfig(UserId, OrgId, ProjectId, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveAiModelConfig(
            OrgId, ProjectId, AiModelConfigId, true);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.UnarchiveAiModelConfig(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_UnarchivesAndReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.UnarchiveAiModelConfig(UserId, OrgId, ProjectId, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveAiModelConfig(
            OrgId, ProjectId, AiModelConfigId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockAiModelConfigBusiness.Verify(
            b => b.ArchiveAiModelConfig(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAiModelConfig_ReturnsBusinessResult()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.DeleteAiModelConfig(OrgId, ProjectId, AiModelConfigId))
            .ReturnsAsync(true);

        var actionResult = await _controller.DeleteAiModelConfig(
            OrgId, ProjectId, AiModelConfigId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task GetAiModelConfig_PropagatesNotFoundException()
    {
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(OrgId, ProjectId, AiModelConfigId, true))
            .ThrowsAsync(new KeyNotFoundException("config not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.GetAiModelConfig(OrgId, ProjectId, AiModelConfigId));
    }

    [Fact]
    public async Task UpdateAiModelConfig_PropagatesInvalidOperationException()
    {
        var dto = new UpdateAiModelConfigDto();
        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(UserId, OrgId, ProjectId, AiModelConfigId, dto))
            .ThrowsAsync(new InvalidOperationException("config cannot be updated"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.UpdateAiModelConfig(OrgId, ProjectId, AiModelConfigId, dto));
    }

    [Fact]
    public void Controller_DeclaresOnlyVersion2()
    {
        var attributes = typeof(AiModelConfigProjectController).GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "ApiVersionAttribute")
            .ToArray();

        var attribute = Assert.Single(attributes);
        Assert.Equal("2", GetVersion(attribute));
        Assert.DoesNotContain(attributes, attribute => attribute.NamedArguments.Any(argument =>
            argument.MemberName == "Deprecated" && Equals(argument.TypedValue.Value, true)));
    }

    [Theory]
    [MemberData(nameof(ActionMetadata))]
    public void VersionedActions_HaveMatchingRoutesAndSecurityAttributes(
        string methodName,
        string expectedHttpAttribute)
    {
        var v2Method = GetControllerMethod(methodName);

        Assert.DoesNotContain(v2Method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "MapToApiVersionAttribute");
        AssertHasAttribute(v2Method, "BadgeAttribute");
        AssertHasAttribute(v2Method, expectedHttpAttribute);
    }

    public static IEnumerable<object[]> ActionMetadata()
    {
        yield return [nameof(AiModelConfigProjectController.GetAllAiModelConfigs), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigProjectController.GetAiModelConfig), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigProjectController.GetDefaultAiModelConfig), nameof(HttpGetAttribute)];
        yield return [nameof(AiModelConfigProjectController.CreateAiModelConfig), nameof(HttpPostAttribute)];
        yield return [nameof(AiModelConfigProjectController.UpdateAiModelConfig), nameof(HttpPutAttribute)];
        yield return [nameof(AiModelConfigProjectController.ArchiveAiModelConfig), nameof(HttpPatchAttribute)];
        yield return [nameof(AiModelConfigProjectController.DeleteAiModelConfig), nameof(HttpDeleteAttribute)];
    }

    private static readonly HashSet<string> HttpAttributeNames =
    [
        nameof(HttpDeleteAttribute),
        nameof(HttpGetAttribute),
        nameof(HttpPatchAttribute),
        nameof(HttpPostAttribute),
        nameof(HttpPutAttribute)
    ];

    private static readonly HashSet<string> SecurityAttributeNames =
    [
        "AuthAttribute",
        "OrgAdminAttribute",
        "ProjectAdminAttribute",
        "SensitivityAttribute",
        "SysAdminAttribute"
    ];

    private static MethodInfo GetControllerMethod(string methodName)
    {
        return Assert.Single(
            typeof(AiModelConfigProjectController).GetMethods(),
            method => method.Name == methodName);
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
