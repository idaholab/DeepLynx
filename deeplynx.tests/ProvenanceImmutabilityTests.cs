using deeplynx.business;
using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class ProvenanceImmutabilityTests : IntegrationTestBase
{
    private ProvenanceBusiness _provenanceBusiness = null!;
    private Mock<ILogger<ProvenanceBusiness>> _mockProvLogger = null!;

    public long uid;  // user ID
    public long oid;  // organization ID
    public long pid;  // project ID
    public long did;  // data source ID
    public long rid;  // record ID (has one historical record)

    public ProvenanceImmutabilityTests(TestSuiteFixture fixture) : base(fixture)
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

        var user = new User { Name = "Immutability User", Email = "immutability@test.com", Password = "pw", IsArchived = false };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var org = new Organization { Name = "Immutability Org", LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        var proj = new Project { Name = "Immutability Project", OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.Projects.Add(proj);
        await Context.SaveChangesAsync();
        pid = proj.Id;

        var dataSource = new DataSource { Name = "Immutability Data Source", ProjectId = pid, OrganizationId = oid, LastUpdatedAt = UnspecifiedNow(), LastUpdatedBy = uid };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;

        var record = new datalayer.Models.Record
        {
            Name = "Immutability Record",
            ProjectId = pid,
            OrganizationId = oid,
            DataSourceId = did,
            OriginalId = "immutability-rec-001",
            Description = "",
            Properties = "{}",
            IsArchived = false,
            LastUpdatedAt = UnspecifiedNow(),
            LastUpdatedBy = uid,
            Uri = "/data/org_1/immutability-rec.pdf",
            FileType = "pdf",
            FileSize = 1024,
            FileContentHash = "hash-immutability-rec-v1"
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();
        rid = record.Id;
    }

    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    [Fact]
    public async Task UpdateProvenanceRecord_DirectSql_ThrowsAndIsBlocked()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var record = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);
        var originalChainHash = record.ChainHash;

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE deeplynx.provenance_records SET chain_hash = 'tampered' WHERE id = {record.Id}"));

        Assert.Equal(PostgresErrorCodes.RaiseException, ex.SqlState);
        Assert.Contains("append-only", ex.MessageText);

        Context.ChangeTracker.Clear();
        var unchanged = await Context.ProvenanceRecords.FirstAsync(p => p.Id == record.Id);
        Assert.Equal(originalChainHash, unchanged.ChainHash);
    }

    [Fact]
    public async Task DeleteProvenanceRecord_DirectSql_ThrowsAndIsBlocked()
    {
        await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);
        var record = await Context.ProvenanceRecords.FirstAsync(p => p.RecordId == rid);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM deeplynx.provenance_records WHERE id = {record.Id}"));

        Assert.Equal(PostgresErrorCodes.RaiseException, ex.SqlState);
        Assert.Contains("append-only", ex.MessageText);

        Context.ChangeTracker.Clear();
        Assert.True(await Context.ProvenanceRecords.AnyAsync(p => p.Id == record.Id));
    }

    [Fact]
    public async Task CreateProvenanceRecord_StillSucceeds_WithImmutabilityTriggerActive()
    {
        var result = await _provenanceBusiness.CreateProvenanceRecord(rid, "create-record", uid, null);

        Assert.True(result);
        Assert.True(await Context.ProvenanceRecords.AnyAsync(p => p.RecordId == rid));
    }
}
