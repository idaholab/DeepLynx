using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using deeplynx.mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace deeplynx.tests;

[Collection("DeeplynxOutputFormatEnv")]
public class GcfCallToolFilterTests : IDisposable
{
    public GcfCallToolFilterTests() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    public void Dispose() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    private static string RecordArrayJson()
    {
        var rows = Enumerable
            .Range(0, 20)
            .Select(i => new { recordId = 5000 + i, recordName = $"asset-{i}", projectId = 42 })
            .ToList();
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    private static CallToolResult ResultWith(params ContentBlock[] content) =>
        new() { Content = content.ToList() };

    private static string TextOf(CallToolResult r) => ((TextContentBlock)r.Content[0]).Text;

    [Fact]
    public void Transform_Enabled_Rewrites_Single_Json_Block_As_Gcf()
    {
        Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", "gcf");
        var json = RecordArrayJson();

        var outResult = GcfCallToolFilter.Transform(ResultWith(new TextContentBlock { Type = "text", Text = json }));

        Assert.Single(outResult.Content);
        Assert.StartsWith("GCF profile=generic", TextOf(outResult));
    }

    [Fact]
    public void Transform_Disabled_Leaves_Result_Unchanged()
    {
        var json = RecordArrayJson();

        var outResult = GcfCallToolFilter.Transform(ResultWith(new TextContentBlock { Type = "text", Text = json }));

        Assert.Equal(json, TextOf(outResult));
    }

    [Fact]
    public void Transform_Error_Result_Is_Left_As_Json()
    {
        Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", "gcf");
        var json = RecordArrayJson();

        var result = ResultWith(new TextContentBlock { Type = "text", Text = json });
        result.IsError = true;
        var outResult = GcfCallToolFilter.Transform(result);

        Assert.Equal(json, TextOf(outResult));
    }

    [Fact]
    public void Transform_Multiple_Content_Blocks_Are_Left_Unchanged()
    {
        Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", "gcf");
        var json = RecordArrayJson();

        // A JSON text block alongside another block must not be rewritten (would drop the
        // second block).
        var result = ResultWith(
            new TextContentBlock { Type = "text", Text = json },
            new TextContentBlock { Type = "text", Text = "second block" });
        var outResult = GcfCallToolFilter.Transform(result);

        Assert.Equal(2, outResult.Content.Count);
        Assert.Equal(json, TextOf(outResult));
    }
}
