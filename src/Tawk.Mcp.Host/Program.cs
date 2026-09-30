using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using Tawk.Mcp.Clients.Streaming;
using Tawk.Mcp.Host;
using Tawk.Mcp.Managers;

var options = TawkMcpOptionsBinder.Bind(args, Environment.GetEnvironmentVariable);
if (options.Error is not null)
{
    await Console.Error.WriteLineAsync(options.Error);
    return 2;
}

switch (options.Command)
{
    case HostCommand.Help:
        Console.WriteLine(HelpText.Text);
        return 0;
    case HostCommand.Version:
        Console.WriteLine("tawk-mcp " + TawkMcpComposition.Version);
        return 0;
    case HostCommand.PrintToken:
        var store = TawkMcpComposition.CreateTokenStore(options);
        Console.WriteLine(store.GetOrCreate());
        return 0;
    case HostCommand.Healthcheck:
        return await HealthCheckCommand.RunAsync(options, Console.Out);
}

if (options.Transport == TransportKind.Stdio)
{
    // stdout carries the MCP protocol, so every log line goes to stderr.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = false, Args = [] });
    ConfigureLogging(builder.Logging);
    builder.Services.AddTawkMcp(options).WithStdioServerTransport();
    await builder.Build().RunAsync();
    return 0;
}

var tokens = TawkMcpComposition.CreateTokenStore(options);
tokens.GetOrCreate();
var web = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
ConfigureLogging(web.Logging);
// Kestrel listens only where TAWKMCP_BIND and TAWKMCP_PORT say; this replaces any ASPNETCORE_URLS.
web.WebHost.UseUrls(new UriBuilder(Uri.UriSchemeHttp, IPAddress.Parse(options.Bind).ToString(), options.Port).Uri.ToString().TrimEnd('/'));
web.Services.AddSingleton(tokens);
// Sessions carry elicitation, resource subscriptions and channel events, which stateless HTTP cannot.
web.Services.AddTawkMcp(options).WithHttpTransport(http => http.SessionMode = HttpServerSessionMode.Stateful);

var app = web.Build();
app.UseMiddleware<OriginGuardMiddleware>();
app.UseMiddleware<BearerTokenMiddleware>(tokens);
app.MapGet(HealthEndpoint.Path, (ILiveUpdatesManager live) => HealthEndpoint.Handle(live));
var events = new EventStreamEndpoint(
    app.Services.GetRequiredService<IEventStreamHub>(), app.Services.GetRequiredService<ILiveUpdatesManager>(), options.Heartbeat);
app.MapGet(EventStreamEndpoint.Path, events.HandleAsync);
app.MapMcp("/mcp");
await Console.Error.WriteLineAsync($"tawk-mcp {TawkMcpComposition.Version} serving MCP at http://{options.Bind}:{options.Port}/mcp");
await app.RunAsync();
return 0;

static void ConfigureLogging(ILoggingBuilder logging)
{
    logging.ClearProviders();
    logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddFilter("Microsoft", LogLevel.Warning);
    logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
}
