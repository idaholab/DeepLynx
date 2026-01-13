// Program.cs
using deeplynx.mcp.helpers;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// load environment variables
Env.Load();

// used to access request headers in tools
builder.Services.AddHttpContextAccessor();

// register the authed HTTP client factory as scoped (per-request)
builder.Services.AddScoped<IAuthenticatedHttpClientFactory, AuthenticatedHttpClientFactory>();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

var mcp_server_url = 
    Environment.GetEnvironmentVariable("MCP_SERVER_URL") 
    ?? "http://0.0.0.0:43656";
app.Run(mcp_server_url);
