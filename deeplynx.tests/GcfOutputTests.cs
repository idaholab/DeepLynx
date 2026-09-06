using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BlackwellSystems.Gcf;
using deeplynx.mcp;
using Xunit;

namespace deeplynx.tests;

// Mutates the DEEPLYNX_OUTPUT_FORMAT environment variable; the collection keeps these
// tests from racing each other (and any other env-mutating test) in parallel.
[Collection("DeeplynxOutputFormatEnv")]
public class GcfOutputTests : IDisposable
{
    public GcfOutputTests() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    public void Dispose() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    // A representative "get records for a project" result: an array of uniform Record
    // objects, the shape RecordTools returns. The tools serialize with WriteIndented = true,
    // so this mirrors real output; the encoder is measured against it below.
    private static string Records(int n, bool indented = true)
    {
        var rows = Enumerable
            .Range(0, n)
            .Select(i => new
            {
                recordId = 5000 + i,
                recordName = $"asset-{i}",
                description = new[] { "pump", "valve", "sensor", "pipe" }[i % 4] + " component",
                uri = $"deeplynx://project/42/record/{5000 + i}",
                classId = 100 + (i % 8),
                dataSourceId = 12 + (i % 3),
                projectId = 42,
                organizationId = 7,
                originalId = $"SRC-{100000 + i}",
                updatedAt = "2026-09-03T10:20:00Z",
                updatedBy = 200 + (i % 6),
                archived = i % 9 == 0,
                fileType = i % 5 == 0 ? "pdf" : null,
                tagId = 300 + (i % 12),
            })
            .ToList();

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = indented });
    }

    [Fact]
    public void Enabled_Reflects_Environment()
    {
        Assert.False(GcfOutput.Enabled);

        foreach (var value in new[] { "gcf", "GCF", " gcf " })
        {
            Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", value);
            Assert.True(GcfOutput.Enabled);
        }

        Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", "json");
        Assert.False(GcfOutput.Enabled);
    }

    [Fact]
    public void TryEncode_RecordArray_Is_Smaller_And_RoundTrips()
    {
        var json = Records(30);

        var wire = GcfOutput.TryEncode(json);

        Assert.NotNull(wire);
        Assert.StartsWith("GCF profile=generic", wire);
        Assert.True(wire!.Length < json.Length, "GCF wire must be smaller than the JSON");
        // Decoding then re-encoding reproduces the wire (stable, lossless round-trip).
        Assert.Equal(wire, Gcf.EncodeGeneric(Gcf.DecodeGeneric(wire)));
    }

    [Fact]
    public void TryEncode_Decoded_Wire_Carries_Input_Values()
    {
        var wire = GcfOutput.TryEncode(Records(30));
        Assert.NotNull(wire);

        var decoded = Assert.IsType<List<object?>>(Gcf.DecodeGeneric(wire!));
        Assert.Equal(30, decoded.Count);

        var first = Assert.IsType<OrderedMap>(decoded[0]);
        Assert.Equal(5000L, (long)first["recordId"]!);
        Assert.Equal("asset-0", (string?)first["recordName"]);
        Assert.Equal(42L, (long)first["projectId"]!);

        var last = Assert.IsType<OrderedMap>(decoded[29]);
        Assert.Equal(5029L, (long)last["recordId"]!);
        Assert.Equal("asset-29", (string?)last["recordName"]);
    }

    [Fact]
    public void TryEncode_Tiny_Payload_Falls_Back_To_Json()
    {
        var json = JsonSerializer.Serialize(new { status = "ok" });
        Assert.Null(GcfOutput.TryEncode(json)); // GCF not smaller: keep JSON
    }

    [Fact]
    public void TryEncode_Invalid_Json_Falls_Back()
    {
        Assert.Null(GcfOutput.TryEncode("{not json"));
    }

    private static string Numbered(object value, int rows)
    {
        var arr = Enumerable
            .Range(0, rows)
            .Select(_ => new Dictionary<string, object> { ["metric"] = value, ["id"] = 1 })
            .ToList();
        return JsonSerializer.Serialize(arr, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string NumberedRaw(string numberToken, int rows)
    {
        var items = string.Join(",", Enumerable.Range(0, rows).Select(_ => $"{{\"metric\":{numberToken},\"id\":1}}"));
        return $"[{items}]";
    }

    [Fact]
    public void TryEncode_Keeps_16Digit_ShortestRoundTrip_Double()
    {
        // 0.5029000043869019 is exactly representable but 16 significant digits; it must
        // encode (a (decimal)d guard keeps only 15 digits and would wrongly decline it).
        var wire = GcfOutput.TryEncode(Numbered(0.5029000043869019, 20));
        Assert.NotNull(wire);
        Assert.Contains("0.5029000043869019", wire);
    }

    [Fact]
    public void TryEncode_Declines_NonFinite_Double()
    {
        // 1e400 parses to Infinity; the guard must decline rather than encode Infinity.
        Assert.Null(GcfOutput.TryEncode(NumberedRaw("1e400", 20)));
    }

    [Fact]
    public void TryEncode_Declines_High_Precision_Decimal()
    {
        // 33.333333333333333 (17 sig digits) cannot be held by a double without loss.
        Assert.Null(GcfOutput.TryEncode(Numbered(33.333333333333333m, 20)));
    }

    [Fact]
    public void TryEncode_Preserves_Int64_Above_2Pow53()
    {
        var rows = Enumerable.Range(0, 20).Select(_ => new { id = 9007199254740993L, name = "x" }).ToList();
        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });

        var wire = GcfOutput.TryEncode(json);

        Assert.NotNull(wire);
        Assert.Contains("9007199254740993", wire);
        Assert.DoesNotContain("9.007", wire); // not a rounded float
    }
}
