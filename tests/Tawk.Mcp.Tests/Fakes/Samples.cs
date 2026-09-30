namespace Tawk.Mcp.Tests.Fakes;

/// <summary>JSON taken from CONTROL.md.</summary>
public static class Samples
{
    public const string MomJid = "27820000000@s.whatsapp.net";

    public const string Chat = """{"jid":"27820000000@s.whatsapp.net","name":"Mom","is_group":false,"unread":2,"unread_mention":false,"muted":false,"pinned":true,"archived":false,"last_ts":1790000000,"preview":"See you at 6"}""";

    public const string Message = """{"id":"3EB0C2A1F0","chat":"27820000000@s.whatsapp.net","sender":"27820000000@s.whatsapp.net","sender_name":"Mom","from_me":false,"ts":1790000000,"type":"text","text":"See you at 6","status":"read","edited":false,"deleted":false,"forwarded":false,"reply_to":{"id":"3EB0AA","sender":"You","text":"When?","status":false},"reactions":"👍 2","link":{"url":"https://example.org","title":"Example","description":""},"mentions_me":false}""";

    public const string HelloAnswer = """{"id":"1","ok":true,"result":{"protocol":1,"tawk":"0.6.4","access":"send","account":{"jid":"27830000000@s.whatsapp.net","name":"Logan"},"connected":true}}""";

    public const string UnreadAnswer = """{"id":"2","ok":true,"result":{"total":2,"mentions":0,"chats":[{"jid":"27820000000@s.whatsapp.net","name":"Mom","is_group":false,"unread":2,"unread_mention":false,"muted":false,"pinned":true,"archived":false,"last_ts":1790000000,"preview":"See you at 6"}]}}""";

    public const string SendAnswer = """{"id":"3","ok":true,"result":{"id":"3EB0D41C22"}}""";

    public static string MessageEvent(string text = "See you at 6", string id = "3EB0C2A1F0") =>
        $$"""{"evt":"message","chat":{"jid":"{{MomJid}}","name":"Mom"},"message":{{Message.Replace("See you at 6", text, StringComparison.Ordinal).Replace("3EB0C2A1F0", id, StringComparison.Ordinal)}}}""";
}
