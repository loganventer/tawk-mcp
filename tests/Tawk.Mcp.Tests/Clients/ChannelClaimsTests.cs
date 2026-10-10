using Tawk.Mcp.Clients.Channels;

namespace Tawk.Mcp.Tests.Clients;

public class ChannelClaimsTests
{
    [Test]
    public void Only_a_session_told_it_has_the_channel_promises_to_answer_tawk()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChannelClaims.AnswersTawk(ChannelMode.On), Is.True);
            Assert.That(ChannelClaims.AnswersTawk(ChannelMode.Auto), Is.False, "it only guesses that the client listens");
            Assert.That(ChannelClaims.AnswersTawk(ChannelMode.Off), Is.False);
        });
    }

    [Test]
    public void A_listening_session_says_so_in_its_label()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChannelClaims.LabelMark(ChannelMode.On), Is.EqualTo(", channel"));
            Assert.That(ChannelClaims.LabelMark(ChannelMode.Auto), Is.Empty);
            Assert.That(ChannelClaims.LabelMark(ChannelMode.Off), Is.Empty);
        });
    }
}
