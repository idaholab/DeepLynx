using deeplynx.business;
using deeplynx.datalayer.Models;

namespace deeplynx.tests;

public class ProvenanceChainEnvelopeTests
{
    private static ProvenanceRecord MakeRecord(
        long recordId = 1,
        long historicalRecordId = 2,
        long organizationId = 3,
        long projectId = 4,
        string provId = "urn:deeplynx:provenance:abc",
        string? fileContentHash = "hash-v1",
        string provenanceJson = "{\"@id\":\"urn:x\",\"@graph\":[]}")
    {
        return new ProvenanceRecord
        {
            RecordId = recordId,
            HistoricalRecordId = historicalRecordId,
            OrganizationId = organizationId,
            ProjectId = projectId,
            ProvId = provId,
            FileContentHash = fileContentHash,
            ProvenanceJson = provenanceJson
        };
    }

    [Fact]
    public void HashBase64_IsDeterministic_ForIdenticalInput()
    {
        var record = MakeRecord();

        var hash1 = ProvenanceChainEnvelope.HashBase64(record, ProvenanceChainEnvelope.GenesisHash);
        var hash2 = ProvenanceChainEnvelope.HashBase64(record, ProvenanceChainEnvelope.GenesisHash);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashBase64_Differs_WhenPreviousHashDiffers()
    {
        var record = MakeRecord();

        var hashAtGenesis = ProvenanceChainEnvelope.HashBase64(record, ProvenanceChainEnvelope.GenesisHash);
        var hashAtOther = ProvenanceChainEnvelope.HashBase64(record, "some-other-previous-hash");

        Assert.NotEqual(hashAtGenesis, hashAtOther);
    }

    [Fact]
    public void HashBase64_Differs_WhenRecordIdDiffers()
    {
        var recordA = MakeRecord(recordId: 1);
        var recordB = MakeRecord(recordId: 2);

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void HashBase64_Differs_WhenFileContentHashDiffers()
    {
        var recordA = MakeRecord(fileContentHash: "hash-v1");
        var recordB = MakeRecord(fileContentHash: "hash-v2");
        var recordNull = MakeRecord(fileContentHash: null);

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);
        var hashNull = ProvenanceChainEnvelope.HashBase64(recordNull, ProvenanceChainEnvelope.GenesisHash);

        Assert.NotEqual(hashA, hashB);
        Assert.NotEqual(hashA, hashNull);
        Assert.NotEqual(hashB, hashNull);
    }

    [Fact]
    public void HashBase64_Differs_WhenProvenanceJsonContentDiffers()
    {
        var recordA = MakeRecord(provenanceJson: "{\"@id\":\"urn:x\",\"@graph\":[{\"nexus:action\":\"create-record\"}]}");
        var recordB = MakeRecord(provenanceJson: "{\"@id\":\"urn:x\",\"@graph\":[{\"nexus:action\":\"update-record\"}]}");

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void HashBase64_IsInvariant_ToJsonKeyOrder()
    {
        var recordA = MakeRecord(provenanceJson: "{\"a\":1,\"b\":2}");
        var recordB = MakeRecord(provenanceJson: "{\"b\":2,\"a\":1}");

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void HashBase64_IsInvariant_ToNestedObjectKeyOrder()
    {
        var recordA = MakeRecord(provenanceJson: "{\"@graph\":[{\"x\":1,\"y\":{\"nested\":true,\"other\":false}}]}");
        var recordB = MakeRecord(provenanceJson: "{\"@graph\":[{\"y\":{\"other\":false,\"nested\":true},\"x\":1}]}");

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void HashBase64_IsInvariant_ToEquivalentNumberFormatting()
    {
        var recordA = MakeRecord(provenanceJson: "{\"n\":1}");
        var recordB = MakeRecord(provenanceJson: "{\"n\":1.0}");

        var hashA = ProvenanceChainEnvelope.HashBase64(recordA, ProvenanceChainEnvelope.GenesisHash);
        var hashB = ProvenanceChainEnvelope.HashBase64(recordB, ProvenanceChainEnvelope.GenesisHash);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void GenesisHash_IsStableConstant()
    {
        Assert.Equal(Convert.ToBase64String(new byte[32]), ProvenanceChainEnvelope.GenesisHash);
        Assert.Equal(44, ProvenanceChainEnvelope.GenesisHash.Length);
    }
}
