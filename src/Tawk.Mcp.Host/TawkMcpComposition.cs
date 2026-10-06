using System.Globalization;
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
using Tawk.Mcp.Core.Media;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Knowledge;
using Tawk.Mcp.Engines.Media;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.Engines.Sync;
using Tawk.Mcp.Engines.Transcription;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Approvals;
using Tawk.Mcp.Managers.Knowledge;
using Tawk.Mcp.Managers.Media;
using Tawk.Mcp.Managers.Memory;
using Tawk.Mcp.Managers.Sync;
using Tawk.Mcp.Managers.Transcription;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Media;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.ResourceAccess.Sync;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Host;

/// <summary>The composition root: every interface is bound to its implementation here and nowhere else.</summary>
public static class TawkMcpComposition
{
    public static string Version { get; } =
        typeof(TawkMcpComposition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.5.2";

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
            Label = ConnectionLabel(options),
            RequestTimeout = TimeSpan.FromSeconds(options.RequestTimeoutS),
            ParkWaitingWrites = admin.Enabled,
        });
        services.AddSingleton<ConnectSignal>();
        services.AddSingleton<ISocketFileWatcher, SocketFileWatcher>();
        services.AddSingleton<IDelay, TimeProviderDelay>();
        services.AddSingleton<UnixSocketTawkControl>();
        services.AddSingleton<IAccountScope, AmbientAccountScope>();
        services.AddSingleton<IAccountTag, TawkAccountTag>();
        // Every request names the account of the tool call it is made in.
        services.AddSingleton<ITawkControl>(sp =>
            new AccountScopedTawkControl(sp.GetRequiredService<UnixSocketTawkControl>(), sp.GetRequiredService<IAccountScope>()));
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

        // Media: tawk says where a file is, and tawk-mcp only ever reads it.
        services.AddSingleton(new MediaOptions());
        services.AddSingleton<ITawkMediaSource, TawkMediaSource>();
        services.AddSingleton<IMediaFiles, DiskMediaFiles>();
        services.AddSingleton<IMediaTypeSniffer, MediaTypeSniffer>();
        services.AddSingleton<IMediaViewingManager, MediaViewingManager>();
        // The tools are always offered. With transcription off they say so, instead of not being there.
        var transcription = Transcription(options);
        AddTranscription(services, transcription);

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
        if (options.Memory == MemoryMode.Write && options.SyncRepository is { Length: > 0 })
        {
            services.AddHostedService<MemorySyncService>();
        }

        // A session that is let go takes its resource subscriptions with it.
        var subscriptions = new ResourceSubscriptionRegistry();
        var hints = new ChannelContextHints();
        var sessions = new ClientSessionRegistry(gone =>
        {
            subscriptions.RemoveSession(gone);
            hints.Forget(gone);
        });
        services.AddSingleton<IClientSessionRegistry>(sessions);
        services.AddSingleton<IResourceSubscriptionRegistry>(subscriptions);
        services.AddSingleton(new ChannelOptions(
            options.Channel, options.ChannelOwn, options.ChannelRead, options.ChannelReactions, options.ChannelEdits, options.ChannelScheduled));
        services.AddSingleton<EventStreamHub>();
        services.AddSingleton<IEventStreamHub>(sp => sp.GetRequiredService<EventStreamHub>());
        services.AddSingleton<IEventSink, ResourceUpdatePump>();
        services.AddSingleton<IChannelContextHints>(hints);
        services.AddSingleton<IEventSink, ChannelEventSink>();
        services.AddSingleton<IEventSink>(sp => sp.GetRequiredService<EventStreamHub>());
        services.AddHostedService<NotificationDispatcher>();

        return services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation { Name = "tawk-mcp", Title = "tawk", Version = Version };
                server.ServerInstructions = TawkServerInstructions.Text
                    + TawkServerInstructions.Accounts
                    + (options.Memory == MemoryMode.Off ? string.Empty : TawkServerInstructions.Memory)
                    + (workflow.Enabled ? TawkServerInstructions.Workflow : string.Empty)
                    + (admin.Enabled ? TawkServerInstructions.Admin : string.Empty)
                    + TawkServerInstructions.Media
                    + TawkServerInstructions.Transcription
                    + (options.Channel == ChannelMode.Off ? string.Empty : TawkServerInstructions.Channel)
                    + (options.Channel != ChannelMode.Off && options.ChannelOwn ? TawkServerInstructions.ChannelOwn : string.Empty)
                    + (options.Channel != ChannelMode.Off && options.ChannelRead ? TawkServerInstructions.ChannelRead : string.Empty)
                    + (options.Channel != ChannelMode.Off && (options.ChannelReactions || options.ChannelEdits || options.ChannelScheduled)
                        ? TawkServerInstructions.ChannelActivity
                        : string.Empty)
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
            .WithTools<MediaTools>()
            .WithResources<ChatResources>()
            .WithPrompts<TawkPrompts>()
            .WithSubscribeToResourcesHandler(ResourceSubscriptionHandlers.SubscribeAsync)
            .WithUnsubscribeFromResourcesHandler(ResourceSubscriptionHandlers.UnsubscribeAsync)
            .WithMessageFilters(filters => filters
                .AddIncomingFilter(ProtocolRevisionFilter.Filter())
                .AddIncomingFilter(SessionTracking.Filter(sessions)))
            .WithRequestFilters(filters => filters.AddCallToolFilter(WorkflowReminder.Filter(cadence, workflow)))
            .WithAdmin(admin)
            .WithTools<TranscriptionTools>()
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

    // Each Claude Code session starts its own stdio server, so several may be connected to tawk at once.
    // The folder and the process id say which is which; the one HTTP server says its port.
    private static string ConnectionLabel(TawkMcpOptions options)
    {
        if (options.Transport != TransportKind.Stdio)
        {
            return string.Create(CultureInfo.InvariantCulture, $"http, port {options.Port}");
        }

        var folder = Path.GetFileName(Environment.CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Create(
            CultureInfo.InvariantCulture, $"{(folder.Length == 0 ? "/" : folder)} (stdio, pid {Environment.ProcessId})");
    }

    /// <summary>The model files and their manager alone, for `tawk-mcp fetch-model`, which does not serve MCP.</summary>
    public static ServiceProvider BuildModels(TawkMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = new ServiceCollection();
        services.AddSingleton(Transcription(options));
        services.AddSingleton<IModelFiles, DiskModelFiles>();
        services.AddSingleton<ITranscriptionModelManager, TranscriptionModelManager>();
        return services.BuildServiceProvider();
    }

    private static TranscriptionOptions Transcription(TawkMcpOptions options) => new()
    {
        Engine = options.Transcribe,
        Url = options.TranscribeUrl,
        Command = options.TranscribeCommand,
        Model = options.TranscribeModel,
        Models = options.TranscribeModels,
        Languages = options.TranscribeLanguages,
        MaxLanguages = options.TranscribeMaxLanguages,
        MaxSeconds = options.TranscribeMaxSeconds,
        Timeout = TimeSpan.FromSeconds(options.TranscribeTimeoutS),
        Concurrency = options.TranscribeConcurrency,
        Automatic = options.TranscribeAuto,
        ModelDirectory = options.TranscribeModelDir
            ?? DiskModelFiles.DefaultFolder(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        IdleUnload = TimeSpan.FromMinutes(options.TranscribeIdleUnloadM),
    };

    // The engine is chosen here, once. Nothing below this knows which one it was given.
    private static void AddTranscription(IServiceCollection services, TranscriptionOptions transcription)
    {
        services.AddSingleton(transcription);

        // The model inside tawk-mcp: one host owns it, and one lock keeps it the only one on the machine.
        services.AddSingleton<IModelFiles, DiskModelFiles>();
        services.AddSingleton<IModelLock>(sp => new FileModelLock(
            Path.Combine(transcription.ModelDirectory, ".loaded.lock"), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IAudioDecoder, OggOpusDecoder>();
        services.AddSingleton<IWhisperRuntime>(new BundledWhisperRuntime(typeof(TawkMcpComposition).Assembly, transcription));
        services.AddSingleton<IWhisperModelHost, WhisperModelHost>();
        services.AddSingleton<EmbeddedTranscriber>();

        // Each pass is bounded by the manager, so the HTTP client itself never times out first.
        services.AddSingleton(_ => new HttpTranscriber(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, transcription));
        switch (transcription.Engine)
        {
            case TranscriptionEngine.Off:
                services.AddSingleton<ITranscriber, NoTranscriber>();
                break;
            case TranscriptionEngine.Command:
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<ITranscriber, CommandTranscriber>();
                break;
            case TranscriptionEngine.Http:
                services.AddSingleton<ITranscriber>(sp => sp.GetRequiredService<HttpTranscriber>());
                break;
            case TranscriptionEngine.Auto when transcription.Url is not null:
                // Its own breaker: the one for tawk's socket must not open because a transcriber is down.
                services.AddSingleton<ITranscriber>(sp => new FailoverTranscriber(
                    sp.GetRequiredService<HttpTranscriber>(),
                    sp.GetRequiredService<EmbeddedTranscriber>(),
                    new CircuitBreaker(2, TimeSpan.FromSeconds(60), sp.GetRequiredService<TimeProvider>()),
                    sp.GetRequiredService<IWhisperModelHost>()));
                break;
            default:
                services.AddSingleton<ITranscriber>(sp => sp.GetRequiredService<EmbeddedTranscriber>());
                break;
        }

        services.AddSingleton<ITranscriptionJobStore, InMemoryTranscriptionJobStore>();
        services.AddSingleton<ITranscriptionPolicy, TranscriptionPolicy>();
        services.AddSingleton<ITranscriptionNoticeFormatter, TranscriptionNoticeFormatter>();
        services.AddSingleton<ITranscriptionManager, TranscriptionManager>();
        services.AddSingleton<ITranscriptionRunManager, TranscriptionRunManager>();
        services.AddSingleton<ITranscriptionPreferences, TawkTranscriptionPreferences>();
        // Whether a voice note is transcribed unasked is read as it arrives, so the sink is always there.
        services.AddSingleton<IEventSink, AutoTranscriptionSink>();

        services.AddHostedService<TranscriptionWorker>();
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

        // Sync. It has no default destination: without a repository set on this machine, nothing syncs. It goes
        // over SSH with the machine's own key; there are no tokens.
        // Everything it keeps on this machine sits beside the database.
        var sync = new SyncOptions
        {
            Repository = options.SyncRepository,
            KeyFile = options.SyncKeyFile,
            Branch = options.SyncBranch,
            File = options.SyncFile,
            Interval = TimeSpan.FromMinutes(options.SyncIntervalMinutes),
        };
        services.AddSingleton(sync);
        services.AddSingleton<IMemorySnapshotStore, SqliteMemorySnapshotStore>();
        services.AddSingleton<IGitRunner>(new ProcessGitRunner(TimeSpan.FromSeconds(100)));
        services.AddSingleton<IMemoryRemote>(sp => new GitMemoryRemote(
            sp.GetRequiredService<IGitRunner>(), sync, path + ".sync.git", Environment.GetEnvironmentVariable));
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
