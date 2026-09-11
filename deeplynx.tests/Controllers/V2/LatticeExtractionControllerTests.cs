using System.Reflection;
using System.Text;
using System.Text.Json;
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
public class LatticeExtractionControllerTests : IDisposable
{
    private const long DataSourceId = 3L;
    private const long ExtractionId = 7L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long RecordId = 5L;
    private const long UserId = 10L;

    private readonly LatticeExtractionController _controller;
    private readonly Mock<ILatticeExtractionBusiness> _mockBusiness;
    private readonly Mock<IInsightBusiness> _mockInsightBusiness;

    public LatticeExtractionControllerTests()
    {
        _mockBusiness = new Mock<ILatticeExtractionBusiness>();
        _mockInsightBusiness = new Mock<IInsightBusiness>();
        var mockLogger = new Mock<ILogger<LatticeExtractionController>>();
        _controller = new LatticeExtractionController(
            _mockBusiness.Object,
            _mockInsightBusiness.Object,
            mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        _controller.Request.Body.Dispose();
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    [Fact]
    public async Task ListExtractions_ReturnsProjectExtractions()
    {
        var expected = new PaginatedResponse<ExtractionListItemDto>
        {
            Items = [new(), new()],
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };
        _mockBusiness
            .Setup(b => b.ListExtractionsByProjectPaginated(ProjectId, It.IsAny<PaginatedRequestDto>()))
            .ReturnsAsync(expected);

        var actionResult = await _controller.ListExtractions(ProjectId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetEmbeddingStatus_ReturnsStatus()
    {
        var expected = new EmbeddingStatusResponseDto { OntologyReady = true };
        _mockBusiness
            .Setup(b => b.GetEmbeddingStatus(ProjectId))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetEmbeddingStatus(OrgId, ProjectId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task EmbedOntology_ReturnsAcceptedAndPassesCurrentUser()
    {
        _mockInsightBusiness
            .Setup(b => b.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 22L))
            .Returns(Task.CompletedTask);

        var actionResult = await _controller.EmbedOntology(OrgId, ProjectId, 22L);

        var accepted = Assert.IsType<AcceptedResult>(actionResult);
        Assert.Null(accepted.Value);
        _mockInsightBusiness.Verify(
            b => b.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 22L),
            Times.Once);
    }

    [Fact]
    public async Task InsightExtractionFailure_ReturnsAcceptedAndPassesMessage()
    {
        const string errorMessage = "model timed out";
        _mockBusiness
            .Setup(b => b.MarkExtractionFailed(ExtractionId, OrgId, ProjectId, errorMessage))
            .Returns(Task.CompletedTask);

        var actionResult = await _controller.InsightExtractionFailure(
            OrgId, ProjectId, ExtractionId, errorMessage);

        var accepted = Assert.IsType<AcceptedResult>(actionResult);
        Assert.Null(accepted.Value);
        _mockBusiness.Verify(
            b => b.MarkExtractionFailed(ExtractionId, OrgId, ProjectId, errorMessage),
            Times.Once);
    }

    [Fact]
    public async Task InsightExtractionCallback_ReturnsProcessedExtraction()
    {
        SetRequestBody("""{"classes":[],"relationships":[]}""");
        var expected = new ExtractionResponseDto { Id = ExtractionId };
        _mockBusiness
            .Setup(b => b.ProcessInsightCallback(
                OrgId,
                ProjectId,
                DataSourceId,
                ExtractionId,
                It.IsAny<InsightExtractionCallbackDto>()))
            .ReturnsAsync(expected);

        var actionResult = await _controller.InsightExtractionCallback(
            OrgId, ProjectId, ExtractionId, DataSourceId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetExtractionStaging_ReturnsStagingData()
    {
        var expected = new ExtractionStagingResponseDto { Id = ExtractionId };
        _mockBusiness
            .Setup(b => b.GetExtractionStaging(ExtractionId, OrgId, ProjectId))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetExtractionStaging(
            OrgId, ProjectId, ExtractionId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task PromoteExtraction_ReturnsUpdatedExtractionAndPassesCurrentUser()
    {
        var request = new PromoteExtractionRequestDto();
        var expected = new ExtractionResponseDto { Id = ExtractionId };
        _mockBusiness
            .Setup(b => b.PromoteExtraction(UserId, OrgId, ProjectId, ExtractionId, request))
            .ReturnsAsync(expected);

        var actionResult = await _controller.PromoteExtraction(
            OrgId, ProjectId, ExtractionId, request);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task RejectExtraction_ReturnsUpdatedExtraction()
    {
        var request = new RejectExtractionRequestDto { RejectAllRemaining = true };
        var expected = new ExtractionResponseDto { Id = ExtractionId };
        _mockBusiness
            .Setup(b => b.RejectExtraction(ExtractionId, request))
            .ReturnsAsync(expected);

        var actionResult = await _controller.RejectExtraction(
            OrgId, ProjectId, ExtractionId, request);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task TriggerExtraction_ReturnsAcceptedAndPassesCurrentUser()
    {
        _mockBusiness
            .Setup(b => b.TriggerLatticeExtraction(UserId, OrgId, ProjectId, RecordId, "strict"))
            .ReturnsAsync(ExtractionId);

        var actionResult = await _controller.TriggerExtraction(
            OrgId, ProjectId, RecordId, "strict");

        var result = Assert.IsType<AcceptedResult>(actionResult);
        Assert.Equal(ExtractionId, Assert.IsType<long>(result.Value));
    }

    [Fact]
    public async Task ListExtractions_PropagatesUnexpectedException()
    {
        _mockBusiness
            .Setup(b => b.ListExtractionsByProjectPaginated(ProjectId, It.IsAny<PaginatedRequestDto>()))
            .ThrowsAsync(new Exception("database unavailable"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.ListExtractions(ProjectId));
    }

    [Fact]
    public async Task InsightExtractionCallback_PropagatesJsonException()
    {
        SetRequestBody("{ broken }");

        await Assert.ThrowsAsync<JsonException>(
            () => _controller.InsightExtractionCallback(
                OrgId, ProjectId, ExtractionId, DataSourceId));
        _mockBusiness.Verify(
            b => b.MarkExtractionFailed(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void Controller_DeclaresOnlyVersion2()
    {
        var attributes = typeof(LatticeExtractionController).GetCustomAttributesData()
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
        yield return [nameof(LatticeExtractionController.ListExtractions), nameof(HttpGetAttribute)];
        yield return [nameof(LatticeExtractionController.GetEmbeddingStatus), nameof(HttpGetAttribute)];
        yield return [nameof(LatticeExtractionController.EmbedOntology), nameof(HttpPostAttribute)];
        yield return [nameof(LatticeExtractionController.InsightExtractionFailure), nameof(HttpPostAttribute)];
        yield return [nameof(LatticeExtractionController.InsightExtractionCallback), nameof(HttpPostAttribute)];
        yield return [nameof(LatticeExtractionController.GetExtractionStaging), nameof(HttpGetAttribute)];
        yield return [nameof(LatticeExtractionController.PromoteExtraction), nameof(HttpPostAttribute)];
        yield return [nameof(LatticeExtractionController.RejectExtraction), nameof(HttpPostAttribute)];
        yield return [nameof(LatticeExtractionController.TriggerExtraction), nameof(HttpPostAttribute)];
    }

    private static readonly HashSet<string> HttpAttributeNames =
    [
        nameof(HttpGetAttribute),
        nameof(HttpPostAttribute)
    ];

    private static readonly HashSet<string> SecurityAttributeNames =
    [
        "AllowAnonymousAttribute",
        "InsightEnabledAttribute"
    ];

    private void SetRequestBody(string body)
    {
        _controller.Request.Body.Dispose();
        _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        _controller.Request.ContentType = "application/json";
    }

    private static MethodInfo GetControllerMethod(string methodName)
    {
        return Assert.Single(
            typeof(LatticeExtractionController).GetMethods(),
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
