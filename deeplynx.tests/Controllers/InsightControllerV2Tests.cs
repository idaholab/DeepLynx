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
public class InsightControllerV2Tests : IDisposable
{
    private const long FileId = 3L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long RecordId = 4L;
    private const long UserId = 10L;
    private const string UserToken = "test-token";

    private readonly InsightController _controller;
    private readonly Mock<IInsightBusiness> _mockInsightBusiness;

    public InsightControllerV2Tests()
    {
        _mockInsightBusiness = new Mock<IInsightBusiness>();
        var mockLogger = new Mock<ILogger<InsightController>>();
        _controller = new InsightController(_mockInsightBusiness.Object, mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _controller.Response.Body = new MemoryStream();
        UserContextStorage.UserId = UserId;
        UserContextStorage.Token = UserToken;
    }

    public void Dispose()
    {
        _controller.Response.Body.Dispose();
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.Token = string.Empty;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    [Fact]
    public async Task UploadV2_ReturnsAcceptedAndPassesUserContext()
    {
        var dto = new InsightUploadApiRequestDto();
        _mockInsightBusiness
            .Setup(b => b.QueueInsightUpload(UserId, OrgId, ProjectId, 11L, 12L, dto, UserToken))
            .Returns(Task.CompletedTask);

        var result = await _controller.UploadV2(OrgId, ProjectId, 11L, 12L, dto);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Null(accepted.Value);
        _mockInsightBusiness.Verify(
            b => b.QueueInsightUpload(UserId, OrgId, ProjectId, 11L, 12L, dto, UserToken),
            Times.Once);
    }

    [Fact]
    public async Task QueryV2_StreamsAllChunks()
    {
        var dto = new InsightQueryApiRequestDto { Question = "question" };
        _mockInsightBusiness
            .Setup(b => b.StreamInsightQuery(
                UserId,
                OrgId,
                ProjectId,
                null,
                null,
                dto,
                It.IsAny<CancellationToken>()))
            .Returns(StreamChunks("first", " second"));

        await _controller.QueryV2(
            OrgId, ProjectId, null, null, dto, CancellationToken.None);

        _controller.Response.Body.Position = 0;
        using var reader = new StreamReader(_controller.Response.Body, leaveOpen: true);
        Assert.Equal("first second", await reader.ReadToEndAsync());
        Assert.Equal("text/plain; charset=utf-8", _controller.Response.ContentType);
    }

    [Fact]
    public async Task IngestionStatusV2_ReturnsStatus()
    {
        var expected = new InsightIngestionStatusResponseDto { FileId = FileId, Indexed = true };
        _mockInsightBusiness
            .Setup(b => b.FetchInsightIngestionStatus(FileId))
            .ReturnsAsync(expected);

        var actionResult = await _controller.IngestionStatusV2(OrgId, ProjectId, FileId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task PipelineStatusV2_ReturnsStatusAndPassesUserContext()
    {
        var expected = new InsightPipelineStatusResponseDto { RecordId = RecordId };
        _mockInsightBusiness
            .Setup(b => b.FetchInsightPipelineStatus(UserId, OrgId, ProjectId, RecordId))
            .ReturnsAsync(expected);

        var actionResult = await _controller.PipelineStatusV2(OrgId, ProjectId, RecordId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task EndpointHealthV2_ReturnsHealthResult()
    {
        var dto = new InsightEndpointHealthApiRequestDto
        {
            ModelConfigId = 22L,
            ModelType = "llm"
        };
        var expected = new InsightEndpointHealthResponseDto { Reachable = true };
        _mockInsightBusiness
            .Setup(b => b.CheckEndpointHealth(UserId, OrgId, ProjectId, 22L, "llm"))
            .ReturnsAsync(expected);

        var actionResult = await _controller.EndpointHealthV2(OrgId, ProjectId, dto);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task EmbedStringsV2_ReturnsAcceptedAndPassesUserContext()
    {
        _mockInsightBusiness
            .Setup(b => b.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 22L))
            .Returns(Task.CompletedTask);

        var result = await _controller.EmbedStringsV2(OrgId, ProjectId, 22L);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Null(accepted.Value);
        _mockInsightBusiness.Verify(
            b => b.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 22L),
            Times.Once);
    }

    [Fact]
    public async Task UploadV2_PropagatesInsightServiceException()
    {
        var dto = new InsightUploadApiRequestDto();
        _mockInsightBusiness
            .Setup(b => b.QueueInsightUpload(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<long?>(),
                dto,
                It.IsAny<string?>()))
            .ThrowsAsync(new InsightServiceException("Insight unavailable"));

        await Assert.ThrowsAsync<InsightServiceException>(
            () => _controller.UploadV2(OrgId, ProjectId, null, null, dto));
    }

    [Fact]
    public async Task PipelineStatusV2_PropagatesUnauthorizedAccessException()
    {
        _mockInsightBusiness
            .Setup(b => b.FetchInsightPipelineStatus(UserId, OrgId, ProjectId, RecordId))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.PipelineStatusV2(OrgId, ProjectId, RecordId));
    }

    [Fact]
    public void Controller_DeclaresV1AndV2()
    {
        AssertControllerVersions(typeof(InsightController));
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
        yield return [nameof(InsightController.Upload), nameof(InsightController.UploadV2), nameof(HttpPostAttribute)];
        yield return [nameof(InsightController.Query), nameof(InsightController.QueryV2), nameof(HttpPostAttribute)];
        yield return [nameof(InsightController.IngestionStatus), nameof(InsightController.IngestionStatusV2), nameof(HttpGetAttribute)];
        yield return [nameof(InsightController.PipelineStatus), nameof(InsightController.PipelineStatusV2), nameof(HttpGetAttribute)];
        yield return [nameof(InsightController.EndpointHealth), nameof(InsightController.EndpointHealthV2), nameof(HttpPostAttribute)];
        yield return [nameof(InsightController.EmbedStrings), nameof(InsightController.EmbedStringsV2), nameof(HttpPostAttribute)];
    }

    private static readonly HashSet<string> HttpAttributeNames =
    [
        nameof(HttpGetAttribute),
        nameof(HttpPostAttribute)
    ];

    private static readonly HashSet<string> SecurityAttributeNames =
    [
        "AuthAttribute",
        "InsightEnabledAttribute",
        "SensitivityAttribute"
    ];

    private static async IAsyncEnumerable<string> StreamChunks(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }

    private static MethodInfo GetControllerMethod(string methodName)
    {
        return Assert.Single(
            typeof(InsightController).GetMethods(),
            method => method.Name == methodName);
    }

    private static void AssertControllerVersions(Type controllerType)
    {
        var attributes = controllerType.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "ApiVersionAttribute")
            .ToArray();

        Assert.Equal(2, attributes.Length);
        Assert.Contains(attributes, attribute => GetVersion(attribute) == "1");
        Assert.Contains(attributes, attribute => GetVersion(attribute) == "2");
        Assert.DoesNotContain(attributes, attribute => attribute.NamedArguments.Any(argument =>
            argument.MemberName == "Deprecated" && Equals(argument.TypedValue.Value, true)));
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
