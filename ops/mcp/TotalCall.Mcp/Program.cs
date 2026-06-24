using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TotalCall.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the MCP JSON-RPC stream; all logging must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<RepositoryContext>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "totalcall-ops",
            Title = "TotalCall Operations",
            Version = "0.2.0"
        };
        options.ServerInstructions =
            "Use these tools for local TotalCall operations. Most tools are read-only or "
            + "dry-run only. Write tools require explicit confirmation.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
