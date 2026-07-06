using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using DlRecord = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class LatticeExtractionBusinessTests : IntegrationTestBase
{
    // Stored so InitializeAsync can create a fresh LatticeContext per test
    // from the shared data source, mirroring how IntegrationTestBase handles DeeplynxContext.
    private readonly TestSuiteFixture _fixture;

    private LatticeExtractionBusiness _business = null!;
    private LatticeContext _latticeCtx = null!;
    private Mock<IInsightBusiness> _mockInsight = null!;
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private InsightServiceClient _client = null!;
    private Mock<IProvenanceBusiness> _mockProvenance = null!;
    private Mock<ILogger<LatticeExtractionBusiness>> _mockLogger = null!;

    private const long NotFoundId = 99_999L;

    public long uid, oid, pid, dsid;
    public long cid1, cid2, relid1;
    public long extractionId;
    public long completeExtractionId;
    public long recordId;

    public LatticeExtractionBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    public override async Task InitializeAsync()
    {
        // Create a fresh per-test LatticeContext backed by the shared Postgres container.
        _latticeCtx = new LatticeContext(
            new DbContextOptionsBuilder<LatticeContext>()
                .UseNpgsql(_fixture.PostgresDataSource)
                .Options);

        // Clean lattice staging tables before each test so tests are isolated.
        await CleanLatticeDatabaseAsync();

        // Chain to base so CleanDatabaseAsync + SeedTestDataAsync run as normal.
        await base.InitializeAsync();

        _mockInsight = new Mock<IInsightBusiness>();
        _mockHandler = new Mock<HttpMessageHandler>();
        Environment.SetEnvironmentVariable("INSIGHT_FASTAPI_URL", "http://localhost:5000");
        _client = new InsightServiceClient(new HttpClient(_mockHandler.Object));
        _mockLogger = new Mock<ILogger<LatticeExtractionBusiness>>();
        _mockProvenance = new Mock<IProvenanceBusiness>();

        _business = new LatticeExtractionBusiness(
            Context, _latticeCtx,
            _mockInsight.Object, _client, _mockProvenance.Object, _mockLogger.Object);
    }

    public override async Task DisposeAsync()
    {
        await _latticeCtx.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    ///     Deletes all lattice staging rows in FK-safe order before each test.
    ///     Edges depend on records and relationships; records and relationships depend on classes.
    /// </summary>
    private async Task CleanLatticeDatabaseAsync()
    {
        var edges = await _latticeCtx.ExtractionEdges.ToListAsync();
        _latticeCtx.ExtractionEdges.RemoveRange(edges);
        await _latticeCtx.SaveChangesAsync();

        var records = await _latticeCtx.ExtractionRecords.ToListAsync();
        _latticeCtx.ExtractionRecords.RemoveRange(records);
        await _latticeCtx.SaveChangesAsync();

        var relationships = await _latticeCtx.ExtractionRelationships.ToListAsync();
        _latticeCtx.ExtractionRelationships.RemoveRange(relationships);
        await _latticeCtx.SaveChangesAsync();

        var classes = await _latticeCtx.ExtractionClasses.ToListAsync();
        _latticeCtx.ExtractionClasses.RemoveRange(classes);
        await _latticeCtx.SaveChangesAsync();
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User { Name = "Lattice User", Email = "lattice@test.com", Password = "pw", IsArchived = false };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var org = new Organization { Name = "Lattice Org", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var proj = new Project { Name = "Lattice Project", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();
        pid = proj.Id;

        var ds = new DataSource { Name = "Lattice DS", ProjectId = pid, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();
        dsid = ds.Id;

        var c1 = new Class { Name = "Military Organization", ProjectId = pid, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var c2 = new Class { Name = "Air Force Base", ProjectId = pid, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.AddRange(c1, c2);
        await Context.SaveChangesAsync();
        cid1 = c1.Id; cid2 = c2.Id;

        var rel = new Relationship { Name = "located at", OriginId = cid1, DestinationId = cid2, ProjectId = pid, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();
        relid1 = rel.Id;

        var running = new Extraction { CreatedBy = uid, Status = ExtractionStatus.Running, Mode = ExtractionMode.Strict, ProjectId = pid };
        var complete = new Extraction { CreatedBy = uid, Status = ExtractionStatus.Complete, Mode = ExtractionMode.Discovery, ProjectId = pid };
        Context.Extractions.AddRange(running, complete);
        await Context.SaveChangesAsync();
        extractionId = running.Id;
        completeExtractionId = complete.Id;

        var rec = new DlRecord
        {
            Name = "Test Record",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = dsid,
            OriginalId = "rec-001",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/usr/src/app"
        };
        Context.Records.Add(rec);
        await Context.SaveChangesAsync();
        recordId = rec.Id;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private (AiModelConfigResponseDto.WithToken Vlm, AiModelConfigResponseDto.WithToken Embedding) SetupModelConfigMocks()
    {
        var vlmCfg = new AiModelConfigResponseDto.WithToken { Id = 1, ModelType = "vlm", ServerUrl = "http://vlm", ModelName = "m", ModelProvider = "p", OrganizationId = oid };
        var embCfg = new AiModelConfigResponseDto.WithToken { Id = 2, ModelType = "embedding", ServerUrl = "http://emb", ModelName = "m", ModelProvider = "p", OrganizationId = oid };

        _mockInsight.Setup(i => i.ResolveModelConfig(uid, oid, pid, null, "vlm")).ReturnsAsync(vlmCfg);
        _mockInsight.Setup(i => i.ResolveModelConfig(uid, oid, pid, null, "llm")).ReturnsAsync(vlmCfg);
        _mockInsight.Setup(i => i.ResolveModelConfig(uid, oid, pid, null, "embedding")).ReturnsAsync(embCfg);
        _mockInsight.Setup(i => i.QueueInsightEmbedStrings(uid, oid, pid, null)).Returns(Task.CompletedTask);

        return (vlmCfg, embCfg);
    }

    private static InsightExtractionCallbackDto BuildValidDto() => new()
    {
        Classes =
        [
            new InsightExtractedClassDto { Class = "100th Wing",     ClassType = "Military Organization", Confidence = 0.9 },
            new InsightExtractedClassDto { Class = "RAF Mildenhall", ClassType = "Air Force Base",        Confidence = 0.9 }
        ],
        Relationships =
        [
            new InsightExtractedRelationshipDto
            {
                Subject = "100th Wing",     SubjectType = "Military Organization",
                RelationshipType = "located at",
                Object  = "RAF Mildenhall", ObjectType  = "Air Force Base",
                Confidence = 0.9
            }
        ]
    };

    private static InsightExtractionCallbackDto BuildInvalidSchemaDto() => new()
    {
        Classes = [new InsightExtractedClassDto { Class = "Unknown Entity", ClassType = "UnknownXyzType", Confidence = 0.9 }],
        Relationships = []
    };

    private record StagingIds(long CId1, long CId2, long RId1, long RId2, long RelId, long EId);

    private async Task<StagingIds> SeedStagingAsync(
        long exId,
        string status = ExtractionValidationStatus.Valid)
    {
        var isValid = status == ExtractionValidationStatus.Valid;
        var cName1 = isValid ? "Military Organization" : "Novel Type Alpha";
        var cName2 = isValid ? "Air Force Base" : "Novel Type Beta";
        var relName = isValid ? "located at" : "novel relation";
        long? ontC1 = isValid ? cid1 : null;
        long? ontC2 = isValid ? cid2 : null;
        long? ontR = isValid ? relid1 : null;

        var sc1 = new ExtractionClass { ExtractionId = exId, Name = cName1, OrganizationId = oid, ProjectId = pid, ValidationStatus = status, OntologyClassId = ontC1 };
        var sc2 = new ExtractionClass { ExtractionId = exId, Name = cName2, OrganizationId = oid, ProjectId = pid, ValidationStatus = status, OntologyClassId = ontC2 };
        _latticeCtx.ExtractionClasses.AddRange(sc1, sc2);
        await _latticeCtx.SaveChangesAsync();

        var sr1 = new ExtractionRecord { ExtractionId = exId, ExtractionClassId = sc1.Id, Name = "100th Wing", OrganizationId = oid, ProjectId = pid, DataSourceId = dsid, ValidationStatus = status, Frequency = 2, LlmScore = 0.9 };
        var sr2 = new ExtractionRecord { ExtractionId = exId, ExtractionClassId = sc2.Id, Name = "RAF Mildenhall", OrganizationId = oid, ProjectId = pid, DataSourceId = dsid, ValidationStatus = status, Frequency = 1, LlmScore = 0.9 };
        _latticeCtx.ExtractionRecords.AddRange(sr1, sr2);
        await _latticeCtx.SaveChangesAsync();

        var srel = new ExtractionRelationship { ExtractionId = exId, OriginClassId = sc1.Id, DestinationClassId = sc2.Id, Name = relName, OrganizationId = oid, ProjectId = pid, ValidationStatus = status, OntologyRelationshipId = ontR };
        _latticeCtx.ExtractionRelationships.Add(srel);
        await _latticeCtx.SaveChangesAsync();

        var se = new ExtractionEdge { ExtractionId = exId, ExtractionRelationshipId = srel.Id, OriginRecordId = sr1.Id, DestinationRecordId = sr2.Id, OrganizationId = oid, ProjectId = pid, DataSourceId = dsid, ValidationStatus = status, Frequency = 1, LlmScore = 0.9 };
        _latticeCtx.ExtractionEdges.Add(se);
        await _latticeCtx.SaveChangesAsync();

        return new StagingIds(sc1.Id, sc2.Id, sr1.Id, sr2.Id, srel.Id, se.Id);
    }

    /// <summary>
    ///     Seeds a minimal staging set (one class + one record) with configurable
    ///     SourceRecordId and Attributes on the record, for originId injection tests.
    /// </summary>
    private async Task<(long ClassId, long RecordId)> SeedRecordForOriginIdAsync(
        long exId,
        long? sourceRecordId,
        string? attributes)
    {
        var sc = new ExtractionClass
        {
            ExtractionId = exId,
            Name = "OriginId Test Class",
            OrganizationId = oid,
            ProjectId = pid,
            ValidationStatus = ExtractionValidationStatus.InvalidSchema
        };
        _latticeCtx.ExtractionClasses.Add(sc);
        await _latticeCtx.SaveChangesAsync();

        var sr = new ExtractionRecord
        {
            ExtractionId = exId,
            ExtractionClassId = sc.Id,
            Name = "OriginId Test Record",
            OrganizationId = oid,
            ProjectId = pid,
            DataSourceId = dsid,
            ValidationStatus = ExtractionValidationStatus.InvalidSchema,
            Frequency = 1,
            LlmScore = 0.9,
            SourceRecordId = sourceRecordId,
            Attributes = attributes
        };
        _latticeCtx.ExtractionRecords.Add(sr);
        await _latticeCtx.SaveChangesAsync();

        return (sc.Id, sr.Id);
    }

    // Promotes every staged item of an extraction — the "approve all" equivalent under the
    // selection-based promote API.
    private async Task<ExtractionResponseDto> PromoteAllAsync(long exId)
    {
        var request = new PromoteExtractionRequestDto
        {
            ClassIds = await _latticeCtx.ExtractionClasses
                .Where(c => c.ExtractionId == exId).Select(c => c.Id).ToListAsync(),
            RecordIds = await _latticeCtx.ExtractionRecords
                .Where(r => r.ExtractionId == exId).Select(r => r.Id).ToListAsync(),
            RelationshipIds = await _latticeCtx.ExtractionRelationships
                .Where(r => r.ExtractionId == exId).Select(r => r.Id).ToListAsync(),
            EdgeIds = await _latticeCtx.ExtractionEdges
                .Where(e => e.ExtractionId == exId).Select(e => e.Id).ToListAsync()
        };
        return await _business.PromoteExtraction(uid, oid, pid, exId, request);
    }

    // =========================================================================
    // MarkExtractionFailed Tests
    // =========================================================================

    #region MarkExtractionFailed Tests

    [Fact]
    public async Task MarkExtractionFailed_SetsStatusToFailed()
    {
        await _business.MarkExtractionFailed(extractionId, oid, pid, "test error");

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(extractionId);
        Assert.NotNull(ex);
        Assert.Equal(ExtractionStatus.Failed, ex.Status);
    }

    [Fact]
    public async Task MarkExtractionFailed_Throws_WhenExtractionNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.MarkExtractionFailed(NotFoundId, oid, pid));
    }

    [Fact]
    public async Task MarkExtractionFailed_Throws_WhenScopedToWrongProject()
    {
        var otherProj = new Project
        {
            Name = "Wrong Scope Project",
            OrganizationId = oid,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid
        };
        Context.Projects.Add(otherProj);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.MarkExtractionFailed(extractionId, oid, otherProj.Id, "wrong scope"));
    }

    #endregion

    // =========================================================================
    // ListExtractionsByProject Tests
    // =========================================================================

    #region ListExtractionsByProject Tests

    [Fact]
    public async Task ListExtractionsByProject_ReturnsExtractionsForCorrectProject()
    {
        var result = await _business.ListExtractionsByProject(pid);

        Assert.NotEmpty(result);
        Assert.All(result, e => Assert.Equal(pid, e.ProjectId));
    }

    [Fact]
    public async Task ListExtractionsByProject_ReturnsEmpty_WhenNoExtractionsExistForProject()
    {
        var result = await _business.ListExtractionsByProject(NotFoundId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListExtractionsByProject_DoesNotReturnOtherProjectExtractions()
    {
        //add a project with no extractions
        var otherProj = new Project { Name = "Other Project", IsArchived = false, OrganizationId = oid };
        Context.Projects.Add(otherProj);

        //add an extraction to pid
        var other = new User { Name = "Other User", Email = "other@test.com", Password = "pw", IsArchived = false };
        Context.Users.Add(other);
        await Context.SaveChangesAsync();

        var extraction = new Extraction { CreatedBy = other.Id, ProjectId = pid };
        Context.Extractions.Add(extraction);
        await Context.SaveChangesAsync();

        var result = await _business.ListExtractionsByProject(otherProj.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListExtractionsByProject_ReturnsCorrectFields()
    {
        var result = await _business.ListExtractionsByProject(pid);

        Assert.Contains(result, e => e.Id == extractionId && e.Status == ExtractionStatus.Running && e.Mode == ExtractionMode.Strict);
        Assert.Contains(result, e => e.Id == completeExtractionId && e.Status == ExtractionStatus.Complete && e.Mode == ExtractionMode.Discovery);
    }

    [Fact]
    public async Task ListExtractionsByProject_ReturnsFailureMessage()
    {
        const string failureMessage = "LLM model endpoint rejected the request.";
        await _business.MarkExtractionFailed(extractionId, oid, pid, failureMessage);

        var result = await _business.ListExtractionsByProject(pid);

        Assert.Contains(result, e => e.Id == extractionId && e.FailureMessage == failureMessage);
    }

    #endregion

    // =========================================================================
    // GetEmbeddingStatus Tests
    // =========================================================================

    #region GetEmbeddingStatus Tests

    [Fact]
    public async Task GetEmbeddingStatus_ReturnsCorrectClassAndRelationshipCounts()
    {
        var result = await _business.GetEmbeddingStatus(pid);

        Assert.Equal(2, result.ClassCount);
        Assert.Equal(1, result.RelationshipCount);
    }

    [Fact]
    public async Task GetEmbeddingStatus_ReturnsZeroEmbeddedCounts_WhenNoEmbeddingsExist()
    {
        var result = await _business.GetEmbeddingStatus(pid);

        Assert.Equal(0, result.EmbeddedClassCount);
        Assert.Equal(0, result.EmbeddedRelationshipCount);
        Assert.False(result.OntologyReady);
    }

    [Fact]
    public async Task GetEmbeddingStatus_ReturnsZeroCounts_WhenProjectDoesNotExist()
    {
        var result = await _business.GetEmbeddingStatus(NotFoundId);

        Assert.Equal(0, result.ClassCount);
        Assert.Equal(0, result.RelationshipCount);
        Assert.False(result.OntologyReady);
    }

    [Fact]
    public async Task GetEmbeddingStatus_ReturnsOntologyReady_WhenRequiredSchemaIsEmbedded()
    {
        // OntologyVector.Vector is typed as string in the model but the DB column is
        // type vector with a NOT NULL constraint — EF Core cannot bridge that gap.
        // Use raw SQL with a zero vector literal to satisfy the constraint.
        await Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO dl_vector.ontology_vector (class_id, vector) VALUES ({0}, '[0]')", cid1);
        await Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO dl_vector.ontology_vector (class_id, vector) VALUES ({0}, '[0]')", cid2);
        await Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO dl_vector.ontology_vector (relationship_id, vector) VALUES ({0}, '[0]')", relid1);

        var result = await _business.GetEmbeddingStatus(pid);

        Assert.Equal(2, result.EmbeddedClassCount);
        Assert.Equal(1, result.EmbeddedRelationshipCount);
        Assert.True(result.OntologyReady);
    }

    #endregion

    // =========================================================================
    // ProcessInsightExtractionCallback Tests
    // =========================================================================

    #region ProcessInsightExtractionCallback Tests

    [Fact]
    public async Task ProcessInsightExtractionCallback_StagesAllItems_AndSetsExtractionComplete()
    {
        var result = await _business.ProcessInsightCallback(
            oid, pid, dsid, extractionId, BuildValidDto());

        Assert.Equal(2, result.ClassCount);
        Assert.Equal(2, result.RecordCount);
        Assert.Equal(1, result.RelationshipCount);
        Assert.Equal(1, result.EdgeCount);

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(extractionId);
        Assert.Equal(ExtractionStatus.Complete, ex!.Status);
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_SetsValidStatus_WhenOntologyMatches()
    {
        await _business.ProcessInsightCallback(oid, pid, dsid, extractionId, BuildValidDto());

        var classes = _latticeCtx.ExtractionClasses.Where(c => c.ExtractionId == extractionId).ToList();
        Assert.All(classes, c => Assert.Equal(ExtractionValidationStatus.Valid, c.ValidationStatus));
        Assert.All(classes, c => Assert.NotNull(c.OntologyClassId));

        var records = _latticeCtx.ExtractionRecords.Where(r => r.ExtractionId == extractionId).ToList();
        Assert.All(records, r => Assert.Equal(ExtractionValidationStatus.Valid, r.ValidationStatus));
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_SetsInvalidSchema_WhenNoOntologyMatch()
    {
        var result = await _business.ProcessInsightCallback(
            oid, pid, dsid, extractionId, BuildInvalidSchemaDto());

        Assert.Equal(1, result.ClassCount);
        var cls = _latticeCtx.ExtractionClasses.Single(c => c.ExtractionId == extractionId);
        Assert.Equal(ExtractionValidationStatus.InvalidSchema, cls.ValidationStatus);
        Assert.Null(cls.OntologyClassId);
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_Deduplicates_DuplicateClassesInDto()
    {
        var dto = new InsightExtractionCallbackDto
        {
            Classes =
            [
                new InsightExtractedClassDto { Class = "100th Wing", ClassType = "Military Organization", Confidence = 0.9 },
                new InsightExtractedClassDto { Class = "100th Wing", ClassType = "Military Organization", Confidence = 0.8 }
            ],
            Relationships = []
        };

        var result = await _business.ProcessInsightCallback(oid, pid, dsid, extractionId, dto);

        Assert.Equal(1, result.ClassCount);
        Assert.Equal(1, result.RecordCount);
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_SetsNovelDiscovery_WhenPatternNotInOntology()
    {
        var ex = await Context.Extractions.FindAsync(extractionId);
        ex!.Mode = ExtractionMode.Discovery;
        await Context.SaveChangesAsync();

        var dto = new InsightExtractionCallbackDto
        {
            Classes =
            [
                new InsightExtractedClassDto { Class = "RAF Mildenhall", ClassType = "Air Force Base",        Confidence = 0.9 },
                new InsightExtractedClassDto { Class = "100th Wing",     ClassType = "Military Organization", Confidence = 0.9 }
            ],
            Relationships =
            [
                new InsightExtractedRelationshipDto
                {
                    Subject = "RAF Mildenhall", SubjectType = "Air Force Base",
                    RelationshipType = "located at",
                    Object  = "100th Wing",     ObjectType  = "Military Organization",
                    Confidence = 0.9
                }
            ]
        };

        await _business.ProcessInsightCallback(oid, pid, dsid, extractionId, dto);

        var rel = _latticeCtx.ExtractionRelationships.Single(r => r.ExtractionId == extractionId);
        Assert.Equal(ExtractionValidationStatus.NovelDiscovery, rel.ValidationStatus);
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_Throws_WhenExtractionNotFound()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.ProcessInsightCallback(oid, pid, dsid, NotFoundId, BuildValidDto()));

        Assert.Contains(NotFoundId.ToString(), ex.Message);
    }

    [Fact]
    public async Task ProcessInsightExtractionCallback_SkipsEdge_WhenSubjectRecordNotStaged()
    {
        var dto = new InsightExtractionCallbackDto
        {
            Classes =
            [
                new InsightExtractedClassDto { Class = "RAF Mildenhall", ClassType = "Air Force Base", Confidence = 0.9 }
            ],
            Relationships =
            [
                new InsightExtractedRelationshipDto
                {
                    Subject = "Ghost Unit",    SubjectType = "Military Organization",
                    RelationshipType = "located at",
                    Object  = "RAF Mildenhall", ObjectType  = "Air Force Base",
                    Confidence = 0.9
                }
            ]
        };

        var result = await _business.ProcessInsightCallback(oid, pid, dsid, extractionId, dto);

        Assert.Equal(0, result.EdgeCount);
    }

    #endregion

    // =========================================================================
    // PromoteExtraction Tests
    // =========================================================================

    #region PromoteExtraction Tests

    [Fact]
    public async Task RejectExtraction_SetsRejectedStatus_AndWritesNoDeeplynxRows()
    {
        await SeedStagingAsync(completeExtractionId);

        var classesBefore = Context.Classes.Count();
        var recsBefore = Context.Records.Count();
        var relsBefore = Context.Relationships.Count();
        var edgesBefore = Context.Edges.Count();

        await _business.RejectExtraction(completeExtractionId,
            new RejectExtractionRequestDto { RejectAllRemaining = true });

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Rejected, ex!.Status);
        Assert.Equal(classesBefore, Context.Classes.Count());
        Assert.Equal(recsBefore, Context.Records.Count());
        Assert.Equal(relsBefore, Context.Relationships.Count());
        Assert.Equal(edgesBefore, Context.Edges.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Throws_WhenExtractionNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, NotFoundId, new PromoteExtractionRequestDto()));
    }

    [Fact]
    public async Task PromoteExtraction_Throws_WhenExtractionStatusIsNotComplete()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, extractionId, new PromoteExtractionRequestDto()));
    }

    [Fact]
    public async Task PromoteExtraction_Approve_DoesNotDuplicateValidClasses()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.Valid);
        var classesBefore = Context.Classes.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(classesBefore, Context.Classes.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_CreatesNewDeeplynxClasses_ForInvalidSchema()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        var classesBefore = Context.Classes.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(classesBefore + 2, Context.Classes.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_SetsPromotedId_OnStagingClasses()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await PromoteAllAsync(completeExtractionId);

        _latticeCtx.ChangeTracker.Clear();
        var stagingClasses = _latticeCtx.ExtractionClasses
            .Where(c => c.ExtractionId == completeExtractionId)
            .ToList();
        Assert.All(stagingClasses, c => Assert.NotNull(c.PromotedId));
    }

    [Fact]
    public async Task PromoteExtraction_Approve_DeduplicatesClassNameWithinStagingBatch()
    {
        var sc1 = new ExtractionClass { ExtractionId = completeExtractionId, Name = "Novel Type", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        var sc2 = new ExtractionClass { ExtractionId = completeExtractionId, Name = "Novel Type", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        _latticeCtx.ExtractionClasses.AddRange(sc1, sc2);
        await _latticeCtx.SaveChangesAsync();

        var classesBefore = Context.Classes.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(classesBefore + 1, Context.Classes.Count());

        _latticeCtx.ChangeTracker.Clear();
        var promoted1 = _latticeCtx.ExtractionClasses.Find(sc1.Id);
        var promoted2 = _latticeCtx.ExtractionClasses.Find(sc2.Id);
        Assert.NotNull(promoted1!.PromotedId);
        Assert.Equal(promoted1.PromotedId, promoted2!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Approve_CreatesNewDeeplynxRecords()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        var recsBefore = Context.Records.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(recsBefore + 2, Context.Records.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_LinksExistingRecord_WhenDeeplynxRecordIdSet()
    {
        var sc = new ExtractionClass
        {
            ExtractionId = completeExtractionId,
            Name = "Military Organization",
            OrganizationId = oid,
            ProjectId = pid,
            ValidationStatus = ExtractionValidationStatus.Valid,
            OntologyClassId = cid1
        };
        _latticeCtx.ExtractionClasses.Add(sc);
        await _latticeCtx.SaveChangesAsync();

        var sr = new ExtractionRecord
        {
            ExtractionId = completeExtractionId,
            ExtractionClassId = sc.Id,
            Name = "Test Record",
            OrganizationId = oid,
            ProjectId = pid,
            DataSourceId = dsid,
            ValidationStatus = ExtractionValidationStatus.Valid,
            DeeplynxRecordId = recordId,
            Frequency = 1,
            LlmScore = 0.9
        };
        _latticeCtx.ExtractionRecords.Add(sr);
        await _latticeCtx.SaveChangesAsync();

        var recsBefore = Context.Records.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(recsBefore, Context.Records.Count());

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(sr.Id);
        Assert.Equal(recordId, promoted!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Approve_DoesNotDuplicateValidRelationships()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.Valid);
        var relsBefore = Context.Relationships.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(relsBefore, Context.Relationships.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_CreatesNewDeeplynxRelationships_ForInvalidSchema()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        var relsBefore = Context.Relationships.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(relsBefore + 1, Context.Relationships.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_SetsPromotedId_OnStagingRelationships()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await PromoteAllAsync(completeExtractionId);

        _latticeCtx.ChangeTracker.Clear();
        var stagingRel = _latticeCtx.ExtractionRelationships.Find(ids.RelId);
        Assert.NotNull(stagingRel!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Approve_DeduplicatesRelationshipNameWithinStagingBatch()
    {
        var sc1 = new ExtractionClass { ExtractionId = completeExtractionId, Name = "Alpha", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        var sc2 = new ExtractionClass { ExtractionId = completeExtractionId, Name = "Beta", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        _latticeCtx.ExtractionClasses.AddRange(sc1, sc2);
        await _latticeCtx.SaveChangesAsync();

        var srel1 = new ExtractionRelationship { ExtractionId = completeExtractionId, OriginClassId = sc1.Id, DestinationClassId = sc2.Id, Name = "novel relation", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        var srel2 = new ExtractionRelationship { ExtractionId = completeExtractionId, OriginClassId = sc2.Id, DestinationClassId = sc1.Id, Name = "novel relation", OrganizationId = oid, ProjectId = pid, ValidationStatus = ExtractionValidationStatus.InvalidSchema };
        _latticeCtx.ExtractionRelationships.AddRange(srel1, srel2);
        await _latticeCtx.SaveChangesAsync();

        var relsBefore = Context.Relationships.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(relsBefore + 1, Context.Relationships.Count());

        _latticeCtx.ChangeTracker.Clear();
        var promoted1 = _latticeCtx.ExtractionRelationships.Find(srel1.Id);
        var promoted2 = _latticeCtx.ExtractionRelationships.Find(srel2.Id);
        Assert.NotNull(promoted1!.PromotedId);
        Assert.Equal(promoted1.PromotedId, promoted2!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Approve_CreatesNewDeeplynxEdges()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        var edgesBefore = Context.Edges.Count();

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        Assert.Equal(edgesBefore + 1, Context.Edges.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Approve_SetsPromotedId_OnStagingEdges()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await PromoteAllAsync(completeExtractionId);

        _latticeCtx.ChangeTracker.Clear();
        var stagingEdge = _latticeCtx.ExtractionEdges.Find(ids.EId);
        Assert.NotNull(stagingEdge!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Approve_SetsPromotedStatus()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Promoted, ex!.Status);
    }

    [Fact]
    public async Task PromoteExtraction_PartialSelection_SetsPartiallyPromoted_AndLeavesRestStaged()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        var recsBefore = Context.Records.Count();

        // Promote only the classes — records/relationship/edge are left staged.
        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [ids.CId1, ids.CId2] });

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.PartiallyPromoted, ex!.Status);
        Assert.Equal(recsBefore, Context.Records.Count());

        _latticeCtx.ChangeTracker.Clear();
        Assert.Null(_latticeCtx.ExtractionRecords.Find(ids.RId1)!.PromotedId);
    }

    [Fact]
    public async Task PromoteExtraction_Throws_WhenSelectedRecordsClassNotIncluded()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        // Selecting a record whose (novel) class is neither selected nor promoted must fail strictly.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
                new PromoteExtractionRequestDto { RecordIds = [ids.RId1] }));
    }

    [Fact]
    public async Task PromoteExtraction_SecondRound_CompletesPromotion()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [ids.CId1, ids.CId2] });
        var classesAfterRound1 = Context.Classes.Count();

        // Second round promotes everything that remains; already-promoted classes are not duplicated.
        await PromoteAllAsync(completeExtractionId);

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Promoted, ex!.Status);
        Assert.Equal(classesAfterRound1, Context.Classes.Count());
    }

    [Fact]
    public async Task PromoteExtraction_ApproveByStatus_Valid_PromotesAndCompletes()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.Valid);

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ApproveByStatus = [ExtractionValidationStatus.Valid] });

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Promoted, ex!.Status);
    }

    [Fact]
    public async Task PromoteExtraction_ApproveByStatus_InvalidSchema_Throws()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
                new PromoteExtractionRequestDto { ApproveByStatus = [ExtractionValidationStatus.InvalidSchema] }));
    }

    [Fact]
    public async Task PromoteExtraction_Throws_WhenNothingSelected()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.Valid);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, completeExtractionId, new PromoteExtractionRequestDto()));
    }

    #endregion

    // =========================================================================
    // RejectExtraction (per-item) Tests
    // =========================================================================

    #region RejectExtraction Tests

    [Fact]
    public async Task RejectExtraction_FlagsSelectedLeafItem_AndKeepsInProgress()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        // The edge is a leaf — rejecting it has no dependents to drag along.
        await _business.RejectExtraction(completeExtractionId,
            new RejectExtractionRequestDto { EdgeIds = [ids.EId] });

        _latticeCtx.ChangeTracker.Clear();
        Assert.True(_latticeCtx.ExtractionEdges.Find(ids.EId)!.Rejected);

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.PartiallyPromoted, ex!.Status);
    }

    [Fact]
    public async Task RejectExtraction_Strict_Throws_WhenDependentsNotIncluded()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        // Rejecting only the class strands its record, relationship, and edge.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.RejectExtraction(completeExtractionId,
                new RejectExtractionRequestDto { ClassIds = [ids.CId1] }));
    }

    [Fact]
    public async Task RejectExtraction_RejectsFullDependentClosure()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await _business.RejectExtraction(completeExtractionId, new RejectExtractionRequestDto
        {
            ClassIds = [ids.CId1],
            RecordIds = [ids.RId1],
            RelationshipIds = [ids.RelId],
            EdgeIds = [ids.EId]
        });

        _latticeCtx.ChangeTracker.Clear();
        Assert.True(_latticeCtx.ExtractionClasses.Find(ids.CId1)!.Rejected);
        Assert.True(_latticeCtx.ExtractionRecords.Find(ids.RId1)!.Rejected);
        Assert.True(_latticeCtx.ExtractionEdges.Find(ids.EId)!.Rejected);
    }

    [Fact]
    public async Task RejectExtraction_ByStatus_RejectsAllMatching_AndCompletesAsRejected()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await _business.RejectExtraction(completeExtractionId,
            new RejectExtractionRequestDto { RejectByStatus = [ExtractionValidationStatus.InvalidSchema] });

        _latticeCtx.ChangeTracker.Clear();
        Assert.True(_latticeCtx.ExtractionEdges.Find(ids.EId)!.Rejected);

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Rejected, ex!.Status);
    }

    [Fact]
    public async Task RejectExtraction_RejectAllRemaining_AfterPartialPromote_KeepsPromotedAndCompletes()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [ids.CId1, ids.CId2] });
        var classesAfterPromote = Context.Classes.Count();

        await _business.RejectExtraction(completeExtractionId,
            new RejectExtractionRequestDto { RejectAllRemaining = true });

        Context.ChangeTracker.Clear();
        var ex = await Context.Extractions.FindAsync(completeExtractionId);
        Assert.Equal(ExtractionStatus.Promoted, ex!.Status);
        Assert.Equal(classesAfterPromote, Context.Classes.Count());
    }

    [Fact]
    public async Task PromoteExtraction_Throws_WhenSelectingPreviouslyRejectedItem()
    {
        var ids = await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);
        await _business.RejectExtraction(completeExtractionId,
            new RejectExtractionRequestDto { EdgeIds = [ids.EId] });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
                new PromoteExtractionRequestDto { EdgeIds = [ids.EId] }));
    }

    #endregion

    // =========================================================================
    // GetExtractionStaging Tests
    // =========================================================================

    #region GetExtractionStaging Tests

    [Fact]
    public async Task GetExtractionStaging_ReturnsAllStagedItems_WithCorrectCounts()
    {
        await SeedStagingAsync(completeExtractionId);

        var result = await _business.GetExtractionStaging(completeExtractionId);

        Assert.Equal(completeExtractionId, result.Id);
        Assert.Equal(2, result.Classes.Count);
        Assert.Equal(2, result.Records.Count);
        Assert.Single(result.Relationships);
        Assert.Single(result.Edges);
    }

    [Fact]
    public async Task GetExtractionStaging_Throws_WhenExtractionNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.GetExtractionStaging(NotFoundId));
    }

    [Fact]
    public async Task GetExtractionStaging_ReturnsCorrectValidationStatuses_ForInvalidSchema()
    {
        await SeedStagingAsync(completeExtractionId, ExtractionValidationStatus.InvalidSchema);

        var result = await _business.GetExtractionStaging(completeExtractionId);

        Assert.All(result.Classes, c => Assert.Equal(ExtractionValidationStatus.InvalidSchema, c.ValidationStatus));
        Assert.All(result.Records, r => Assert.Equal(ExtractionValidationStatus.InvalidSchema, r.ValidationStatus));
        Assert.All(result.Relationships, r => Assert.Equal(ExtractionValidationStatus.InvalidSchema, r.ValidationStatus));
        Assert.All(result.Edges, e => Assert.Equal(ExtractionValidationStatus.InvalidSchema, e.ValidationStatus));
    }

    [Fact]
    public async Task GetExtractionStaging_ReturnsCorrectNames()
    {
        await SeedStagingAsync(completeExtractionId);

        var result = await _business.GetExtractionStaging(completeExtractionId);

        Assert.Contains(result.Records, r => r.Name == "100th Wing");
        Assert.Contains(result.Records, r => r.Name == "RAF Mildenhall");
        Assert.Contains(result.Relationships, r => r.Name == "located at");
        Assert.Contains(result.Edges, e => e.OriginRecordName == "100th Wing" && e.DestinationRecordName == "RAF Mildenhall");
    }

    [Fact]
    public async Task GetExtractionStaging_ReturnsExtractionMetadata()
    {
        var result = await _business.GetExtractionStaging(completeExtractionId);

        Assert.Equal(ExtractionStatus.Complete, result.Status);
        Assert.Equal(ExtractionMode.Discovery, result.Mode);
        Assert.Equal(uid, result.CreatedBy);
    }

    #endregion

    // =========================================================================
    // EnsureOntologyReady Tests (via TriggerLatticeExtraction)
    // =========================================================================

    #region EnsureOntologyReady Tests

    [Fact]
    public async Task TriggerLatticeExtraction_Throws_WhenFewerThanTwoNonDefaultClasses()
    {
        var proj = new Project { Name = "Sparse Proj", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();

        var ds = new DataSource { Name = "Sparse DS", ProjectId = proj.Id, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var c = new Class { Name = "Only Class", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.DataSources.Add(ds);
        Context.Classes.Add(c);
        await Context.SaveChangesAsync(); // c.Id is now populated

        // Build the Relationship AFTER saving so OriginId carries the real PK
        var rel = new Relationship { Name = "some rel", OriginId = c.Id, ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();

        var rec = new DlRecord { Name = "Sparse Rec", ProjectId = proj.Id, OrganizationId = oid, DataSourceId = ds.Id, OriginalId = "sp-1", Description = "", Properties = "{}", IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Records.Add(rec);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, proj.Id, rec.Id, ExtractionMode.Strict));

        Assert.Contains("sufficient ontology", ex.Message);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_Throws_WhenNoRelationshipsExist()
    {
        var proj = new Project { Name = "No Rel Proj", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();

        var c1 = new Class { Name = "Type A", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var c2 = new Class { Name = "Type B", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var ds = new DataSource { Name = "NoRel DS", ProjectId = proj.Id, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.AddRange(c1, c2);
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        var rec = new DlRecord { Name = "NoRel Rec", ProjectId = proj.Id, OrganizationId = oid, DataSourceId = ds.Id, OriginalId = "nr-1", Description = "", Properties = "{}", IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Records.Add(rec);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, proj.Id, rec.Id, ExtractionMode.Strict));

        Assert.Contains("sufficient ontology", ex.Message);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_Throws_WhenDefaultClassesDoNotCountTowardMinimum()
    {
        var proj = new Project { Name = "Default Only Proj", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();

        var file = new Class { Name = "File", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var report = new Class { Name = "Report", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var ts = new Class { Name = "Timeseries", ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        var ds = new DataSource { Name = "Default DS", ProjectId = proj.Id, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Classes.AddRange(file, report, ts);
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        // Build the Relationship AFTER saving so both FK columns carry real PKs
        var rel = new Relationship { Name = "some rel", OriginId = ts.Id, DestinationId = file.Id, ProjectId = proj.Id, OrganizationId = oid, IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Relationships.Add(rel);
        await Context.SaveChangesAsync();

        var rec = new DlRecord { Name = "Default Rec", ProjectId = proj.Id, OrganizationId = oid, DataSourceId = ds.Id, OriginalId = "def-1", Description = "", Properties = "{}", IsArchived = false, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Records.Add(rec);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, proj.Id, rec.Id, ExtractionMode.Strict));
    }

    [Fact]
    public async Task TriggerLatticeExtraction_Throws_WhenRecordNotFoundInProject()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, NotFoundId, ExtractionMode.Strict));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_Throws_WhenEmbeddingsNotReady()
    {
        SetupModelConfigMocks();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));

        Assert.Contains("Embeddings are being generated", ex.Message);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_QueuesRecordAndOntologyEmbeddings_WhenNotReady()
    {
        var (vlmCfg, embCfg) = SetupModelConfigMocks();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));

        _mockInsight.Verify(
            i => i.TriggerEmbedding(pid, recordId, It.IsAny<string>(), vlmCfg, embCfg, null, false),
            Times.Once);

        _mockInsight.Verify(
            i => i.QueueInsightEmbedStrings(uid, oid, pid, null),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // Provenance Record Creation Tests
    // =========================================================================

    #region Provenance Record Creation Tests

    [Fact]
    public async Task TriggerLatticeExtraction_CreatesProvenanceRecord_WhenRecordEmbeddingIsTriggered()
    {
        var (_, embCfg) = SetupModelConfigMocks();
        _mockProvenance
            .Setup(p => p.CreateProvenanceRecord(recordId, "embedding_requested", uid, embCfg.Id))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));

        _mockProvenance.Verify(
            p => p.CreateProvenanceRecord(recordId, "embedding_requested", uid, embCfg.Id),
            Times.Once);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_DoesNotCreateProvenanceRecord_WhenRecordAlreadyEmbedded()
    {
        SetupModelConfigMocks();
 
        // Seed an existing embedding for the record so the "record not embedded" branch —
        // the only branch that creates a provenance record — is skipped. Ontology embeddings
        // are intentionally left absent so the call still fails fast for retry.
        await Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO dl_vector.embeddings (record_id, page_number, text_chunk, vector, last_updated_at) " +
            "VALUES ({0}, {1}, {2}, '[0]', {3})",
            recordId, 1, "chunk text", DateTime.UtcNow);
 
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));
 
        _mockProvenance.Verify(
            p => p.CreateProvenanceRecord(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long?>()),
            Times.Never);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_PassesEmbeddingModelConfigId_ToProvenanceRecord()
    {
        var (_, embCfg) = SetupModelConfigMocks();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));

        // The embedding model config (not the vlm/llm config) is the one recorded as
        // provenance for the embedding_requested action.
        _mockProvenance.Verify(
            p => p.CreateProvenanceRecord(recordId, It.IsAny<string>(), uid, embCfg.Id),
            Times.Once);
    }

    [Fact]
    public async Task TriggerLatticeExtraction_StillThrowsEmbeddingsPendingError_WhenProvenanceRecordCreationFails()
    {
        SetupModelConfigMocks();

        // Simulate ProvenanceBusiness failing to find a historical record (returns false)
        _mockProvenance
            .Setup(p => p.CreateProvenanceRecord(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long?>()))
            .ReturnsAsync(false);

        // A failed provenance write should not surface as a different error, nor should it
        // prevent the normal "embeddings pending" retry signal from being thrown.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _business.TriggerLatticeExtraction(uid, oid, pid, recordId, ExtractionMode.Strict));

        Assert.Contains("Embeddings are being generated", ex.Message);
        _mockProvenance.Verify(
            p => p.CreateProvenanceRecord(recordId, "embedding_requested", uid, It.IsAny<long?>()),
            Times.Once);
    }

    #endregion

    // =========================================================================
    // OriginId Provenance Tests
    // =========================================================================

    #region OriginId Provenance Tests

    [Fact]
    public async Task PromoteRecords_InjectsOriginId_WhenSourceRecordIdIsSet()
    {
        var (classId, recId) = await SeedRecordForOriginIdAsync(
            completeExtractionId,
            sourceRecordId: 42L,
            attributes: """{"manufacturer":"Boeing"}""");

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [classId], RecordIds = [recId] });

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(recId);
        Assert.NotNull(promoted!.PromotedId);

        Context.ChangeTracker.Clear();
        var dlRecord = await Context.Records.FindAsync(promoted.PromotedId!.Value);
        Assert.NotNull(dlRecord);

        var props = System.Text.Json.Nodes.JsonNode.Parse(dlRecord!.Properties)!.AsObject();
        Assert.Equal(42L, props["originId"]!.GetValue<long>());
        Assert.Equal("Boeing", props["manufacturer"]!.GetValue<string>());
    }

    [Fact]
    public async Task PromoteRecords_CreatesOriginIdOnly_WhenAttributesIsNull()
    {
        var (classId, recId) = await SeedRecordForOriginIdAsync(
            completeExtractionId,
            sourceRecordId: 99L,
            attributes: null);

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [classId], RecordIds = [recId] });

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(recId);

        Context.ChangeTracker.Clear();
        var dlRecord = await Context.Records.FindAsync(promoted!.PromotedId!.Value);

        var props = System.Text.Json.Nodes.JsonNode.Parse(dlRecord!.Properties)!.AsObject();
        Assert.Single(props);
        Assert.Equal(99L, props["originId"]!.GetValue<long>());
    }

    [Fact]
    public async Task PromoteRecords_NoOriginId_WhenSourceRecordIdIsNull()
    {
        var (classId, recId) = await SeedRecordForOriginIdAsync(
            completeExtractionId,
            sourceRecordId: null,
            attributes: """{"role":"transport"}""");

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [classId], RecordIds = [recId] });

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(recId);

        Context.ChangeTracker.Clear();
        var dlRecord = await Context.Records.FindAsync(promoted!.PromotedId!.Value);

        var props = System.Text.Json.Nodes.JsonNode.Parse(dlRecord!.Properties)!.AsObject();
        Assert.False(props.ContainsKey("originId"));
        Assert.Equal("transport", props["role"]!.GetValue<string>());
    }

    [Fact]
    public async Task PromoteRecords_OverwritesLlmOriginId_WithSourceRecordId()
    {
        var (classId, recId) = await SeedRecordForOriginIdAsync(
            completeExtractionId,
            sourceRecordId: 777L,
            attributes: """{"originId":12345,"color":"red"}""");

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [classId], RecordIds = [recId] });

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(recId);

        Context.ChangeTracker.Clear();
        var dlRecord = await Context.Records.FindAsync(promoted!.PromotedId!.Value);

        var props = System.Text.Json.Nodes.JsonNode.Parse(dlRecord!.Properties)!.AsObject();
        Assert.Equal(777L, props["originId"]!.GetValue<long>());
        Assert.Equal("red", props["color"]!.GetValue<string>());
    }

    [Fact]
    public async Task PromoteRecords_DoesNotModifyProperties_WhenExistingKgRecordLinked()
    {
        var sc = new ExtractionClass
        {
            ExtractionId = completeExtractionId,
            Name = "Military Organization",
            OrganizationId = oid,
            ProjectId = pid,
            ValidationStatus = ExtractionValidationStatus.Valid,
            OntologyClassId = cid1
        };
        _latticeCtx.ExtractionClasses.Add(sc);
        await _latticeCtx.SaveChangesAsync();

        var sr = new ExtractionRecord
        {
            ExtractionId = completeExtractionId,
            ExtractionClassId = sc.Id,
            Name = "Test Record",
            OrganizationId = oid,
            ProjectId = pid,
            DataSourceId = dsid,
            ValidationStatus = ExtractionValidationStatus.Valid,
            DeeplynxRecordId = recordId,
            SourceRecordId = 42L,
            Frequency = 1,
            LlmScore = 0.9
        };
        _latticeCtx.ExtractionRecords.Add(sr);
        await _latticeCtx.SaveChangesAsync();

        // Capture properties before promotion
        Context.ChangeTracker.Clear();
        var before = (await Context.Records.FindAsync(recordId))!.Properties;

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [sc.Id], RecordIds = [sr.Id] });

        // The existing KG record's Properties must be untouched
        Context.ChangeTracker.Clear();
        var after = (await Context.Records.FindAsync(recordId))!.Properties;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task PromoteRecords_EmptyJsonProperties_WhenBothSourceRecordIdAndAttributesNull()
    {
        var (classId, recId) = await SeedRecordForOriginIdAsync(
            completeExtractionId,
            sourceRecordId: null,
            attributes: null);

        await _business.PromoteExtraction(uid, oid, pid, completeExtractionId,
            new PromoteExtractionRequestDto { ClassIds = [classId], RecordIds = [recId] });

        _latticeCtx.ChangeTracker.Clear();
        var promoted = _latticeCtx.ExtractionRecords.Find(recId);

        Context.ChangeTracker.Clear();
        var dlRecord = await Context.Records.FindAsync(promoted!.PromotedId!.Value);

        Assert.Equal("{}", dlRecord!.Properties);
    }

    #endregion
}