using System.Text.Json;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class ContactFieldCatalogTests
{
    private readonly ContactFieldCatalog _catalog = new(
        StandardContactFields.All,
        [
            new TextValueValidator(), new TextListValueValidator(), new NumberValueValidator(), new BooleanValueValidator(),
            new ChoiceValueValidator(), new DateValueValidator(), new JsonValueValidator(),
        ]);

    [TestCase("relation", "\"spouse\"", true)]
    [TestCase("relation", "\"boss-man\"", false)]
    [TestCase("closeness", "4", true)]
    [TestCase("closeness", "4.5", false)]
    [TestCase("closeness", "9", false)]
    [TestCase("accommodation", "0.3", true)]
    [TestCase("birthday", "\"--09-30\"", true)]
    [TestCase("birthday", "\"30 Sept\"", false)]
    [TestCase("nicknames", "[\"Ma\", \"Tokkie\"]", true)]
    [TestCase("values_top", "[\"security\", \"tradition\"]", true)]
    [TestCase("values_top", "[\"security\", \"tradition\", \"power\", \"hedonism\"]", false)]
    [TestCase("humour_ok", "\"yes\"", false)]
    [TestCase("follow_ups", "[{\"text\": \"ask about the interview\", \"due\": \"2026-10-05\"}]", true)]
    public void Checks_values_against_the_field(string field, string json, bool ok)
    {
        Assert.That(_catalog.Check(field, JsonDocument.Parse(json).RootElement, FactSource.User).Ok, Is.EqualTo(ok));
    }

    [Test]
    public void Sensitive_and_stated_only_fields_refuse_inference()
    {
        var text = JsonDocument.Parse("\"x\"").RootElement;
        var band = JsonDocument.Parse("\"high\"").RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(_catalog.Check("sensitive_notes", text, FactSource.Inferred).Problem, Does.Contain("only the user"));
            Assert.That(_catalog.Check("sensitive_notes", text, FactSource.Contact).Ok, Is.False);
            Assert.That(_catalog.Check("sensitive_notes", text, FactSource.User).Ok, Is.True);
            Assert.That(_catalog.Check("attachment_anxiety", band, FactSource.Inferred).Problem, Does.Contain("not inferred"));
            Assert.That(_catalog.Check("attachment_anxiety", band, FactSource.Contact).Ok, Is.True);
            Assert.That(_catalog.Check("big_five_openness", band, FactSource.Inferred).Ok, Is.True);
            Assert.That(_catalog.Check("star_sign", text, FactSource.User).Problem, Does.Contain("not a profile field"));
        });
    }
}
