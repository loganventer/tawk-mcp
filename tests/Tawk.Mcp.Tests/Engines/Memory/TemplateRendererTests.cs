using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    public void Fills_what_it_can_and_reports_the_rest()
    {
        var rendering = _renderer.Render(
            "hey {{contact.first_name}}, geluk met jou {{ Age }}ste! sien jou {{day}}",
            new Dictionary<string, string> { ["contact.first_name"] = "Neal", ["age"] = "40" });

        Assert.Multiple(() =>
        {
            Assert.That(rendering.Text, Is.EqualTo("hey Neal, geluk met jou 40ste! sien jou {{day}}"));
            Assert.That(rendering.Missing, Is.EqualTo(new[] { "day" }));
            Assert.That(_renderer.Placeholders("{{a}} {{contact.b}} {{a}}"), Is.EqualTo(new[] { "a", "contact.b" }));
        });
    }
}
