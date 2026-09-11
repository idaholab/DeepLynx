using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.exceptions;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class ProvenanceBusinessTests : IntegrationTestBase
{
    private readonly TestSuiteFixture _fixture;
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
        _fixture = fixture;
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
    // GetProjectProvenanceHistory Tests
    // =========================================================================

    #region GetProjectProvenanceHistory Tests

    [Fact]
    public async Task GetProjectProvenanceHistory_Throws_WhenProjectNotFound()
    {
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _provenanceBusiness.GetProjectProvenanceHistory(999999L, new PaginatedRequestDto()));

        Assert.Contains("Project with id 999999 not found", ex.Message);
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_ReturnsEmptyPaginatedResponse_WhenNoProvenanceExists()
    {
        var history = await _provenanceBusiness.GetProjectProvenanceHistory(pid, new PaginatedRequestDto());

        Assert.Empty(history.Items);
        Assert.Equal(0, history.TotalCount);
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_IncludesProvenance_ForDeletedRecord()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        // provenance_records no longer cascade-deletes with its record (dropped FKs), so the
        // record can be removed while its provenance trail stays put.
        var record = await Context.Records.FirstAsync(r => r.Id == rid);
        Context.Records.Remove(record);
        await Context.SaveChangesAsync();

        Assert.False(await Context.Records.AnyAsync(r => r.Id == rid));

        var history = await _provenanceBusiness.GetProjectProvenanceHistory(pid, new PaginatedRequestDto());

        Assert.Contains(history.Items, p => p.Id == provenanceRecord.Id);
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_ExcludesProvenance_FromOtherProjects()
    {
        var otherProject = new Project
        {
            Name = "Other Project", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid
        };
        Context.Projects.Add(otherProject);
        await Context.SaveChangesAsync();

        var otherDataSource = new DataSource
        {
            Name = "Other DS", ProjectId = otherProject.Id, OrganizationId = oid,
            LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid
        };
        Context.DataSources.Add(otherDataSource);
        await Context.SaveChangesAsync();

        var otherRecord = new datalayer.Models.Record
        {
            Name = "Other Project Record",
            ProjectId = otherProject.Id,
            OrganizationId = oid,
            DataSourceId = otherDataSource.Id,
            OriginalId = "other-rec-001",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/data/org_1/other.pdf",
            FileType = "pdf",
            FileSize = 256,
            FileContentHash = "hash-other-v1"
        };
        Context.Records.Add(otherRecord);
        await Context.SaveChangesAsync();

        await _provenanceBusiness.CreateProvenanceRecord(otherRecord.Id, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        var history = await _provenanceBusiness.GetProjectProvenanceHistory(pid, new PaginatedRequestDto());

        Assert.Single(history.Items);
        Assert.All(history.Items, p => Assert.Equal(pid, p.ProjectId));
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_OrdersNewestFirst_WithIdTiebreaker()
    {
        // BulkCreateProvenanceRecords stamps one shared CreatedAt across the whole batch, so
        // CreatedAt alone can't order these two rows deterministically - the query needs the
        // Id tiebreaker to avoid skipping/duplicating rows across pages.
        var result = await _provenanceBusiness.BulkCreateProvenanceRecords([rid, rid2], "attach-tag", uid, null);
        Assert.True(result);

        var created = await Context.ProvenanceRecords.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, created.Count);

        var history = await _provenanceBusiness.GetProjectProvenanceHistory(pid, new PaginatedRequestDto());

        Assert.Equal(2, history.Items.Count);
        Assert.Equal(created[1].Id, history.Items[0].Id);
        Assert.Equal(created[0].Id, history.Items[1].Id);
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_Paginates_AcrossMultiplePages()
    {
        for (var i = 0; i < 5; i++)
        {
            await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
            await Task.Delay(10);
        }

        var page1 = await _provenanceBusiness.GetProjectProvenanceHistory(
            pid, new PaginatedRequestDto { PageNumber = 1, PageSize = 2 });
        var page2 = await _provenanceBusiness.GetProjectProvenanceHistory(
            pid, new PaginatedRequestDto { PageNumber = 2, PageSize = 2 });
        var page3 = await _provenanceBusiness.GetProjectProvenanceHistory(
            pid, new PaginatedRequestDto { PageNumber = 3, PageSize = 2 });

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Single(page3.Items);

        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(p => p.Id).ToList();
        Assert.Equal(allIds.Distinct().Count(), allIds.Count);
    }

    [Fact]
    public async Task GetProjectProvenanceHistory_ReturnsAllRows_WhenPageSizeIsNegativeOne()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await Task.Delay(10);
        await _provenanceBusiness.CreateProvenanceRecord(rid2, "create-record", uid, null);

        var history = await _provenanceBusiness.GetProjectProvenanceHistory(
            pid, new PaginatedRequestDto { PageSize = -1 });

        Assert.Equal(2, history.Items.Count);
        Assert.Equal(2, history.TotalCount);
    }

    #endregion

    // =========================================================================
    // VerifyProvenanceChain Tests
    // =========================================================================

    #region VerifyProvenanceChain Tests

    /// <summary>
    ///     provenance_records is append-only (block_provenance_mutation_trigger). Bypassing it
    ///     with session_replication_role, exactly like IntegrationTestBase.CleanDatabaseAsync and
    ///     ProvenanceImmutabilityTests do, is the only way to simulate tampering for these tests.
    ///     The column name is only ever a hardcoded literal from a call site below (never user
    ///     input), so it's safe to splice directly; the new value is passed as a real parameter.
    /// </summary>
    private async Task TamperProvenanceRecordAsync(long provenanceRecordId, string column, string newValue)
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        await Context.Database.ExecuteSqlRawAsync("SET session_replication_role = replica;");
        await Context.Database.ExecuteSqlRawAsync(
            $"UPDATE deeplynx.provenance_records SET {column} = @newValue WHERE id = @id",
            new NpgsqlParameter("@newValue", newValue),
            new NpgsqlParameter("@id", provenanceRecordId));
        await Context.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT;");
        await transaction.CommitAsync();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task VerifyProvenanceChain_Throws_WhenRecordNotFound()
    {
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _provenanceBusiness.VerifyProvenanceChain(999999L));

        Assert.Contains("Record with id 999999 not found", ex.Message);
    }

    [Fact]
    public async Task VerifyProvenanceChain_ReturnsValid_WithNoChainedHistory()
    {
        var result = await _provenanceBusiness.VerifyProvenanceChain(rid);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.RecordsVerified);
        Assert.Contains($"No chained provenance records exist for record {rid}", result.Message);
    }

    [Fact]
    public async Task VerifyProvenanceChain_ReturnsValid_ForUntamperedChain()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);

        var result = await _provenanceBusiness.VerifyProvenanceChain(rid);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.RecordsVerified);
        Assert.Null(result.FirstInvalidProvenanceRecordId);
    }

    [Fact]
    public async Task VerifyProvenanceChain_VerifiesFromGenesis_ForFirstRecord()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var first = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, first.PreviousHash);

        var result = await _provenanceBusiness.VerifyProvenanceChain(rid);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.RecordsVerified);
    }

    [Fact]
    public async Task VerifyProvenanceChain_DetectsTampering_WhenChainHashAltered()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);
        var records = await Context.ProvenanceRecords.OrderBy(p => p.Id).ToListAsync();
        var target = records[0];
        var originalChainHash = target.ChainHash!;

        await TamperProvenanceRecordAsync(target.Id, "chain_hash", "tampered-chain-hash");

        var result = await _provenanceBusiness.VerifyProvenanceChain(rid);

        Assert.False(result.IsValid);
        Assert.Equal(target.Id, result.FirstInvalidProvenanceRecordId);
        Assert.Equal("tampered-chain-hash", result.ActualChainHash);
        Assert.Equal(originalChainHash, result.ExpectedChainHash);
    }

    [Fact]
    public async Task VerifyProvenanceChain_DetectsTampering_WhenEnvelopeContentAltered()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var target = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        await TamperProvenanceRecordAsync(target.Id, "file_content_hash", "tampered-file-hash");

        var result = await _provenanceBusiness.VerifyProvenanceChain(rid);

        Assert.False(result.IsValid);
        Assert.Equal(target.Id, result.FirstInvalidProvenanceRecordId);
        Assert.Equal(target.ChainHash, result.ActualChainHash);
        Assert.NotEqual(result.ActualChainHash, result.ExpectedChainHash);
    }

    [Fact]
    public async Task VerifyProvenanceChain_ResumesFromCheckpoint_SkippingEarlierRecords()
    {
        for (var i = 0; i < 4; i++)
            await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);

        var records = await Context.ProvenanceRecords.OrderBy(p => p.Id).ToListAsync();
        var checkpoint = records[1];

        var result = await _provenanceBusiness.VerifyProvenanceChain(rid, checkpoint.Id);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.RecordsVerified);
        Assert.Equal(checkpoint.Id, result.CheckpointRecordId);
    }

    [Fact]
    public async Task VerifyProvenanceChain_Throws_WhenCheckpointBelongsToDifferentRecord()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid2, "create-record", uid, null);
        var otherChainRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid2);

        var ex = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _provenanceBusiness.VerifyProvenanceChain(rid, otherChainRecord.Id));

        Assert.Contains($"Checkpoint provenance record {otherChainRecord.Id}", ex.Message);
    }

    [Fact]
    public async Task VerifyProvenanceChain_IgnoresOtherRecordsChain()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid2, "create-record", uid, null);
        var ridRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        await TamperProvenanceRecordAsync(ridRecord.Id, "chain_hash", "tampered-chain-hash");

        var ridResult = await _provenanceBusiness.VerifyProvenanceChain(rid);
        var rid2Result = await _provenanceBusiness.VerifyProvenanceChain(rid2);

        Assert.False(ridResult.IsValid);
        Assert.True(rid2Result.IsValid);
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

    // =========================================================================
    // Chain Hash Tests
    // =========================================================================

    #region Chain Hash Tests

    [Fact]
    public async Task CreateProvenanceRecord_FirstRecordForRecordId_GetsGenesisHash()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        var provenanceRecord = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, provenanceRecord.PreviousHash);
        Assert.False(string.IsNullOrWhiteSpace(provenanceRecord.ChainHash));
    }

    [Fact]
    public async Task CreateProvenanceRecord_SecondRecordForSameRecordId_ChainsToFirst()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var first = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        await _provenanceBusiness.CreateProvenanceRecord(rid, "update-record", uid, null);
        var second = await Context.ProvenanceRecords
            .Where(p => p.RecordId == rid && p.Id != first.Id)
            .FirstAsync();

        Assert.Equal(first.ChainHash, second.PreviousHash);
        Assert.NotEqual(first.ChainHash, second.ChainHash);
    }

    [Fact]
    public async Task CreateProvenanceRecord_DifferentRecordIds_GetIndependentChains()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        await _provenanceBusiness.CreateProvenanceRecord(rid2, "update-record", uid, null);

        var provRid = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);
        var provRid2 = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid2);

        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, provRid.PreviousHash);
        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, provRid2.PreviousHash);
        Assert.NotEqual(provRid.ChainHash, provRid2.ChainHash);
    }

    [Fact]
    public async Task CreateProvenanceRecord_DoesNotLinkTo_PreExistingLegacyRowWithNoChainHash()
    {
        // simulate a pre-chain legacy row: no PreviousHash/ChainHash set
        var legacyRow = new datalayer.Models.ProvenanceRecord
        {
            RecordId = rid,
            HistoricalRecordId = histId1,
            OrganizationId = oid,
            ProjectId = pid,
            ProvId = "urn:deeplynx:provenance:legacy",
            FileContentHash = "hash-rec1-v1",
            ProvenanceJson = "{\"@id\":\"urn:deeplynx:provenance:legacy\",\"@graph\":[]}",
            Signature = null,
            PreviousHash = null,
            ChainHash = null,
            CreatedAt = UnspecifiedNow()
        };
        Context.ProvenanceRecords.Add(legacyRow);
        await Context.SaveChangesAsync();

        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        var newRow = await Context.ProvenanceRecords
            .Where(p => p.RecordId == rid && p.Id != legacyRow.Id)
            .FirstAsync();

        // starts a fresh chain at genesis rather than linking to the legacy row
        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, newRow.PreviousHash);
    }

    [Fact]
    public async Task BulkCreateProvenanceRecords_ChainsEachRecordIndependently()
    {
        // rid already has a chained record; rid2 does not yet
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var existingForRid = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        var result = await _provenanceBusiness.BulkCreateProvenanceRecords(
            [rid, rid2], "attach-tag", uid, null);

        Assert.True(result);

        var newForRid = await Context.ProvenanceRecords
            .Where(p => p.RecordId == rid && p.Id != existingForRid.Id)
            .FirstAsync();
        var newForRid2 = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid2);

        Assert.Equal(existingForRid.ChainHash, newForRid.PreviousHash);
        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, newForRid2.PreviousHash);
    }

    [Fact]
    public async Task CreateProvenanceRecord_ConcurrentCallsForSameRecordId_DoNotForkTheChain()
    {
        // DbContext isn't thread-safe, so exercising the real retry-on-conflict path
        // requires two independent contexts/business instances, same as two concurrent
        // requests each getting their own scoped DbContext in production.
        await using var contextB = new datalayer.Models.DeeplynxContext(
            new DbContextOptionsBuilder<datalayer.Models.DeeplynxContext>()
                .UseNpgsql(_fixture.PostgresDataSource, o => o.UseVector())
                .Options);
        var provenanceBusinessB = new ProvenanceBusiness(contextB, _mockProvLogger.Object);

        var results = await Task.WhenAll(
            _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null),
            provenanceBusinessB.CreateProvenanceRecord(rid, "update-record", uid, null));

        Assert.All(results, Assert.True);

        var rows = await Context.ProvenanceRecords
            .Where(p => p.RecordId == rid)
            .OrderBy(p => p.Id)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(ProvenanceChainEnvelope.GenesisHash, rows[0].PreviousHash);
        Assert.Equal(rows[0].ChainHash, rows[1].PreviousHash);
        Assert.NotEqual(rows[0].ChainHash, rows[1].ChainHash);
    }

    [Fact]
    public async Task DuplicatePreviousHash_ForSameRecordId_ViolatesUniqueConstraint()
    {
        Context.ProvenanceRecords.Add(new datalayer.Models.ProvenanceRecord
        {
            RecordId = rid,
            HistoricalRecordId = histId1,
            OrganizationId = oid,
            ProjectId = pid,
            ProvId = "urn:deeplynx:provenance:dup-1",
            ProvenanceJson = "{\"@id\":\"urn:deeplynx:provenance:dup-1\",\"@graph\":[]}",
            PreviousHash = ProvenanceChainEnvelope.GenesisHash,
            ChainHash = "chain-hash-1",
            CreatedAt = UnspecifiedNow()
        });
        await Context.SaveChangesAsync();

        Context.ProvenanceRecords.Add(new datalayer.Models.ProvenanceRecord
        {
            RecordId = rid,
            HistoricalRecordId = histId1,
            OrganizationId = oid,
            ProjectId = pid,
            ProvId = "urn:deeplynx:provenance:dup-2",
            ProvenanceJson = "{\"@id\":\"urn:deeplynx:provenance:dup-2\",\"@graph\":[]}",
            PreviousHash = ProvenanceChainEnvelope.GenesisHash,
            ChainHash = "chain-hash-2",
            CreatedAt = UnspecifiedNow()
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    #endregion
}