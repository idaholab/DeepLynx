using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using deeplynx.mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace deeplynx.tests;

// End-to-end proof that the GCF filter actually fires in the MCP request pipeline when
// registered the way Program.cs registers it — AddMcpServer(...).WithTools(...)
// .AddCallToolFilter(GcfCallToolFilter.Instance) — rather than only that Transform() works
// in isolation. A real MCP client calls a tool over an in-process stream transport and the
// response text is asserted.
[Collection("DeeplynxOutputFormatEnv")]
public class GcfPipelineTests : IDisposable
{
    public GcfPipelineTests() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    public void Dispose() => Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", null);

    [McpServerToolType]
    public class PipelineTools
    {
        // Mirrors the real tools: returns a stringified JSON array of uniform records.
        [McpServerTool(Name = "records")]
        public static string Records() =>
            JsonSerializer.Serialize(
                Enumerable.Range(0, 20).Select(i => new { recordId = 5000 + i, recordName = $"asset-{i}", projectId = 42 }).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
    }

    private static async Task<string> CallRecordsAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var services = new ServiceCollection();
        services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithTools<PipelineTools>()
            .AddCallToolFilter(GcfCallToolFilter.Instance);

        await using var provider = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var hosted = provider.GetServices<IHostedService>().ToList();
        foreach (var service in hosted)
            await service.StartAsync(cts.Token);

        try
        {
            var transport = new StreamClientTransport(
                serverInput: clientToServer.Writer.AsStream(),
                serverOutput: serverToClient.Reader.AsStream());
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

            var result = await client.CallToolAsync(
                "records",
                new Dictionary<string, object?>(),
                cancellationToken: cts.Token);

            return ((TextContentBlock)result.Content[0]).Text;
        }
        finally
        {
            foreach (var service in hosted)
                await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Filter_Rewrites_Tool_Result_As_Gcf_When_Enabled()
    {
        Environment.SetEnvironmentVariable("DEEPLYNX_OUTPUT_FORMAT", "gcf");

        var text = await CallRecordsAsync();

        Assert.StartsWith("GCF profile=generic", text);
    }

    [Fact]
    public async Task Tool_Result_Stays_Json_When_Disabled()
    {
        var text = await CallRecordsAsync();

        Assert.DoesNotContain("GCF profile=generic", text);
        Assert.Contains("recordName", text); // still the JSON the tool returned
    }
}
