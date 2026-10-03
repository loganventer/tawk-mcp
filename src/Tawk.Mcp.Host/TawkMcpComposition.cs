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
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Knowledge;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.Engines.Sync;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Approvals;
using Tawk.Mcp.Managers.Knowledge;
using Tawk.Mcp.Managers.Memory;
using Tawk.Mcp.Managers.Sync;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Host;

/// <summary>The composition root: every interface is bound to its implementation here and nowhere else.</summary>
public static class TawkMcpComposition
{
    public static string Version { get; } =
        typeof(TawkMcpComposition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.2.0";

    public static IMcpServerBuilder AddTawkMcp(this IServiceCollection services, TawkMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        services.TryAddSingleton(TimeProvider.System);

        // Resource access.
        services.AddSingleton<IControlSocketLocator>(new ControlSocketLocator(options.SocketPath, Environment.GetEnvironmentVariable, home));
        services.AddSingleton<ControlLineCodec>();
        // Answering its own requests is set per instance and never by default: no admin token file, no admin.
        var admin = new AdminOptions(options.AdminTokenFile);
        services.AddSingleton(admin);
        services.AddSingleton(new TawkControlOptions
        {
            Version = Version,
            RequestTimeout = TimeSpan.FromSeconds(options.RequestTimeoutS),
            ParkWaitingWrites = admin.Enabled,
        });
        services.AddSingleton<ConnectSignal>();
        services.AddSingleton<ISocketFileWatcher, SocketFileWatcher>();
        services.AddSingleton<IDelay, TimeProviderDelay>();
        services.AddSingleton<UnixSocketTawkControl>();
        services.AddSingleton<ITawkControl>(sp => sp.GetRequiredService<UnixSocketTawkControl>());
        services.AddSingleton<ITawkConnector>(sp => sp.GetRequiredService<UnixSocketTawkControl>());
        services.AddSingleton<ITawkApprovals>(sp => sp.GetRequiredService<UnixSocketTawkControl>());
        services.AddSingleton<IAdminTokenSource, FileAdminTokenSource>();
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
        services.AddSingleton<IApprovalManager, ApprovalManager>();

        if (options.Memory == MemoryMode.Off)
        {
            services.AddSingleton<IDraftGuidance, NoDraftGuidance>();
        }
        else
        {
            AddMemory(services, options, home);
        }

        // Clients.
        var workflow = new WorkflowOptions(options.Memory == MemoryMode.Write ? options.WorkflowEvery : 0, options.UserInstructions);
        var cadence = new WorkflowCadence(workflow);
        services.AddSingleton(workflow);
        services.AddSingleton<IWorkflowCadence>(cadence);
        if (options.Memory == MemoryMode.Write && options.SyncToken is { Length: > 0 } && options.SyncRepository is { Length: > 0 })
        {
            services.AddHostedService<MemorySyncService>();
        }

        // A session that is let go takes its resource subscriptions with it.
        var subscriptions = new ResourceSubscriptionRegistry();
        var sessions = new ClientSessionRegistry(subscriptions.RemoveSession);
        services.AddSingleton<IClientSessionRegistry>(sessions);
        services.AddSingleton<IResourceSubscriptionRegistry>(subscriptions);
        services.AddSingleton(new ChannelOptions(options.Channel, options.ChannelOwn));
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
                    + (workflow.Enabled ? TawkServerInstructions.Workflow : string.Empty)
                    + (admin.Enabled ? TawkServerInstructions.Admin : string.Empty)
                    + (options.Channel == ChannelMode.Off ? string.Empty : TawkServerInstructions.Channel)
                    + (options.Channel != ChannelMode.Off && options.ChannelOwn ? TawkServerInstructions.ChannelOwn : string.Empty)
                    + TawkServerInstructions.FromUser(options.UserInstructions);
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
            .WithRequestFilters(filters => filters.AddCallToolFilter(WorkflowReminder.Filter(cadence, workflow)))
            .WithAdmin(admin)
            .WithMemory(options.Memory, workflow);
    }

    /// <summary>The memory stores, engines and managers alone, for the commands that work on memory without serving MCP.</summary>
    public static ServiceProvider BuildMemory(TawkMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        AddMemory(services, options, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return services.BuildServiceProvider();
    }

    private static IMcpServerBuilder WithAdmin(this IMcpServerBuilder builder, AdminOptions admin) =>
        admin.Enabled ? builder.WithTools<ApprovalTools>() : builder;

    private static IMcpServerBuilder WithMemory(this IMcpServerBuilder builder, MemoryMode mode, WorkflowOptions workflow) =>
        mode == MemoryMode.Off
            ? builder
            : (workflow.Enabled ? builder.WithTools<WorkflowTools>() : builder)
                .WithTools<CategoryTools>()
                .WithTools<VoiceTools>()
                .WithTools<ContactTools>()
                .WithTools<KnowledgeTools>()
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
        services.AddSingleton(new OkfProducer(Version));
        services.AddSingleton<IOkfStore, SqliteOkfStore>();
        services.AddSingleton<IOkfBundleFiles, DiskOkfBundleFiles>();
        services.AddSingleton<IContactStore, SqliteContactStore>();
        services.AddSingleton<ITemplateStore, SqliteTemplateStore>();
        services.AddSingleton<ITawkChatSource, TawkChatSource>();

        // Sync. It has no default destination: without a repository and a token set on this machine, nothing syncs.
        // Everything it keeps on this machine sits beside the database.
        var sync = new SyncOptions
        {
            Token = options.SyncToken,
            Repository = options.SyncRepository,
            Branch = options.SyncBranch,
            File = options.SyncFile,
            Api = Uri.TryCreate(options.SyncApi, UriKind.Absolute, out var api) ? api : new Uri(SyncOptions.DefaultApi),
            Interval = TimeSpan.FromMinutes(options.SyncIntervalMinutes),
        };
        services.AddSingleton(sync);
        services.AddSingleton<IMemorySnapshotStore, SqliteMemorySnapshotStore>();
        services.AddSingleton<IMemoryRemote>(_ => new GitHubMemoryRemote(new HttpClient { Timeout = TimeSpan.FromSeconds(100) }, sync, Version));
        services.AddSingleton<ISyncStateStore>(new FileSyncStateStore(path + ".sync.json"));
        services.AddSingleton<ISyncLock>(new FileSyncLock(path + ".sync.lock"));
        services.AddSingleton<ISyncScratch, TempSyncScratch>();
        services.AddSingleton<IMemoryMerger, MemoryMerger>();
        services.AddSingleton<IMemoryDigest, MemoryDigest>();
        services.AddSingleton<IMemorySyncManager, MemorySyncManager>();

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
        services.AddSingleton<IKnowledgeFormatter, KnowledgeFormatter>();
        services.AddSingleton<IKnowledgePrecedence, KnowledgePrecedence>();
        services.AddSingleton<IOkfBundleWriter, OkfBundleWriter>();
        services.AddSingleton<IOkfBundleReader, OkfBundleReader>();
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
        services.AddSingleton<IKnowledgeSubjects, KnowledgeSubjects>();
        services.AddSingleton<IKnowledgeManager, KnowledgeManager>();
        services.AddSingleton<IOkfBundleManager, OkfBundleManager>();
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
