using System.Net;
using System.Text;
using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

/// <summary>
///     Unit tests for <see cref="LatticeExtractionController"/>.
///     All business and insight dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage.UserId after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class LatticeExtractionControllerTests : IDisposable
{
    private readonly Mock<ILatticeExtractionBusiness> _mockBusiness;
    private readonly Mock<IInsightBusiness> _mockInsight;
    private readonly Mock<ILogger<LatticeExtractionController>> _mockLogger;
    private readonly LatticeExtractionController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long NotFoundId = 99L;

    public LatticeExtractionControllerTests()
    {
        _mockBusiness = new Mock<ILatticeExtractionBusiness>();
        _mockInsight = new Mock<IInsightBusiness>();
        _mockLogger = new Mock<ILogger<LatticeExtractionController>>();

        _controller = new LatticeExtractionController(
            _mockBusiness.Object,
            _mockInsight.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        // Reset to a safe sentinel so a mutated value never bleeds into another class's tests
        UserContextStorage.UserId = default;
    }

    // -------------------------------------------------------------------------
    // Helper — sets a JSON string as the controller's request body
    // -------------------------------------------------------------------------
    private void SetRequestBody(string json)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        ctx.Request.ContentType = "application/json";
        _controller.ControllerContext = new ControllerContext { HttpContext = ctx };
    }

    // =========================================================================
    // ListExtractions Tests
    // =========================================================================

    #region ListExtractions Tests

    [Fact]
    public async Task ListExtractions_Returns200_WithList()
    {
        var expected = new List<ExtractionListItemDto>
        {
            new() { Id = 1, Status = ExtractionStatus.Complete, Mode = ExtractionMode.Strict, CreatedBy = UserId }
        };
        _mockBusiness.Setup(b => b.ListExtractionsByUser(UserId, ProjectId))
                     .ReturnsAsync(expected);

        var result = await _controller.ListExtractions(OrgId, ProjectId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ListExtractions_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.ListExtractionsByUser(UserId, ProjectId))
                     .ReturnsAsync([]);

        var result = await _controller.ListExtractions(OrgId, ProjectId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<ExtractionListItemDto>>(result.Value);
    }

    [Fact]
    public async Task ListExtractions_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.ListExtractionsByUser(It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.ListExtractions(OrgId, ProjectId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    #endregion

    // =========================================================================
    // GetEmbeddingStatus Tests
    // =========================================================================

    #region GetEmbeddingStatus Tests

    [Fact]
    public async Task GetEmbeddingStatus_Returns200_WithStatus()
    {
        var expected = new EmbeddingStatusResponseDto
        {
            ClassCount = 2,
            EmbeddedClassCount = 2,
            RelationshipCount = 1,
            EmbeddedRelationshipCount = 1,
            OntologyReady = true
        };
        _mockBusiness.Setup(b => b.GetEmbeddingStatus(ProjectId)).ReturnsAsync(expected);

        var result = await _controller.GetEmbeddingStatus(OrgId, ProjectId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEmbeddingStatus_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetEmbeddingStatus(It.IsAny<long>()))
                     .ThrowsAsync(new Exception("unexpected"));

        var result = await _controller.GetEmbeddingStatus(OrgId, ProjectId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    #endregion

    // =========================================================================
    // EmbedOntology Tests
    // =========================================================================

    #region EmbedOntology Tests

    [Fact]
    public async Task EmbedOntology_Returns202_OnSuccess()
    {
        _mockInsight.Setup(i => i.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, null))
                    .Returns(Task.CompletedTask);

        var result = await _controller.EmbedOntology(OrgId, ProjectId) as AcceptedResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
    }

    [Fact]
    public async Task EmbedOntology_PassesOptionalModelConfigId()
    {
        _mockInsight.Setup(i => i.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 42L))
                    .Returns(Task.CompletedTask);

        var result = await _controller.EmbedOntology(OrgId, ProjectId, 42L) as AcceptedResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
        _mockInsight.Verify(i => i.QueueInsightEmbedStrings(UserId, OrgId, ProjectId, 42L), Times.Once);
    }

    [Fact]
    public async Task EmbedOntology_Returns400_OnSchemaNotReady()
    {
        _mockInsight.Setup(i => i.QueueInsightEmbedStrings(
                        It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>()))
                    .ThrowsAsync(new InvalidOperationException("Define at least one relationship before queueing data schema embeddings."));

        var result = await _controller.EmbedOntology(OrgId, ProjectId) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal(
            "ontology_schema_not_ready",
            result.Value.GetType().GetProperty("error")?.GetValue(result.Value));
        Assert.Equal(
            "Define at least one relationship before queueing data schema embeddings.",
            result.Value.GetType().GetProperty("message")?.GetValue(result.Value));
    }

    [Fact]
    public async Task EmbedOntology_Returns500_OnUnexpectedException()
    {
        _mockInsight.Setup(i => i.QueueInsightEmbedStrings(
                        It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>()))
                    .ThrowsAsync(new Exception("queue failure"));

        var result = await _controller.EmbedOntology(OrgId, ProjectId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    #endregion

    // =========================================================================
    // InsightExtractionFailure Tests
    // =========================================================================

    #region InsightExtractionFailure Tests

    [Fact]
    public async Task InsightExtractionFailure_Returns202_OnSuccess()
    {
        _mockBusiness.Setup(b => b.MarkExtractionFailed(5L, It.IsAny<string?>()))
                     .Returns(Task.CompletedTask);

        var result = await _controller.InsightExtractionFailure(OrgId, ProjectId, 5L) as AcceptedResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
    }

    [Fact]
    public async Task InsightExtractionFailure_Returns404_OnInvalidOperationException()
    {
        _mockBusiness.Setup(b => b.MarkExtractionFailed(NotFoundId, It.IsAny<string?>()))
                     .ThrowsAsync(new InvalidOperationException($"Extraction {NotFoundId} not found."));

        var result = await _controller.InsightExtractionFailure(OrgId, ProjectId, NotFoundId) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InsightExtractionFailure_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.MarkExtractionFailed(It.IsAny<long>(), It.IsAny<string?>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.InsightExtractionFailure(OrgId, ProjectId, 5L) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InsightExtractionFailure_PassesErrorMessageToBusinessLayer()
    {
        const string msg = "LLM timed out";
        _mockBusiness.Setup(b => b.MarkExtractionFailed(5L, msg)).Returns(Task.CompletedTask);

        await _controller.InsightExtractionFailure(OrgId, ProjectId, 5L, msg);

        _mockBusiness.Verify(b => b.MarkExtractionFailed(5L, msg), Times.Once);
    }

    [Fact]
    public async Task InsightExtractionFailure_ReadsPlainTextBody_WhenQueryMessageMissing()
    {
        const string msg = "Error: model or endpoint not found";
        SetRequestBody(msg);
        _controller.Request.ContentType = "text/plain";
        _mockBusiness.Setup(b => b.MarkExtractionFailed(5L, msg)).Returns(Task.CompletedTask);

        await _controller.InsightExtractionFailure(OrgId, ProjectId, 5L);

        _mockBusiness.Verify(b => b.MarkExtractionFailed(5L, msg), Times.Once);
    }

    [Fact]
    public async Task InsightExtractionFailure_ReadsJsonDetailBody_WhenQueryMessageMissing()
    {
        const string msg = "model does not exist";
        SetRequestBody("""{"error":"model_not_found","detail":"model does not exist"}""");
        _mockBusiness.Setup(b => b.MarkExtractionFailed(5L, msg)).Returns(Task.CompletedTask);

        await _controller.InsightExtractionFailure(OrgId, ProjectId, 5L);

        _mockBusiness.Verify(b => b.MarkExtractionFailed(5L, msg), Times.Once);
    }

    #endregion

    // =========================================================================
    // InsightExtractionCallback Tests
    // =========================================================================

    #region InsightExtractionCallback Tests

    [Fact]
    public async Task InsightExtractionCallback_Returns200_WithResult_OnValidDto()
    {
        var json = """
                   {
                     "classes": [{"class":"100th Wing","class_type":"Military Organization","confidence":0.9}],
                     "relationships": []
                   }
                   """;
        SetRequestBody(json);

        var expected = new ExtractionResponseDto { Id = 7, ClassCount = 1 };
        _mockBusiness.Setup(b => b.ProcessInsightExtractionCallback(
                         OrgId, ProjectId, 3L, 7L, It.IsAny<InsightExtractionCallbackDto>()))
                     .ReturnsAsync(expected);

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    /// <summary>
    ///     Verifies that LlmJsonParser correctly deserializes the callback body and the controller
    ///     forwards a DTO with the expected field values to the business layer.
    /// </summary>
    [Fact]
    public async Task InsightExtractionCallback_DeserializesDto_AndForwardsCorrectFields()
    {
        var json = """
                   {
                     "classes": [{"class":"100th Wing","class_type":"Military Organization","confidence":0.9}],
                     "relationships": [
                       {"subject":"100th Wing","subject_type":"Military Organization",
                        "relationship_type":"located at",
                        "object":"RAF Mildenhall","object_type":"Air Force Base","confidence":0.85}
                     ]
                   }
                   """;
        SetRequestBody(json);

        InsightExtractionCallbackDto? captured = null;
        _mockBusiness.Setup(b => b.ProcessInsightExtractionCallback(
                         OrgId, ProjectId, 3L, 7L, It.IsAny<InsightExtractionCallbackDto>()))
                     .Callback<long, long, long, long, InsightExtractionCallbackDto>(
                         (_, _, _, _, dto) => captured = dto)
                     .ReturnsAsync(new ExtractionResponseDto { Id = 7 });

        await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L);

        Assert.NotNull(captured);
        Assert.Single(captured.Classes);
        Assert.Equal("100th Wing", captured.Classes[0].Class);
        Assert.Equal("Military Organization", captured.Classes[0].ClassType);
        Assert.Equal(0.9, captured.Classes[0].Confidence, precision: 5);
        Assert.Single(captured.Relationships);
        Assert.Equal("located at", captured.Relationships[0].RelationshipType);
    }

    [Fact]
    public async Task InsightExtractionCallback_Returns400_OnMalformedJson()
    {
        SetRequestBody("{ this is not valid json }}}");

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InsightExtractionCallback_CallsMarkFailed_AndReturns400_WhenJsonParseFails()
    {
        SetRequestBody("{ broken }");
        _mockBusiness.Setup(b => b.MarkExtractionFailed(7L, It.IsAny<string>()))
                     .Returns(Task.CompletedTask);

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        _mockBusiness.Verify(b => b.MarkExtractionFailed(7L, It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    ///     If MarkExtractionFailed itself throws during the JSON-parse failure path, the controller's
    ///     inner catch logs and continues — 400 must still be returned to the caller.
    /// </summary>
    [Fact]
    public async Task InsightExtractionCallback_Returns400_EvenWhenMarkFailedThrows_OnJsonParseFailure()
    {
        SetRequestBody("{ broken }");
        _mockBusiness.Setup(b => b.MarkExtractionFailed(7L, It.IsAny<string>()))
                     .ThrowsAsync(new Exception("db unavailable"));

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task InsightExtractionCallback_Returns404_OnInvalidOperationException()
    {
        SetRequestBody("""{"classes":[],"relationships":[]}""");
        _mockBusiness.Setup(b => b.ProcessInsightExtractionCallback(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<InsightExtractionCallbackDto>()))
                     .ThrowsAsync(new InvalidOperationException("Extraction not found."));

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InsightExtractionCallback_Returns500_OnUnexpectedException()
    {
        SetRequestBody("""{"classes":[],"relationships":[]}""");
        _mockBusiness.Setup(b => b.ProcessInsightExtractionCallback(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<InsightExtractionCallbackDto>()))
                     .ThrowsAsync(new Exception("unexpected"));

        var result = await _controller.InsightExtractionCallback(OrgId, ProjectId, 7L, 3L) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    #endregion

    // =========================================================================
    // GetExtractionStaging Tests
    // =========================================================================

    #region GetExtractionStaging Tests

    [Fact]
    public async Task GetExtractionStaging_Returns200_WithStagingData()
    {
        var expected = new ExtractionStagingResponseDto
        {
            Id = 7,
            Status = ExtractionStatus.Complete,
            Mode = ExtractionMode.Strict,
            Classes = [new StagedClassDto { Id = 1, Name = "Military Organization" }],
            Records = [new StagedRecordDto { Id = 1, Name = "100th Wing" }],
            Relationships = [new StagedRelationshipDto { Id = 1, Name = "located at" }],
            Edges = [new StagedEdgeDto { Id = 1, OriginRecordName = "100th Wing", DestinationRecordName = "RAF Mildenhall" }]
        };
        _mockBusiness.Setup(b => b.GetExtractionStaging(7L)).ReturnsAsync(expected);

        var result = await _controller.GetExtractionStaging(OrgId, ProjectId, 7L) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetExtractionStaging_Returns404_OnInvalidOperationException()
    {
        _mockBusiness.Setup(b => b.GetExtractionStaging(NotFoundId))
                     .ThrowsAsync(new InvalidOperationException($"Extraction {NotFoundId} not found."));

        var result = await _controller.GetExtractionStaging(OrgId, ProjectId, NotFoundId) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetExtractionStaging_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetExtractionStaging(It.IsAny<long>()))
                     .ThrowsAsync(new Exception("unexpected"));

        var result = await _controller.GetExtractionStaging(OrgId, ProjectId, 7L) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    #endregion

    // =========================================================================
    // PromoteExtraction Tests
    // =========================================================================

    #region PromoteExtraction Tests

    [Fact]
    public async Task PromoteExtraction_Approve_Returns200_WithResult()
    {
        var expected = new ExtractionResponseDto { Id = 7, ClassCount = 2, RecordCount = 2, EdgeCount = 1 };
        _mockBusiness.Setup(b => b.PromoteExtraction(UserId, OrgId, ProjectId, 7L, true))
                     .ReturnsAsync(expected);

        var result = await _controller.PromoteExtraction(OrgId, ProjectId, 7L, true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task PromoteExtraction_Reject_Returns200()
    {
        _mockBusiness.Setup(b => b.PromoteExtraction(UserId, OrgId, ProjectId, 7L, false))
                     .ReturnsAsync(new ExtractionResponseDto { Id = 7 });

        var result = await _controller.PromoteExtraction(OrgId, ProjectId, 7L, false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task PromoteExtraction_Returns400_OnInvalidOperationException()
    {
        _mockBusiness.Setup(b => b.PromoteExtraction(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new InvalidOperationException("Cannot promote — status is running."));

        var result = await _controller.PromoteExtraction(OrgId, ProjectId, 7L, true) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task PromoteExtraction_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.PromoteExtraction(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.PromoteExtraction(OrgId, ProjectId, 7L, true) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task PromoteExtraction_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 42L;
        _mockBusiness.Setup(b => b.PromoteExtraction(42L, OrgId, ProjectId, 7L, true))
                     .ReturnsAsync(new ExtractionResponseDto { Id = 7 });

        await _controller.PromoteExtraction(OrgId, ProjectId, 7L, true);

        _mockBusiness.Verify(b => b.PromoteExtraction(42L, OrgId, ProjectId, 7L, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // TriggerExtraction Tests
    // =========================================================================

    #region TriggerExtraction Tests

    [Fact]
    public async Task TriggerExtraction_Returns202_WithExtractionId()
    {
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(UserId, OrgId, ProjectId, 5L, ExtractionMode.Strict))
                     .ReturnsAsync(99L);

        var result = await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Strict) as AcceptedResult;

        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
        Assert.NotNull(result.Value);
        var idProp = result.Value.GetType().GetProperty("extraction_id");
        Assert.NotNull(idProp);
        Assert.Equal(99L, idProp.GetValue(result.Value));
    }

    [Fact]
    public async Task TriggerExtraction_Returns409_WithStructuredError_WhenEmbeddingsNotReady()
    {
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                     .ThrowsAsync(new InvalidOperationException("Embeddings are being generated."));

        var result = await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Strict) as ConflictObjectResult;

        Assert.NotNull(result);
        Assert.Equal(409, result.StatusCode);
        Assert.NotNull(result.Value);
        var errorProp = result.Value.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal("embeddings_not_ready", errorProp.GetValue(result.Value));
    }

    [Fact]
    public async Task TriggerExtraction_PreservesInsightFailureStatusCode()
    {
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                     .ThrowsAsync(new InsightServiceException(
                         "Insight /lattice_query failed with 424 Failed Dependency.",
                         HttpStatusCode.FailedDependency,
                         """{"detail":"model missing"}"""));

        var result = await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Strict) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(424, result.StatusCode);
        Assert.NotNull(result.Value);

        var errorProp = result.Value.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal("lattice_trigger_failed", errorProp.GetValue(result.Value));
    }

    [Fact]
    public async Task TriggerExtraction_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                     .ThrowsAsync(new Exception("network error"));

        var result = await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Strict) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task TriggerExtraction_PassesCorrectModeToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(UserId, OrgId, ProjectId, 5L, ExtractionMode.Discovery))
                     .ReturnsAsync(1L);

        await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Discovery);

        _mockBusiness.Verify(
            b => b.TriggerLatticeExtraction(UserId, OrgId, ProjectId, 5L, ExtractionMode.Discovery),
            Times.Once);
    }

    [Fact]
    public async Task TriggerExtraction_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;
        _mockBusiness.Setup(b => b.TriggerLatticeExtraction(77L, OrgId, ProjectId, 5L, ExtractionMode.Strict))
                     .ReturnsAsync(1L);

        await _controller.TriggerExtraction(OrgId, ProjectId, 5L, ExtractionMode.Strict);

        _mockBusiness.Verify(
            b => b.TriggerLatticeExtraction(77L, OrgId, ProjectId, 5L, ExtractionMode.Strict),
            Times.Once);
    }

    #endregion
}
