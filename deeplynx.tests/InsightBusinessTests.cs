using System.Net;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class InsightBusinessTests : IntegrationTestBase
{
    private InsightBusiness _insightBusiness = null!;
    private InsightServiceClient _client = null!;
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<IAiModelConfigBusiness> _mockAiModelConfigBusiness = null!;
    private Mock<IProvenanceBusiness> _mockProvenanceBusiness = null!;
    private Mock<ISensitivityLabelService> _mockSensitivityLabelService = null!;
    private Mock<ILogger<InsightBusiness>> _mockLogger = null!;

    public long uid, oid, pid, dsid;
    public long recordId1, recordId2;
    public long vlmConfigId, embeddingConfigId, llmConfigId;

    public InsightBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        Environment.SetEnvironmentVariable("INSIGHT_FASTAPI_URL", "http://localhost:5000");
        _mockHandler = new Mock<HttpMessageHandler>();
        _client = new InsightServiceClient(new HttpClient(_mockHandler.Object));

        _mockAiModelConfigBusiness = new Mock<IAiModelConfigBusiness>();
        _mockProvenanceBusiness = new Mock<IProvenanceBusiness>();
        _mockSensitivityLabelService = new Mock<ISensitivityLabelService>();
        _mockLogger = new Mock<ILogger<InsightBusiness>>();

        _insightBusiness = new InsightBusiness(
            Context,
            _client,
            _mockAiModelConfigBusiness.Object,
            _mockProvenanceBusiness.Object,
            _mockLogger.Object,
            _mockSensitivityLabelService.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User { Name = "Insight User", Email = "insight@test.com", Password = "pw", IsArchived = false };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var org = new Organization { Name = "Insight Org", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var proj = new Project { Name = "Insight Project", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();
        pid = proj.Id;

        var ds = new DataSource { Name = "Insight DS", ProjectId = pid, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();
        dsid = ds.Id;

        var rec1 = new datalayer.Models.Record
        {
            Name = "Record 1",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = dsid,
            OriginalId = "rec-001",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/usr/src/app/org_1/rec1.pdf"
        };
        var rec2 = new datalayer.Models.Record
        {
            Name = "Record 2",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = dsid,
            OriginalId = "rec-002",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/usr/src/app/org_1/rec2.pdf"
        };
        Context.Records.AddRange(rec1, rec2);
        await Context.SaveChangesAsync();
        recordId1 = rec1.Id;
        recordId2 = rec2.Id;

        vlmConfigId = 10;
        embeddingConfigId = 20;
        llmConfigId = 30;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private AiModelConfigResponseDto.WithToken MakeConfig(long id, string modelType, bool requiresToken = false, string? token = null) =>
        new()
        {
            Id = id,
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = $"http://{modelType}.example.com",
            ModelProvider = "openai",
            ModelName = $"{modelType}-model",
            ModelType = modelType,
            RequiresToken = requiresToken,
            Token = token
        };

    /// <summary>
    ///     Configures the mocked HttpMessageHandler to respond successfully to any request
    ///     issued by the real InsightServiceClient, with the given JSON body.
    /// </summary>
    private void SetupHttpSuccess(string jsonBody = "{}")
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void SetupHttpFailure(HttpStatusCode code = HttpStatusCode.InternalServerError, string body = "boom")
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = code,
                Content = new StringContent(body)
            });
    }

    // =========================================================================
    // IsSupportedFile Tests
    // =========================================================================

    #region IsSupportedFile Tests

    [Theory]
    [InlineData("pdf")]
    [InlineData("PDF")]
    [InlineData("txt")]
    [InlineData("html")]
    [InlineData("htm")]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public void IsSupportedFile_ReturnsTrue_ForSupportedTypes(string fileType)
    {
        Assert.True(_insightBusiness.IsSupportedFile(fileType));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("csv")]
    [InlineData("")]
    [InlineData("exe")]
    public void IsSupportedFile_ReturnsFalse_ForUnsupportedTypes(string fileType)
    {
        Assert.False(_insightBusiness.IsSupportedFile(fileType));
    }

    #endregion

    // =========================================================================
    // ResolveModelConfig Tests
    // =========================================================================

    #region ResolveModelConfig Tests

    [Fact]
    public async Task ResolveModelConfig_UsesExplicitId_WhenProvided()
    {
        var expected = MakeConfig(vlmConfigId, "vlm");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId))
            .ReturnsAsync(expected);

        var result = await _insightBusiness.ResolveModelConfig(uid, oid, pid, vlmConfigId, "vlm");

        Assert.Equal(vlmConfigId, result.Id);
        _mockAiModelConfigBusiness.Verify(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId), Times.Once);
        _mockAiModelConfigBusiness.Verify(m => m.GetDefaultAiModelConfigWithToken(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolveModelConfig_FallsBackToDefault_WhenNoExplicitId()
    {
        var expected = MakeConfig(embeddingConfigId, "embedding");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ReturnsAsync(expected);

        var result = await _insightBusiness.ResolveModelConfig(uid, oid, pid, null, "embedding");

        Assert.Equal(embeddingConfigId, result.Id);
    }

    [Fact]
    public async Task ResolveModelConfig_Llm_FallsBackToVlmDefault_WhenNoLlmDefaultConfigured()
    {
        var vlmFallback = MakeConfig(vlmConfigId, "vlm");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "llm"))
            .ThrowsAsync(new KeyNotFoundException("no default llm"));
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "vlm"))
            .ReturnsAsync(vlmFallback);

        var result = await _insightBusiness.ResolveModelConfig(uid, oid, pid, null, "llm");

        Assert.Equal(vlmConfigId, result.Id);
    }

    [Fact]
    public async Task ResolveModelConfig_Throws_WhenTokenRequiredButMissing()
    {
        var configMissingToken = MakeConfig(vlmConfigId, "vlm", requiresToken: true, token: null);
        _mockAiModelConfigBusiness
            .Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId))
            .ReturnsAsync(configMissingToken);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.ResolveModelConfig(uid, oid, pid, vlmConfigId, "vlm"));

        Assert.Contains("requires an API token", ex.Message);
    }

    [Fact]
    public async Task ResolveModelConfig_Succeeds_WhenTokenRequiredAndPresent()
    {
        var configWithToken = MakeConfig(vlmConfigId, "vlm", requiresToken: true, token: "secret-token");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId))
            .ReturnsAsync(configWithToken);

        var result = await _insightBusiness.ResolveModelConfig(uid, oid, pid, vlmConfigId, "vlm");

        Assert.Equal("secret-token", result.Token);
    }

    [Fact]
    public async Task ResolveModelConfig_Propagates_KeyNotFound_WhenNoDefaultExists()
    {
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ThrowsAsync(new KeyNotFoundException("not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _insightBusiness.ResolveModelConfig(uid, oid, pid, null, "embedding"));
    }

    #endregion

    // =========================================================================
    // QueueInsightUpload Tests
    // =========================================================================

    #region QueueInsightUpload Tests

    private void SetupDefaultConfigsForUpload(out AiModelConfigResponseDto.WithToken vlmCfg, out AiModelConfigResponseDto.WithToken embCfg)
    {
        vlmCfg = MakeConfig(vlmConfigId, "vlm");
        embCfg = MakeConfig(embeddingConfigId, "embedding");
        _mockAiModelConfigBusiness.Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId)).ReturnsAsync(vlmCfg);
        _mockAiModelConfigBusiness.Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, embeddingConfigId)).ReturnsAsync(embCfg);
    }

    [Fact]
    public async Task QueueInsightUpload_Throws_WhenFileInfoEmpty()
    {
        SetupDefaultConfigsForUpload(out _, out _);
        var payload = new InsightUploadApiRequestDto { FileInfo = [] };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.QueueInsightUpload(uid, oid, pid, vlmConfigId, embeddingConfigId, payload));

        Assert.Contains("Select at least one document", ex.Message);
    }

    [Fact]
    public async Task QueueInsightUpload_Throws_WhenNoAuthorizedFiles()
    {
        SetupDefaultConfigsForUpload(out _, out _);
        _mockSensitivityLabelService
            .Setup(s => s.FilterAuthorizedRecordIds(uid, oid, pid, It.IsAny<List<long>>(), Context))
            .ReturnsAsync(new HashSet<long>());

        var payload = new InsightUploadApiRequestDto
        {
            FileInfo = [new() { FileId = recordId1, FileUri = "org_1/rec1.pdf" }]
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.QueueInsightUpload(uid, oid, pid, vlmConfigId, embeddingConfigId, payload));

        Assert.Contains("No authorized documents", ex.Message);
    }

    [Fact]
    public async Task QueueInsightUpload_FiltersToOnlyAuthorizedFiles()
    {
        SetupDefaultConfigsForUpload(out _, out _);
        SetupHttpSuccess();

        _mockSensitivityLabelService
            .Setup(s => s.FilterAuthorizedRecordIds(uid, oid, pid, It.Is<List<long>>(ids => ids.Contains(recordId1) && ids.Contains(recordId2)), Context))
            .ReturnsAsync(new HashSet<long> { recordId1 });

        var payload = new InsightUploadApiRequestDto
        {
            FileInfo =
            [
                new() { FileId = recordId1, FileUri = "org_1/rec1.pdf" },
                new() { FileId = recordId2, FileUri = "org_1/rec2.pdf" }
            ]
        };

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        await _insightBusiness.QueueInsightUpload(uid, oid, pid, vlmConfigId, embeddingConfigId, payload);

        Assert.NotNull(capturedRequest);
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("rec1.pdf", body);
        Assert.DoesNotContain("rec2.pdf", body);
    }

    [Fact]
    public async Task QueueInsightUpload_ResolvesDefaultConfigs_WhenIdsAreNull()
    {
        var vlmCfg = MakeConfig(vlmConfigId, "vlm");
        var embCfg = MakeConfig(embeddingConfigId, "embedding");
        _mockAiModelConfigBusiness.Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "vlm")).ReturnsAsync(vlmCfg);
        _mockAiModelConfigBusiness.Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding")).ReturnsAsync(embCfg);
        _mockSensitivityLabelService
            .Setup(s => s.FilterAuthorizedRecordIds(uid, oid, pid, It.IsAny<List<long>>(), Context))
            .ReturnsAsync(new HashSet<long> { recordId1 });
        SetupHttpSuccess();

        var payload = new InsightUploadApiRequestDto
        {
            FileInfo = [new() { FileId = recordId1, FileUri = "org_1/rec1.pdf" }]
        };

        await _insightBusiness.QueueInsightUpload(uid, oid, pid, null, null, payload);

        _mockAiModelConfigBusiness.Verify(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "vlm"), Times.Once);
        _mockAiModelConfigBusiness.Verify(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"), Times.Once);
    }

    [Fact]
    public async Task QueueInsightUpload_NeverSetsOverwrite()
    {
        SetupDefaultConfigsForUpload(out _, out _);
        _mockSensitivityLabelService
            .Setup(s => s.FilterAuthorizedRecordIds(uid, oid, pid, It.IsAny<List<long>>(), Context))
            .ReturnsAsync(new HashSet<long> { recordId1 });

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        var payload = new InsightUploadApiRequestDto
        {
            FileInfo = [new() { FileId = recordId1, FileUri = "org_1/rec1.pdf" }]
        };

        await _insightBusiness.QueueInsightUpload(uid, oid, pid, vlmConfigId, embeddingConfigId, payload);

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"overwrite\":false", body.ToLowerInvariant());
    }

    #endregion

    // =========================================================================
    // TriggerEmbedding Tests
    // =========================================================================

    #region TriggerEmbedding Tests

    [Fact]
    public async Task TriggerEmbedding_CallsCreateProvenanceRecord_WithExpectedArguments()
    {
        SetupHttpSuccess();
        _mockProvenanceBusiness
            .Setup(p => p.CreateProvenanceRecord(recordId1, "request-embedding", uid, embeddingConfigId))
            .ReturnsAsync(true);

        var vlmCfg = MakeConfig(vlmConfigId, "vlm");
        var embCfg = MakeConfig(embeddingConfigId, "embedding");

        _insightBusiness.TriggerEmbedding(pid, recordId1, "org_1/rec1.pdf", uid, vlmCfg, embCfg);

        // Fire-and-forget: give the background continuations a moment to run.
        await Task.Delay(200);

        _mockProvenanceBusiness.Verify(
            p => p.CreateProvenanceRecord(recordId1, "request-embedding", uid, embeddingConfigId),
            Times.Once);
    }

    [Fact]
    public async Task TriggerEmbedding_LogsWarning_WhenProvenanceRecordCreationFails()
    {
        SetupHttpSuccess();
        _mockProvenanceBusiness
            .Setup(p => p.CreateProvenanceRecord(recordId1, "request-embedding", uid, embeddingConfigId))
            .ReturnsAsync(false);

        var vlmCfg = MakeConfig(vlmConfigId, "vlm");
        var embCfg = MakeConfig(embeddingConfigId, "embedding");

        _insightBusiness.TriggerEmbedding(pid, recordId1, "org_1/rec1.pdf", uid, vlmCfg, embCfg);

        await Task.Delay(200);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to create provenance record")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerEmbedding_LogsError_WhenInsightUploadFaults()
    {
        SetupHttpFailure();
        _mockProvenanceBusiness
            .Setup(p => p.CreateProvenanceRecord(recordId1, "request-embedding", uid, embeddingConfigId))
            .ReturnsAsync(true);

        var vlmCfg = MakeConfig(vlmConfigId, "vlm");
        var embCfg = MakeConfig(embeddingConfigId, "embedding");

        _insightBusiness.TriggerEmbedding(pid, recordId1, "org_1/rec1.pdf", uid, vlmCfg, embCfg);

        await Task.Delay(200);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Insight enqueue failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void TriggerEmbedding_ReturnsImmediately_WithoutBlockingCaller()
    {
        // Arrange - make the HTTP call hang so we can prove the method doesn't wait for it.
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") };
            });
        _mockProvenanceBusiness
            .Setup(p => p.CreateProvenanceRecord(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long?>()))
            .ReturnsAsync(true);

        var vlmCfg = MakeConfig(vlmConfigId, "vlm");
        var embCfg = MakeConfig(embeddingConfigId, "embedding");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _insightBusiness.TriggerEmbedding(pid, recordId1, "org_1/rec1.pdf", uid, vlmCfg, embCfg);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, "TriggerEmbedding should return immediately without awaiting background work.");
    }

    #endregion

    // =========================================================================
    // QueueInsightEmbedStrings Tests
    // =========================================================================

    #region QueueInsightEmbedStrings Tests

    [Fact]
    public async Task QueueInsightEmbedStrings_Throws_WhenNoClassesOrRelationships()
    {
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ThrowsAsync(new KeyNotFoundException("no default"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.QueueInsightEmbedStrings(uid, oid, pid, null));

        Assert.Contains("Define at least one class", ex.Message);
        Assert.Contains("Define at least one relationship", ex.Message);
    }

    [Fact]
    public async Task QueueInsightEmbedStrings_Succeeds_WithClassesAndRelationships()
    {
        var cls = new Class { Name = "Widget", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "A widget", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.Add(cls);
        await Context.SaveChangesAsync();

        var cls2 = new Class { Name = "Gadget", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "A gadget", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.Add(cls2);
        await Context.SaveChangesAsync();

        var rel = new Relationship { Name = "connects to", OriginId = cls.Id, DestinationId = cls2.Id, ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "connects", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();

        var embCfg = MakeConfig(embeddingConfigId, "embedding");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ReturnsAsync(embCfg);

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        await _insightBusiness.QueueInsightEmbedStrings(uid, oid, pid, null);

        Assert.NotNull(capturedRequest);
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("widget", body.ToLowerInvariant());
        Assert.Contains("connects", body.ToLowerInvariant());
    }

    [Fact]
    public async Task QueueInsightEmbedStrings_ExcludesArchivedClassesAndRelationships()
    {
        var archivedCls = new Class { Name = "OldWidget", ProjectId = pid, OrganizationId = oid, IsArchived = true, Description = "old", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var activeCls1 = new Class { Name = "Widget", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "A widget", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var activeCls2 = new Class { Name = "Gadget", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "A gadget", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.AddRange(archivedCls, activeCls1, activeCls2);
        await Context.SaveChangesAsync();

        var rel = new Relationship { Name = "connects to", OriginId = activeCls1.Id, DestinationId = activeCls2.Id, ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "connects", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();

        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ThrowsAsync(new KeyNotFoundException("no default"));

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        await _insightBusiness.QueueInsightEmbedStrings(uid, oid, pid, null);

        Assert.NotNull(capturedRequest);
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain("oldwidget", body.ToLowerInvariant());
    }

    [Fact]
    public async Task QueueInsightEmbedStrings_UsesNameAsFallback_WhenDescriptionEmpty()
    {
        var cls1 = new Class { Name = "NoDescClass", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var cls2 = new Class { Name = "OtherClass", ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "has description", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.AddRange(cls1, cls2);
        await Context.SaveChangesAsync();

        var rel = new Relationship { Name = "NoDescRelationship", OriginId = cls1.Id, DestinationId = cls2.Id, ProjectId = pid, OrganizationId = oid, IsArchived = false, Description = "", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();

        _mockAiModelConfigBusiness
            .Setup(m => m.GetDefaultAiModelConfigWithToken(uid, oid, pid, "embedding"))
            .ThrowsAsync(new KeyNotFoundException("no default"));

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        await _insightBusiness.QueueInsightEmbedStrings(uid, oid, pid, null);

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("nodescclass", body.ToLowerInvariant());
        Assert.Contains("nodescrelationship", body.ToLowerInvariant());
    }

    #endregion

    // =========================================================================
    // CheckEndpointHealth Tests
    // =========================================================================

    #region CheckEndpointHealth Tests

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("chat")]
    public async Task CheckEndpointHealth_Throws_WhenModelTypeInvalid(string modelType)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.CheckEndpointHealth(uid, oid, pid, vlmConfigId, modelType));

        Assert.Contains("modelType must be one of", ex.Message);
    }

    [Fact]
    public async Task CheckEndpointHealth_Throws_WhenResolvedConfigTypeMismatch()
    {
        var mismatchedConfig = MakeConfig(vlmConfigId, "embedding");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId))
            .ReturnsAsync(mismatchedConfig);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _insightBusiness.CheckEndpointHealth(uid, oid, pid, vlmConfigId, "vlm"));

        Assert.Contains("is type 'embedding' but 'vlm' was requested", ex.Message);
    }

    [Fact]
    public async Task CheckEndpointHealth_NormalizesModelTypeCasing()
    {
        var config = MakeConfig(vlmConfigId, "vlm");
        _mockAiModelConfigBusiness
            .Setup(m => m.GetAiModelConfigWithToken(uid, oid, pid, vlmConfigId))
            .ReturnsAsync(config);
        SetupHttpSuccess("""{ "reachable": true, "modelAvailable": true, "latencyMs": 42 }""");

        var result = await _insightBusiness.CheckEndpointHealth(uid, oid, pid, vlmConfigId, " VLM ");

        Assert.NotNull(result);
    }

    #endregion

    // =========================================================================
    // FetchInsightIngestionStatus Tests
    // =========================================================================

    #region FetchInsightIngestionStatus Tests

    [Fact]
    public async Task FetchInsightIngestionStatus_DelegatesToInsightServiceClient()
    {
        SetupHttpSuccess("""{ "status": "complete" }""");

        var result = await _insightBusiness.FetchInsightIngestionStatus(recordId1);

        Assert.NotNull(result);
    }

    #endregion

    // =========================================================================
    // NormalizeFileUri Tests (exercised indirectly via QueueInsightUpload)
    // =========================================================================

    #region NormalizeFileUri Tests

    [Theory]
    [InlineData("https://example.com/file.pdf", "https://example.com/file.pdf")]
    [InlineData("/data/org_1/file.pdf", "/data/org_1/file.pdf")]
    [InlineData("org_1/file.pdf", "/data/org_1/file.pdf")]
    [InlineData("/usr/src/app/org_1/file.pdf", "/data/org_1/file.pdf")]
    public async Task QueueInsightUpload_NormalizesFileUri(string inputUri, string expectedUri)
    {
        SetupDefaultConfigsForUpload(out _, out _);
        _mockSensitivityLabelService
            .Setup(s => s.FilterAuthorizedRecordIds(uid, oid, pid, It.IsAny<List<long>>(), Context))
            .ReturnsAsync(new HashSet<long> { recordId1 });

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        var payload = new InsightUploadApiRequestDto
        {
            FileInfo = [new() { FileId = recordId1, FileUri = inputUri }]
        };

        await _insightBusiness.QueueInsightUpload(uid, oid, pid, vlmConfigId, embeddingConfigId, payload);

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains(expectedUri, body);
    }

    #endregion
}