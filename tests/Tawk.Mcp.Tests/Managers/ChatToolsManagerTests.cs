using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers;

public class ChatToolsManagerTests
{
    private readonly TestParts _parts = new();

    private ChatToolsManager Manager(bool newTawk = true)
    {
        _parts.Control.Hello = _parts.Control.Hello with
        {
            Features = newTawk ? [TawkFeatures.Labels, TawkFeatures.Reminders, TawkFeatures.AwaitingReplies] : [TawkFeatures.Transcripts],
        };
        return new ChatToolsManager(_parts.Control, _parts.Gate, _parts.Transcript, _parts.Directory, _parts.Fence);
    }

    private static WriteContext Context => new(null, null);

    [Test]
    public async Task Every_label_in_use_is_listed_inside_the_fence()
    {
        _parts.Control.Answer("list_labels", """{"labels":["clients","family"]}""");

        var text = await Manager().ListLabelsAsync(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("2 labels."));
            Assert.That(text, Does.Contain("clients, family").And.Contain("UNTRUSTED"));
            Assert.That(_parts.Control.Last("list_labels").Args, Is.Null);
        });
    }

    [Test]
    public async Task One_chats_labels_are_asked_for_by_chat()
    {
        _parts.Control.Answer("list_labels", """{"chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"labels":["family"]}""");

        var text = await Manager().ListLabelsAsync("Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Mom <27820000000@s.whatsapp.net>: family"));
            Assert.That((string?)_parts.Control.Last("list_labels").Args!["chat"], Is.EqualTo("Mom"));
        });
    }

    [Test]
    public async Task No_labels_is_said_plainly()
    {
        _parts.Control.Answer("list_labels", """{"labels":[]}""");

        Assert.That(await Manager().ListLabelsAsync(null, CancellationToken.None), Is.EqualTo("The user has no labels yet."));
    }

    [Test]
    public async Task A_label_is_put_on_a_chat_and_the_result_says_what_it_carries()
    {
        _parts.Control.Answer("set_label", """{"labels":["clients","urgent"]}""");

        var text = await Manager().SetLabelAsync("Work", "Urgent", true, Context, CancellationToken.None);

        var sent = _parts.Control.Last("set_label").Args!;
        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Labelled. The chat now carries: clients, urgent."));
            Assert.That((string?)sent["chat"], Is.EqualTo("Work"));
            Assert.That((string?)sent["label"], Is.EqualTo("Urgent"));
            Assert.That((bool?)sent["on"], Is.True);
        });
    }

    [Test]
    public async Task Taking_the_last_label_off_says_the_chat_carries_none()
    {
        _parts.Control.Answer("set_label", """{"labels":[]}""");

        Assert.That(
            await Manager().SetLabelAsync("Work", "urgent", false, Context, CancellationToken.None),
            Is.EqualTo("Label taken off. The chat carries no labels now."));
    }

    [Test]
    public async Task Chats_put_aside_are_listed_with_when_they_come_back()
    {
        _parts.Control.Answer(
            "list_reminders",
            """{"reminders":[{"chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"due_at":0},{"chat":{"jid":"27820000002@s.whatsapp.net","name":"Work"},"due_at":1790000000}]}""");

        var text = await Manager().ListRemindersAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("2 chats put aside."));
            Assert.That(text, Does.Contain("Mom <27820000000@s.whatsapp.net>: until its person writes"));
            Assert.That(text, Does.Contain("Work <27820000002@s.whatsapp.net>: until ").And.Contain("or sooner if its person writes"));
        });
    }

    [Test]
    public async Task A_reminder_is_asked_of_tawk_in_its_own_words_for_time()
    {
        _parts.Control.Answer("set_reminder", """{"due_at":0}""");

        var text = await Manager().SetReminderAsync("Mom", "reply", Context, CancellationToken.None);

        var sent = _parts.Control.Last("set_reminder").Args!;
        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Put aside until its person writes. It is out of the chat list until then."));
            Assert.That((string?)sent["when"], Is.EqualTo("reply"));
        });
    }

    [Test]
    public async Task Cancelling_brings_the_chat_back()
    {
        _parts.Control.Answer("cancel_reminder", "{}");

        Assert.That(await Manager().CancelReminderAsync("Mom", Context, CancellationToken.None), Is.EqualTo("The chat is back in the list."));
    }

    [Test]
    public async Task Chats_awaiting_a_reply_are_listed_with_the_days_tawk_used()
    {
        _parts.Control.Answer(
            "awaiting_replies",
            """{"days":3,"chats":[{"jid":"27820000002@s.whatsapp.net","name":"Work","is_group":false,"unread":0,"unread_mention":false,"muted":false,"pinned":false,"archived":false,"last_ts":1790000000,"preview":"Did you get my quote?"}]}""");

        var text = await Manager().AwaitingRepliesAsync(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("1 chat where the user's last message has gone unanswered for 3 days or more"));
            Assert.That(text, Does.Contain("Work").And.Contain("UNTRUSTED"));
            Assert.That(_parts.Control.Last("awaiting_replies").Args, Is.Null, "no days given: tawk uses the user's own setting");
        });
    }

    [Test]
    public async Task The_days_asked_for_are_passed_on()
    {
        _parts.Control.Answer("awaiting_replies", """{"days":10,"chats":[]}""");

        var text = await Manager().AwaitingRepliesAsync(10, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("0 chats where").And.Not.Contain("UNTRUSTED"));
            Assert.That((int?)_parts.Control.Last("awaiting_replies").Args!["days"], Is.EqualTo(10));
        });
    }

    [Test]
    public void An_older_tawk_is_asked_nothing()
    {
        var manager = Manager(newTawk: false);

        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<TawkControlException>(() => manager.ListLabelsAsync(null, CancellationToken.None))!.Message, Does.Contain("no labels").And.Contain("0.20.0"));
            Assert.That(Assert.ThrowsAsync<TawkControlException>(() => manager.SetReminderAsync("Mom", "9:00", Context, CancellationToken.None))!.Message, Does.Contain("no reminders"));
            Assert.That(Assert.ThrowsAsync<TawkControlException>(() => manager.AwaitingRepliesAsync(null, CancellationToken.None))!.Message, Does.Contain("awaiting a reply"));
            Assert.That(_parts.Control.Requests, Is.Empty);
        });
    }
}
