using Microsoft.Extensions.Logging.Abstractions;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Builds the real engines and managers around a fake control, the way the composition root does.</summary>
public sealed class TestParts
{
    public TestParts(FakeTawkControl? control = null)
    {
        Control = control ?? new FakeTawkControl();
        Transcript = new TranscriptFormatter(Clock);
        Directory = new ChatDirectoryFormatter(Transcript);
        Gate = new ConfirmationGate(Control);
        Reading = new ChatReadingManager(Control, Transcript, Directory, Fence, new CatchUpPlanner(Clock), new DraftReplyPlanner());
        Sending = new MessageSendingManager(Control, Transcript, Jitter);
        Messages = new MessageManagementManager(Gate);
        Chats = new ChatManagementManager(Gate, Directory, Fence);
        Schedule = new ScheduleManagementManager(Gate, Transcript, Jitter);
        Statuses = new StatusManager(Control, Gate, Fence);
        Profile = new ProfileManager(Control, Gate);
        Settings = new SettingsManager(Control, Gate);
        App = new AppManager(Control, Gate);
    }

    public ManualTimeProvider Clock { get; } = new();

    /// <summary>Off by default so tests see the time they asked for.</summary>
    public IScheduleJitter Jitter { get; } = new RandomScheduleJitter(TimeSpan.Zero, () => 0.5);

    public FakeTawkControl Control { get; }

    public TranscriptFormatter Transcript { get; }

    public ChatDirectoryFormatter Directory { get; }

    public UntrustedTextFence Fence { get; } = new();

    public ConfirmationGate Gate { get; }

    public ChatReadingManager Reading { get; }

    public MessageSendingManager Sending { get; }

    public MessageManagementManager Messages { get; }

    public ChatManagementManager Chats { get; }

    public ScheduleManagementManager Schedule { get; }

    public StatusManager Statuses { get; }

    public ProfileManager Profile { get; }

    public SettingsManager Settings { get; }

    public AppManager App { get; }

    public LiveUpdatesManager LiveUpdates(params IEventSink[] sinks) =>
        new(Control, sinks, Transcript, new NotificationFormatter(), Fence, NullLogger<LiveUpdatesManager>.Instance);
}
