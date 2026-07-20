using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class ProvenanceBusinessTests : IntegrationTestBase
{
    private ProvenanceBusiness _provenanceBusiness = null!;
    private Mock<ILogger<ProvenanceBusiness>> _mockProvLogger = null!;

    public long uid;  // user ID
    public long oid;  // organization ID
    public long pid;  // project ID
    public long did;  // data source ID
    public long rid;  // record ID (has one historical record)
    public long rid2; // record ID (has multiple historical records, for ordering tests)
    public long rid3; // record ID (no historical record)
    public long mcid;  // ai model config ID

    public long histId1;  // historical record for rid
    public long histId2Old; // older historical record for rid2
    public long histId2New; // newer historical record for rid2

    public ProvenanceBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockProvLogger = new Mock<ILogger<ProvenanceBusiness>>();
        _provenanceBusiness = new ProvenanceBusiness(Context, _mockProvLogger.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User { Name = "Provenance User", Email = "provenance@test.com", Password = "pw", IsArchived = false };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var org = new Organization { Name = "Provenance Org", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var proj = new Project { Name = "Provenance Project", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();
        pid = proj.Id;

        var ds = new DataSource { Name = "Provenance DS", ProjectId = pid, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();
        did = ds.Id;

        var record1 = new datalayer.Models.Record
        {
            Name = "Record 1",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = did,
            OriginalId = "rec-001",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/data/org_1/rec1.pdf",
            FileType = "pdf",
            FileSize = 1024,
            FileContentHash = "hash-rec1-v1"
        };
        var record2 = new datalayer.Models.Record
        {
            Name = "Record 2",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = did,
            OriginalId = "rec-002",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/data/org_1/rec2.pdf",
            FileType = "pdf",
            FileSize = 2048,
            FileContentHash = "hash-rec2-v1"
        };
        var record3 = new datalayer.Models.Record
        {
            Name = "Record 3 - No History",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = did,
            OriginalId = "rec-003",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/data/org_1/rec3.pdf",
            FileType = "pdf",
            FileSize = 512
        };
        Context.Records.AddRange(record1, record2, record3);
        await Context.SaveChangesAsync();
        rid = record1.Id;
        rid2 = record2.Id;
        rid3 = record3.Id;

        // NOTE: deeplynx.records has an AFTER INSERT/UPDATE trigger that automatically
        // creates a matching historical_records row. We don't create these manually -
        // instead we drive the desired history via inserts/updates on the Record itself
        // and then read back whatever the trigger produced.

        // rid: a single historical record, created by the initial insert above.
        var hist1 = await Context.HistoricalRecords
            .Where(h => h.RecordId == rid)
            .OrderByDescending(h => h.Id)
            .FirstAsync();
        histId1 = hist1.Id;

        // rid2: the insert above created an initial ("old") historical record.
        // Updating the record then triggers a second ("new") historical record,
        // which should always be treated as the latest.
        var hist2Old = await Context.HistoricalRecords
            .Where(h => h.RecordId == rid2)
            .OrderByDescending(h => h.Id)
            .FirstAsync();
        histId2Old = hist2Old.Id;

        record2.FileContentHash = "hash-rec2-v2";
        record2.LastUpdatedAt = UnspecifiedNow();
        await Context.SaveChangesAsync();

        var hist2New = await Context.HistoricalRecords
            .Where(h => h.RecordId == rid2)
            .OrderByDescending(h => h.Id)
            .FirstAsync();
        histId2New = hist2New.Id;

        // rid3: represents a record whose historical trail has been wiped out
        // (e.g. after a delete). The insert above auto-created a historical record,
        // so we remove it here to simulate the "no historical record" edge case.
        var autoCreatedForRid3 = await Context.HistoricalRecords
            .Where(h => h.RecordId == rid3)
            .ToListAsync();
        Context.HistoricalRecords.RemoveRange(autoCreatedForRid3);
        await Context.SaveChangesAsync();

        var modelConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://api.openai.com",
            ModelProvider = "openai",
            ModelName = "text-embedding-3-large",
            ModelType = "embedding",
            RequiresToken = true,
            Default = true,
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(modelConfig);
        await Context.SaveChangesAsync();
        mcid = modelConfig.Id;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // =========================================================================
    // GetProvenanceRecord Tests
    // =========================================================================

    #region GetProvenanceRecord Tests

    [Fact]
    public async Task GetProvenanceRecord_ReturnsCorrectFields_WhenExists()
    {
        var created = await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        Assert.True(created);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        var result = await _provenanceBusiness.GetProvenanceRecord(provenanceRecord.Id);

        Assert.Equal(provenanceRecord.Id, result.Id);
        Assert.Equal(rid, result.RecordId);
        Assert.Equal(histId1, result.HistoricalRecordId);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(provenanceRecord.ProvId, result.ProvId);
        Assert.Equal(provenanceRecord.ProvenanceJson, result.ProvenanceJson);
        Assert.Equal("hash-rec1-v1", result.FileContentHash);
        Assert.Null(result.Signature);
        Assert.Equal(provenanceRecord.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public async Task GetProvenanceRecord_Throws_WhenNotFound()
    {
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _provenanceBusiness.GetProvenanceRecord(999999L));

        Assert.Contains("Provenance record with id 999999 not found", ex.Message);
    }

    #endregion

    // =========================================================================
    // GetProvenanceHistory Tests
    // =========================================================================

    #region GetProvenanceHistory Tests

    [Fact]
    public async Task GetProvenanceHistory_Throws_WhenRecordNotFound()
    {
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _provenanceBusiness.GetProvenanceHistory(999999L));

        Assert.Contains("Record with id 999999 not found", ex.Message);
    }

    [Fact]
    public async Task GetProvenanceHistory_ReturnsMessageAndEmptyList_WhenNoHistoryExists()
    {
        var result = await _provenanceBusiness.GetProvenanceHistory(rid);

        Assert.Empty(result.Records);
        Assert.Contains($"No provenance history exists yet for record {rid}", result.Message);
    }

    [Fact]
    public async Task GetProvenanceHistory_ReturnsRecordsNewestFirst()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await Task.Delay(10);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);

        var result = await _provenanceBusiness.GetProvenanceHistory(rid);

        Assert.Equal(2, result.Records.Count);
        Assert.True(result.Records[0].CreatedAt >= result.Records[1].CreatedAt);

        var json0 = result.Records[0].ProvenanceJson!;
        Assert.Contains("update-record", json0);
    }

    [Fact]
    public async Task GetProvenanceHistory_OnlyReturnsRecordsForRequestedRecordId()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid2, "create-record", uid, null);

        var result = await _provenanceBusiness.GetProvenanceHistory(rid);

        Assert.Single(result.Records);
        Assert.Equal(rid, result.Records[0].RecordId);
    }

    #endregion

    // =========================================================================
    // CreateProvenanceRecord Tests
    // =========================================================================

    #region CreateProvenanceRecord Tests

    [Fact]
    public async Task CreateProvenanceRecord_ReturnsFalse_WhenNoHistoricalRecordExists()
    {
        var result = await _provenanceBusiness.CreateProvenanceRecord(rid3, "create-record", uid, null);

        Assert.False(result);

        var count = await Context.ProvenanceRecords.CountAsync(p => p.RecordId == rid3);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateProvenanceRecord_Success_PersistsExpectedFields()
    {
        var result = await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        Assert.True(result);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.Equal(rid, provenanceRecord.RecordId);
        Assert.Equal(histId1, provenanceRecord.HistoricalRecordId);
        Assert.Equal(oid, provenanceRecord.OrganizationId);
        Assert.Equal(pid, provenanceRecord.ProjectId);
        Assert.Equal("hash-rec1-v1", provenanceRecord.FileContentHash);
        Assert.Null(provenanceRecord.Signature);
        Assert.False(string.IsNullOrWhiteSpace(provenanceRecord.ProvId));
        Assert.StartsWith("urn:deeplynx:provenance:", provenanceRecord.ProvId);
        Assert.False(string.IsNullOrWhiteSpace(provenanceRecord.ProvenanceJson));
    }

    [Fact]
    public async Task CreateProvenanceRecord_Success_UsesLatestHistoricalRecord()
    {
        var result = await _provenanceBusiness.CreateProvenanceRecord(rid2, "update-record", uid, null);

        Assert.True(result);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid2);

        // The latest historical record for rid2 (by Id) should be hist2New, not hist2Old
        Assert.Equal(histId2New, provenanceRecord.HistoricalRecordId);
        Assert.Equal("hash-rec2-v2", provenanceRecord.FileContentHash);
    }

    [Fact]
    public async Task CreateProvenanceRecord_ProvenanceJson_ContainsExpectedActionAndRecordUrns()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "archive-record", uid, null);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.Contains("archive-record", provenanceRecord.ProvenanceJson);
        Assert.Contains($"urn:deeplynx:record:{rid}", provenanceRecord.ProvenanceJson);
        Assert.Contains($"urn:deeplynx:historical-record:{histId1}", provenanceRecord.ProvenanceJson);
        Assert.Contains($"urn:deeplynx:user:{uid}", provenanceRecord.ProvenanceJson);
    }

    [Fact]
    public async Task CreateProvenanceRecord_ProvenanceJson_OmitsEmbeddingSection_WhenNoAiConfigProvided()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.DoesNotContain("embedding_generation", provenanceRecord.ProvenanceJson);
        Assert.DoesNotContain("ai_model", provenanceRecord.ProvenanceJson);
    }

    [Fact]
    public async Task CreateProvenanceRecord_ProvenanceJson_IncludesAiModelInfo_WhenAiConfigProvided()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "request-embedding", uid, mcid);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.Contains("embedding_generation", provenanceRecord.ProvenanceJson);
        Assert.Contains("openai", provenanceRecord.ProvenanceJson);
        Assert.Contains("text-embedding-3-large", provenanceRecord.ProvenanceJson);
    }

    [Fact]
    public async Task CreateProvenanceRecord_Success_WhenAiConfigIdDoesNotExist()
    {
        // aiConfigId is provided but doesn't resolve to a real config; should not throw,
        // and should simply omit the AI-specific fields from the graph.
        var result = await _provenanceBusiness.CreateProvenanceRecord(rid, "request-embedding", uid, 999999L);

        Assert.True(result);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);
        Assert.Contains("embedding_generation", provenanceRecord.ProvenanceJson);
        Assert.DoesNotContain("openai", provenanceRecord.ProvenanceJson);
    }

    [Fact]
    public async Task CreateProvenanceRecord_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow;

        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.True(provenanceRecord.CreatedAt >= DateTime.SpecifyKind(before, DateTimeKind.Unspecified).AddSeconds(-5));
    }

    [Fact]
    public async Task CreateProvenanceRecord_AllowsMultipleRecords_ForSameRecordId()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);

        var count = await Context.ProvenanceRecords.CountAsync(p => p.RecordId == rid);
        Assert.Equal(2, count);
    }

    #endregion

    // =========================================================================
    // BulkCreateProvenanceRecords Tests
    // =========================================================================

    #region BulkCreateProvenanceRecords Tests

    [Fact]
    public async Task BulkCreateProvenanceRecords_ReturnsTrue_WhenRecordIdsIsNull()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(null!, "attach-tag", uid, null);

        Assert.True(result);
        Assert.Equal(0, await Context.ProvenanceRecords.CountAsync());
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_ReturnsTrue_WhenRecordIdsIsEmpty()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(new List<long>(), "attach-tag", uid, null);

        Assert.True(result);
        Assert.Equal(0, await Context.ProvenanceRecords.CountAsync());
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_ReturnsFalse_WhenAnyRecordHasNoHistoricalRecord()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid3], "attach-tag", uid, null);

        Assert.False(result);

        // Nothing should have been persisted since the whole batch is rejected
        Assert.Equal(0, await Context.ProvenanceRecords.CountAsync());
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_Success_CreatesRecordForEachDistinctId()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid2], "attach-tag", uid, null);

        Assert.True(result);

        var created = await Context.ProvenanceRecords.ToListAsync();
        Assert.Equal(2, created.Count);
        Assert.Contains(created, p => p.RecordId == rid);
        Assert.Contains(created, p => p.RecordId == rid2);
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_Success_DeduplicatesRepeatedRecordIds()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid, rid], "attach-tag", uid, null);

        Assert.True(result);

        var count = await Context.ProvenanceRecords.CountAsync(p => p.RecordId == rid);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_Success_UsesLatestHistoricalRecordPerRecordId()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid2], "update-record", uid, null);

        Assert.True(result);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid2);
        Assert.Equal(histId2New, provenanceRecord.HistoricalRecordId);
        Assert.Equal("hash-rec2-v2", provenanceRecord.FileContentHash);
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_ProvenanceJson_IncludesAiModelInfo_WhenAiConfigProvided()
    {
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid2], "request-embedding", uid, mcid);

        Assert.True(result);

        var records = await Context.ProvenanceRecords.ToListAsync();
        Assert.All(records, r => Assert.Contains("openai", r.ProvenanceJson));
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_UsesSameActionAndActor_ForAllRecords()
    {
        await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid2], "detach-label", uid, null);

        var records = await Context.ProvenanceRecords.ToListAsync();
        Assert.All(records, r => Assert.Contains("detach-label", r.ProvenanceJson));
        Assert.All(records, r => Assert.Contains($"urn:deeplynx:user:{uid}", r.ProvenanceJson));
    }

    #endregion
}