using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients;
using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Clients.Prompts;
using Tawk.Mcp.Clients.Protocol;
using Tawk.Mcp.Clients.Resources;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Streaming;
using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Host;

/// <summary>The composition root: every interface is bound to its implementation here and nowhere else.</summary>
public static class TawkMcpComposition
{
    public static string Version { get; } =
        typeof(TawkMcpComposition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.1.0";

    public static IMcpServerBuilder AddTawkMcp(this IServiceCollection services, TawkMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        services.TryAddSingleton(TimeProvider.System);

        // Resource access.
        services.AddSingleton<IControlSocketLocator>(new ControlSocketLocator(options.SocketPath, Environment.GetEnvironmentVariable, home));
        services.AddSingleton<ControlLineCodec>();
        services.AddSingleton(new TawkControlOptions
        {
            Version = Version,
            RequestTimeout = TimeSpan.FromSeconds(options.RequestTimeoutS),
        });
        services.AddSingleton<ConnectSignal>();
        services.AddSingleton<ISocketFileWatcher, SocketFileWatcher>();
        services.AddSingleton<IDelay, TimeProviderDelay>();
        services.AddSingleton<UnixSocketTawkControl>();
        services.AddSingleton<ITawkControl>(sp => sp.GetRequiredService<UnixSocketTawkControl>());
        services.AddSingleton<ITawkConnector>(sp => sp.GetRequiredService<UnixSocketTawkControl>());
        services.AddSingleton<IConfirmationGate, ConfirmationGate>();
        services.AddHostedService<TawkConnectionSupervisor>();

        // Engines.
        services.AddSingleton<ICircuitBreaker>(sp => new CircuitBreaker(
            options.BreakerThreshold, TimeSpan.FromSeconds(options.BreakerCooldownS), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IBackoffPolicy>(new ExponentialBackoffPolicy(
            TimeSpan.FromMilliseconds(options.BackoffInitialMs), TimeSpan.FromMilliseconds(options.BackoffMaxMs), Random.Shared.NextDouble));
        services.AddSingleton<ITranscriptFormatter, TranscriptFormatter>();
        services.AddSingleton<IChatDirectoryFormatter, ChatDirectoryFormatter>();
        services.AddSingleton<IUntrustedTextFence, UntrustedTextFence>();
        services.AddSingleton<ICatchUpPlanner, CatchUpPlanner>();
        services.AddSingleton<IDraftReplyPlanner, DraftReplyPlanner>();
        services.AddSingleton<INotificationFormatter, NotificationFormatter>();

        // Managers.
        services.AddSingleton<IChatReadingManager, ChatReadingManager>();
        services.AddSingleton<IMessageSendingManager, MessageSendingManager>();
        services.AddSingleton<IMessageManagementManager, MessageManagementManager>();
        services.AddSingleton<IChatManagementManager, ChatManagementManager>();
        services.AddSingleton<IScheduleManagementManager, ScheduleManagementManager>();
        services.AddSingleton<IStatusManager, StatusManager>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<ISettingsManager, SettingsManager>();
        services.AddSingleton<IAppManager, AppManager>();
        services.AddSingleton<ILiveUpdatesManager, LiveUpdatesManager>();

        // Clients.
        var sessions = new ClientSessionRegistry();
        services.AddSingleton<IClientSessionRegistry>(sessions);
        services.AddSingleton<IResourceSubscriptionRegistry, ResourceSubscriptionRegistry>();
        services.AddSingleton(new ChannelOptions(options.Channel));
        services.AddSingleton<EventStreamHub>();
        services.AddSingleton<IEventStreamHub>(sp => sp.GetRequiredService<EventStreamHub>());
        services.AddSingleton<IEventSink, ResourceUpdatePump>();
        services.AddSingleton<IEventSink, ChannelEventSink>();
        services.AddSingleton<IEventSink>(sp => sp.GetRequiredService<EventStreamHub>());
        services.AddHostedService<NotificationDispatcher>();

        return services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation { Name = "tawk-mcp", Title = "tawk", Version = Version };
                server.ServerInstructions = TawkServerInstructions.Text
                    + (options.Channel == ChannelMode.Off ? string.Empty : TawkServerInstructions.Channel);
                if (options.Channel != ChannelMode.Off)
                {
                    server.Capabilities ??= new ServerCapabilities();
                    server.Capabilities.Experimental ??= new Dictionary<string, object>();
                    server.Capabilities.Experimental[ChannelOptions.Capability] = new JsonObject();
                }
            })
            .WithTools<ChatTools>()
            .WithTools<MessageTools>()
            .WithTools<ScheduleTools>()
            .WithTools<StatusTools>()
            .WithTools<ProfileTools>()
            .WithTools<SettingsTools>()
            .WithTools<AppTools>()
            .WithResources<ChatResources>()
            .WithPrompts<TawkPrompts>()
            .WithSubscribeToResourcesHandler(ResourceSubscriptionHandlers.SubscribeAsync)
            .WithUnsubscribeFromResourcesHandler(ResourceSubscriptionHandlers.UnsubscribeAsync)
            .WithMessageFilters(filters => filters
                .AddIncomingFilter(ProtocolRevisionFilter.Filter())
                .AddIncomingFilter(SessionTracking.Filter(sessions)));
    }

    public static ITokenStore CreateTokenStore(TawkMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.IsNullOrEmpty(options.Token))
        {
            return new FixedTokenStore(options.Token);
        }

        var path = options.TokenFile
            ?? FileTokenStore.DefaultPath(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return new FileTokenStore(path);
    }
}
