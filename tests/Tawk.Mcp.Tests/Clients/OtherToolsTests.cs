using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class OtherToolsTests
{
    private readonly TestParts _parts = new();

    [Test]
    public async Task Schedule_tools_call_their_operations()
    {
        _parts.Control
            .Answer("list_scheduled", """{"scheduled":[{"id":"S1","chat":"27820000000@s.whatsapp.net","text":"Happy birthday","due_at":1790791320}]}""")
            .Answer("schedule_message", """{"id":"S2","due_at":1790791320}""")
            .Answer("reschedule", """{"due_at":1790791320}""");
        var tools = new ScheduleTools(_parts.Reading, _parts.Sending, _parts.Schedule);

        var list = ToolOutput.Text(await tools.ListScheduledAsync());
        var scheduled = ToolOutput.Text(await tools.ScheduleMessageAsync("Mom", "18:02", "hi"));
        var moved = ToolOutput.Text(await tools.RescheduleAsync(null, "S1", "18:02"));
        var now = ToolOutput.Text(await tools.SendScheduledNowAsync(null, "S1"));

        Assert.Multiple(() =>
        {
            Assert.That(list, Does.Contain("[due 2026-09-30 18:02] to 27820000000@s.whatsapp.net: Happy birthday [id S1]"));
            Assert.That(scheduled, Is.EqualTo("Scheduled for 2026-09-30 18:02 (id S2)."));
            Assert.That((string?)_parts.Control.Last("schedule_message").Args!["when"], Is.EqualTo("18:02"));
            Assert.That(moved, Is.EqualTo("Rescheduled for 2026-09-30 18:02."));
            Assert.That(now, Is.EqualTo("Sending now."));
        });
    }

    [Test]
    public async Task Status_tools_call_their_operations()
    {
        _parts.Control
            .Answer("list_statuses", """{"statuses":[{"id":"ST1","author":"x","author_name":"Dad","from_me":false,"type":"text","text":"Fishing","ts":1790791320,"viewed":false}]}""")
            .Answer("status_viewers", """{"viewers":[{"jid":"x","name":"Dad","ts":1,"liked":true}]}""")
            .Answer("list_backgrounds", """{"backgrounds":["blue"]}""")
            .Answer("reply_status", """{"id":"R1"}""")
            .Answer("like_status", """{"how":"reply"}""");
        var tools = new StatusTools(_parts.Reading, _parts.Statuses);

        Assert.Multiple(async () =>
        {
            Assert.That(ToolOutput.Text(await tools.ListStatusesAsync(true)), Does.Contain("Dad: Fishing (not viewed) [id ST1]"));
            Assert.That((bool?)_parts.Control.Last("list_statuses").Args!["include_archived"], Is.True);
            Assert.That(ToolOutput.Text(await tools.StatusViewersAsync("ST1")), Does.Contain("\"liked\":true").And.Contain("UNTRUSTED"));
            Assert.That(ToolOutput.Text(await tools.ListBackgroundsAsync()), Is.EqualTo("""{"backgrounds":["blue"]}"""));
            Assert.That(ToolOutput.Text(await tools.PostStatusAsync(null, "text", "hello", background: "blue")), Does.Contain("posting"));
            Assert.That(ToolOutput.Text(await tools.ReplyStatusAsync(null, "ST1", "nice")), Does.Contain("R1"));
            Assert.That(ToolOutput.Text(await tools.LikeStatusAsync(null, "ST1")), Does.Contain("heart reply"));
        });
    }

    [Test]
    public async Task Profile_settings_and_app_tools_call_their_operations()
    {
        _parts.Control
            .Answer("get_profile", """{"jid":"27830000000@s.whatsapp.net","name":"Logan"}""")
            .Answer("set_setting", """{"value":"on"}""")
            .Answer("app_status", """{"tawk":"0.6.4","backend":"whatsmeow","connected":true,"state":"online","detail":"","ringing":false}""");
        var profile = new ProfileTools(_parts.Profile);
        var settings = new SettingsTools(_parts.Settings);
        var app = new AppTools(_parts.App);

        Assert.Multiple(async () =>
        {
            Assert.That(ToolOutput.Text(await profile.GetProfileAsync()), Does.Contain("Logan"));
            Assert.That(ToolOutput.Text(await profile.SetProfileAsync(null, about: "Busy")), Does.Contain("background"));
            Assert.That((await profile.SetProfileAsync(null)).IsError, Is.True);
            Assert.That(ToolOutput.Text(await profile.SetProfilePhotoAsync(null, "/tmp/me.jpg")), Is.EqualTo("Profile photo set."));
            Assert.That(ToolOutput.Text(await settings.SetSettingAsync(null, "notifications", "sound", "on")), Is.EqualTo("notifications.sound is now on."));
            Assert.That(ToolOutput.Text(await settings.GetSettingsAsync()), Is.EqualTo("{}"));
            Assert.That(ToolOutput.Text(await settings.ListThemesAsync()), Is.EqualTo("{}"));
            Assert.That(ToolOutput.Text(await app.AppStatusAsync()), Does.Contain("whatsmeow"));
            Assert.That(ToolOutput.Text(await app.ReconnectAsync(null)), Does.Contain("reconnecting"));
            Assert.That(ToolOutput.Text(await app.DeclineCallAsync(null)), Is.EqualTo("Declined the call."));
        });
    }
}
