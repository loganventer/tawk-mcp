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
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Memory;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;

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
        services.AddSingleton<IScheduleJitter>(new RandomScheduleJitter(TimeSpan.FromSeconds(options.ScheduleJitterS), Random.Shared.NextDouble));

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

        if (options.Memory == MemoryMode.Off)
        {
            services.AddSingleton<IDraftGuidance, NoDraftGuidance>();
        }
        else
        {
            AddMemory(services, options, home);
        }

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
                    + (options.Memory == MemoryMode.Off ? string.Empty : TawkServerInstructions.Memory)
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
                .AddIncomingFilter(SessionTracking.Filter(sessions)))
            .WithMemory(options.Memory);
    }

    private static IMcpServerBuilder WithMemory(this IMcpServerBuilder builder, MemoryMode mode) =>
        mode == MemoryMode.Off
            ? builder
            : builder
                .WithTools<CategoryTools>()
                .WithTools<VoiceTools>()
                .WithTools<ContactTools>()
                .WithTools<TemplateTools>()
                .WithResources<MemoryResources>();

    private static void AddMemory(IServiceCollection services, TawkMcpOptions options, string home)
    {
        var path = options.DataFile ?? SqliteConnectionFactory.DefaultPath(Environment.GetEnvironmentVariable, home);

        // Resource access.
        services.AddSingleton<ISchemaMigrator, SqliteSchemaMigrator>();
        services.AddSingleton<ISqliteConnectionFactory>(sp => new SqliteConnectionFactory(path, sp.GetRequiredService<ISchemaMigrator>()));
        services.AddSingleton<ICategoryStore, SqliteCategoryStore>();
        services.AddSingleton<IVoiceStore, SqliteVoiceStore>();
        services.AddSingleton<IContactStore, SqliteContactStore>();
        services.AddSingleton<ITemplateStore, SqliteTemplateStore>();
        services.AddSingleton<ITawkChatSource, TawkChatSource>();

        // Engines. Each voice rule is its own class; the checker runs whichever are registered here.
        services.AddSingleton<IStyleFeatureExtractor, StyleFeatureExtractor>();
        services.AddSingleton<IVoiceRule, ForbiddenPatternRule>();
        services.AddSingleton<IVoiceRule, CaseRule>();
        services.AddSingleton<IVoiceRule, EmojiRule>();
        services.AddSingleton<IVoiceRule, LengthRule>();
        services.AddSingleton<IVoiceRule, LanguageRule>();
        services.AddSingleton<IVoiceRule, AddressFormRule>();
        services.AddSingleton<IVoiceRule, RequiredMarkerRule>();
        services.AddSingleton<IVoiceRule, GreetingSignoffRule>();
        services.AddSingleton<IVoiceRule, BaselineDeviationRule>();
        services.AddSingleton<IVoiceChecker, VoiceChecker>();
        services.AddSingleton<IVoiceResolver, VoiceResolver>();
        services.AddSingleton<IVoiceGuideImporter, VoiceGuideImporter>();
        services.AddSingleton<IStyleBaselineCalculator, StyleBaselineCalculator>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<IMemoryFormatter, MemoryFormatter>();
        services.AddSingleton<IMemoryWriteGuard>(new MemoryWriteGuard(options.Memory));
        services.AddSingleton<IFieldValueValidator, TextValueValidator>();
        services.AddSingleton<IFieldValueValidator, TextListValueValidator>();
        services.AddSingleton<IFieldValueValidator, NumberValueValidator>();
        services.AddSingleton<IFieldValueValidator, BooleanValueValidator>();
        services.AddSingleton<IFieldValueValidator, ChoiceValueValidator>();
        services.AddSingleton<IFieldValueValidator, DateValueValidator>();
        services.AddSingleton<IFieldValueValidator, JsonValueValidator>();
        services.AddSingleton<IContactFieldCatalog>(sp => new ContactFieldCatalog(
            StandardContactFields.All, sp.GetServices<IFieldValueValidator>()));

        // Managers.
        services.AddSingleton<ICategoryEnsurer, CategoryEnsurer>();
        services.AddSingleton<IVoiceSelector, VoiceSelector>();
        services.AddSingleton<ICategoryManager, CategoryManager>();
        services.AddSingleton<VoiceManager>();
        services.AddSingleton<IVoiceManager>(sp => sp.GetRequiredService<VoiceManager>());
        services.AddSingleton<IDraftGuidance>(sp => sp.GetRequiredService<VoiceManager>());
        services.AddSingleton<IContactProfileManager, ContactProfileManager>();
        services.AddSingleton<ITemplateManager, TemplateManager>();
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
