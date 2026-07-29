using System.Net;
using System.Text;
using deeplynx.mcp.helpers;
using deeplynx.mcp.tools;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace deeplynx.tests;

public class McpAuthenticatedHttpClientFactoryTests
{
    [Theory]
    [InlineData("http://localhost:5095", "http://localhost:5095/api/v1/")]
    [InlineData("http://localhost:5095/", "http://localhost:5095/api/v1/")]
    [InlineData("http://localhost:5095/api/v1", "http://localhost:5095/api/v1/")]
    public async Task CreateClientAsync_UsesExplicitV1BaseAddress(
        string configuredUrl,
        string expectedBaseAddress)
    {
        var factory = new AuthenticatedHttpClientFactory(
            AuthenticatedContext(),
            configuredUrl);

        using var client = await factory.CreateClientAsync();

        client.BaseAddress.Should().Be(expectedBaseAddress);
    }

    [Fact]
    public async Task ProjectTool_ResolvesRequestThroughVersionedBaseAddress()
    {
        var handler = new CapturingHandler();
        var factory = new AuthenticatedHttpClientFactory(
            AuthenticatedContext(),
            "http://localhost:5095",
            handler);
        var tools = new ProjectTools(factory);

        await tools.GetAllOrganizations();

        handler.RequestUri.Should().NotBeNull();
        handler.RequestUri!.AbsolutePath.Should().Be("/api/v1/organizations/user");
    }

    [Fact]
    public async Task RecordTool_ResolvesRequestThroughVersionedBaseAddress()
    {
        var handler = new CapturingHandler();
        var factory = new AuthenticatedHttpClientFactory(
            AuthenticatedContext(),
            "http://localhost:5095",
            handler);
        var tools = new RecordTools(factory);

        await tools.GetAllRecords(organizationId: 12, projectId: 34);

        handler.RequestUri.Should().NotBeNull();
        handler.RequestUri!.AbsolutePath.Should()
            .Be("/api/v1/organizations/12/projects/34/records");
    }

    [Fact]
    public void BuildBaseAddress_RejectsEnvironmentOnlyVersionCutover()
    {
        var action = () =>
            AuthenticatedHttpClientFactory.BuildBaseAddress("http://localhost:5095/api/v2");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*pinned to v1*");
    }

    private static HttpContextAccessor AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }
    }
}
