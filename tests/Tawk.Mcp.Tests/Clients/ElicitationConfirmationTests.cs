using System.Text.Json;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Tests.Clients;

public class ElicitationConfirmationTests
{
    private static ElicitationConfirmation Answering(string action, bool? confirm, List<ElicitRequestParams>? asked = null) =>
        new((request, _) =>
        {
            asked?.Add(request);
            var content = confirm is { } c
                ? new Dictionary<string, JsonElement> { [ElicitationConfirmation.FieldName] = JsonSerializer.SerializeToElement(c) }
                : null;
            return ValueTask.FromResult(new ElicitResult { Action = action, Content = content });
        });

    [Test]
    public async Task Asks_a_yes_or_no_question_showing_the_summary()
    {
        var asked = new List<ElicitRequestParams>();

        var answer = await Answering("accept", true, asked).AskAsync("Delete the chat with Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Is.EqualTo(ConfirmationAnswer.Accepted));
            Assert.That(asked[0].Message, Does.Contain("Delete the chat with Mom"));
            Assert.That(asked[0].RequestedSchema!.Properties[ElicitationConfirmation.FieldName], Is.TypeOf<ElicitRequestParams.BooleanSchema>());
        });
    }

    [TestCase("accept", false, ConfirmationAnswer.Declined)]
    [TestCase("decline", null, ConfirmationAnswer.Declined)]
    [TestCase("cancel", null, ConfirmationAnswer.Cancelled)]
    public async Task Anything_but_a_ticked_yes_is_a_no(string action, bool? confirm, ConfirmationAnswer expected)
    {
        Assert.That(await Answering(action, confirm).AskAsync("x", CancellationToken.None), Is.EqualTo(expected));
    }

    [Test]
    public async Task A_client_without_elicitation_cannot_be_asked()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await new ElicitationConfirmation(null).AskAsync("x", CancellationToken.None), Is.EqualTo(ConfirmationAnswer.NotSupported));
            Assert.That(await ElicitationConfirmation.For(null).AskAsync("x", CancellationToken.None), Is.EqualTo(ConfirmationAnswer.NotSupported));
        });
    }
}
