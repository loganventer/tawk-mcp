using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess;

public class ControlLineCodecTests
{
    private readonly ControlLineCodec _codec = new();

    [Test]
    public void Encodes_the_read_messages_request_exactly_as_the_example()
    {
        var line = _codec.EncodeRequest("7", "read_messages", new JsonObject { ["chat"] = "Mom", ["limit"] = 20 });

        Assert.That(line, Is.EqualTo("""{"id":"7","op":"read_messages","args":{"chat":"Mom","limit":20}}"""));
    }

    [Test]
    public void Encodes_the_hello_request_exactly_as_the_example()
    {
        var args = new JsonObject { ["client"] = "tawk-mcp", ["version"] = "0.1.0", ["protocol"] = 1, ["origin"] = "mcp" };

        Assert.That(
            _codec.EncodeRequest("1", "hello", args),
            Is.EqualTo("""{"id":"1","op":"hello","args":{"client":"tawk-mcp","version":"0.1.0","protocol":1,"origin":"mcp"}}"""));
    }

    [Test]
    public void Leaves_args_out_when_there_are_none()
    {
        Assert.That(_codec.EncodeRequest("2", "unread_summary", null), Is.EqualTo("""{"id":"2","op":"unread_summary"}"""));
    }

    [Test]
    public void Keeps_emoji_and_other_text_readable()
    {
        var line = _codec.EncodeRequest("3", "send_message", new JsonObject { ["chat"] = "Mom", ["text"] = "Ja, 6 uur 👍" });

        Assert.That(line, Does.Contain("Ja, 6 uur"));
    }

    [Test]
    public void Decodes_a_successful_answer()
    {
        var frame = (ControlResponse)_codec.Decode("""{"id":"7","ok":true,"result":{"messages":[],"next_before":0}}""")!;

        Assert.Multiple(() =>
        {
            Assert.That(frame.Id, Is.EqualTo("7"));
            Assert.That(frame.Ok, Is.True);
            Assert.That(frame.Result.GetProperty("next_before").GetInt64(), Is.Zero);
        });
    }

    [Test]
    public void Decodes_a_failed_answer()
    {
        var frame = (ControlResponse)_codec.Decode("""{"id":"7","ok":false,"error":{"code":"not_found","message":"No chat matches \"Mum\""}}""")!;

        Assert.Multiple(() =>
        {
            Assert.That(frame.Ok, Is.False);
            Assert.That(frame.Error!.Kind, Is.EqualTo(ControlErrorCode.NotFound));
            Assert.That(frame.Error.Message, Is.EqualTo("No chat matches \"Mum\""));
        });
    }

    [Test]
    public void Decodes_ambiguous_candidates_and_retry_after()
    {
        var ambiguous = (ControlResponse)_codec.Decode(
            """{"id":"8","ok":false,"error":{"code":"ambiguous","message":"2 chats match","candidates":[{"jid":"a@s.whatsapp.net","name":"Mom"},{"jid":"b@g.us","name":"Mom's group"}]}}""")!;
        var limited = (ControlResponse)_codec.Decode(
            """{"id":"9","ok":false,"error":{"code":"rate_limited","message":"Too many","retry_after":42}}""")!;

        Assert.Multiple(() =>
        {
            Assert.That(ambiguous.Error!.Candidates!.Select(c => c.Name), Is.EqualTo(new[] { "Mom", "Mom's group" }));
            Assert.That(limited.Error!.RetryAfter, Is.EqualTo(42));
            Assert.That(limited.Error.Kind, Is.EqualTo(ControlErrorCode.RateLimited));
        });
    }

    [Test]
    public void Decodes_the_hello_result()
    {
        var frame = (ControlResponse)_codec.Decode(Samples.HelloAnswer)!;
        var hello = ControlLineCodec.Deserialize<HelloInfo>(frame.Result);

        Assert.That(hello, Is.EqualTo(new HelloInfo(1, "0.6.4", "send", new AccountInfo("27830000000@s.whatsapp.net", "Logan"), true)));
    }

    [Test]
    public void Decodes_the_unread_summary_example()
    {
        var frame = (ControlResponse)_codec.Decode(Samples.UnreadAnswer)!;
        var summary = ControlLineCodec.Deserialize<UnreadSummary>(frame.Result);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(2));
            Assert.That(summary.Chats[0], Is.EqualTo(new ChatSummary(Samples.MomJid, "Mom", false, 2, false, false, true, false, 1790000000, "See you at 6")));
        });
    }

    [Test]
    public void Decodes_the_send_answer()
    {
        var frame = (ControlResponse)_codec.Decode(Samples.SendAnswer)!;

        Assert.That(ControlLineCodec.Deserialize<SentMessage>(frame.Result).Id, Is.EqualTo("3EB0D41C22"));
    }

    [Test]
    public void Decodes_the_message_example_with_every_field()
    {
        var message = ControlLineCodec.Deserialize<ChatMessage>(System.Text.Json.JsonDocument.Parse(Samples.Message).RootElement);

        Assert.Multiple(() =>
        {
            Assert.That(message.SenderName, Is.EqualTo("Mom"));
            Assert.That(message.ReplyTo, Is.EqualTo(new ReplyRef("3EB0AA", "You", "When?", false)));
            Assert.That(message.Link!.Title, Is.EqualTo("Example"));
            Assert.That(message.Reactions, Is.EqualTo("👍 2"));
        });
    }

    [Test]
    public void Decodes_the_notification_example()
    {
        var frame = (ControlEventFrame)_codec.Decode("""{"evt":"message","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"message":{}}""")!;

        Assert.That(((MessageEvent)frame.Event).Chat, Is.EqualTo(new ChatRef(Samples.MomJid, "Mom")));
    }

    [Test]
    public void Reads_what_tawk_can_do_and_which_chats_are_not_transcribed()
    {
        var hello = ControlLineCodec.Deserialize<HelloInfo>(((ControlResponse)_codec.Decode(
            """{"id":"1","ok":true,"result":{"protocol":1,"tawk":"0.13.0","access":"send","connected":true,"features":["transcripts"]}}""")!).Result);
        var older = ControlLineCodec.Deserialize<HelloInfo>(((ControlResponse)_codec.Decode(Samples.HelloAnswer)!).Result);
        var on = (MessageEvent)((ControlEventFrame)_codec.Decode(
            """{"evt":"message","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"message":{}}""")!).Event;
        var off = (MessageEvent)((ControlEventFrame)_codec.Decode(
            """{"evt":"message","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"message":{},"transcribe":false}""")!).Event;
        var ready = (Tawk.Mcp.Core.Media.MediaReadyEvent)((ControlEventFrame)_codec.Decode(
            """{"evt":"media_ready","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"message_id":"3EB0","path":"/m/a.ogg","type":"audio","transcribe":false}""")!).Event;

        Assert.Multiple(() =>
        {
            Assert.That(hello.Has(TawkFeatures.Transcripts), Is.True);
            Assert.That(older.Has(TawkFeatures.Transcripts), Is.False, "a tawk that sends no list can do none of it");
            Assert.That(on.Transcribe, Is.True);
            Assert.That(off.Transcribe, Is.False);
            Assert.That(ready.Transcribe, Is.False);
        });
    }

    [Test]
    public void Decodes_chat_bye_approval_and_unknown_notifications()
    {
        Assert.Multiple(() =>
        {
            Assert.That(((ControlEventFrame)_codec.Decode($$"""{"evt":"chat","chat":{{Samples.Chat}}}""")!).Event, Is.TypeOf<ChatUpdatedEvent>());
            Assert.That(((ControlEventFrame)_codec.Decode("""{"evt":"bye"}""")!).Event, Is.TypeOf<ByeEvent>());
            Assert.That(
                ((ControlEventFrame)_codec.Decode("""{"evt":"approval","id":"3","state":"waiting"}""")!).Event,
                Is.EqualTo(new ApprovalEvent("3", "waiting")));
            Assert.That(((ControlEventFrame)_codec.Decode("""{"evt":"typing","chat":"x"}""")!).Event, Is.EqualTo(new UnknownEvent("typing")));
        });
    }

    [Test]
    public void Ignores_fields_it_does_not_know()
    {
        var frame = (ControlResponse)_codec.Decode("""{"id":"1","ok":true,"extra":1,"result":{"id":"X","future":true}}""")!;

        Assert.That(ControlLineCodec.Deserialize<SentMessage>(frame.Result).Id, Is.EqualTo("X"));
    }

    [Test]
    public void Returns_null_for_lines_that_are_neither_answers_nor_notifications()
    {
        Assert.That(_codec.Decode("[1,2]"), Is.Null);
    }

    [Test]
    public void Reads_a_read_receipt_event()
    {
        var frame = new ControlLineCodec().Decode(
            """{"evt":"read","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"message_id":"3EB0","reader":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"at":1790791400}""");

        var read = (ReadEvent)((ControlEventFrame)frame!).Event;
        Assert.Multiple(() =>
        {
            Assert.That(read.Chat.Name, Is.EqualTo("Mom"));
            Assert.That(read.MessageId, Is.EqualTo("3EB0"));
            Assert.That(read.Reader.Name, Is.EqualTo("Mom"));
            Assert.That(read.At, Is.EqualTo(1790791400));
        });
    }

    [Test]
    public void Reads_someone_coming_online_and_leaving()
    {
        var codec = new ControlLineCodec();
        var came = (PresenceEvent)((ControlEventFrame)codec.Decode(
            """{"evt":"presence","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"who":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"state":"online","at":1791364000,"account":{"id":2,"label":"work"}}""")!).Event;
        var left = (PresenceEvent)((ControlEventFrame)codec.Decode(
            """{"evt":"presence","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"who":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"state":"offline","last_seen":1791363900,"at":1791364100}""")!).Event;

        Assert.Multiple(() =>
        {
            Assert.That(came.Online, Is.True);
            Assert.That(came.LastSeen, Is.Null);
            Assert.That(came.Chat.Name, Is.EqualTo("Mom"));
            Assert.That(came.Who.Jid, Is.EqualTo("27820000000@s.whatsapp.net"));
            Assert.That(came.At, Is.EqualTo(1791364000));
            Assert.That(came.Account!.Label, Is.EqualTo("work"));
            Assert.That(left.Online, Is.False);
            Assert.That(left.LastSeen, Is.EqualTo(1791363900));
        });
    }
}
