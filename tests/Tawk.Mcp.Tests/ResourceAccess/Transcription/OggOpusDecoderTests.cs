using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class OggOpusDecoderTests
{
    private string _file = null!;

    [SetUp]
    public void SetUp() => _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [TearDown]
    public void TearDown() => File.Delete(_file);

    [Test]
    public async Task A_file_that_is_not_ogg_is_refused_by_what_it_holds_not_by_its_name()
    {
        await File.WriteAllBytesAsync(_file, "ID3 this is an mp3"u8.ToArray());

        Assert.That(() => new OggOpusDecoder().Decode(_file, 600),
            Throws.TypeOf<TranscriptionException>().With.Message.Contains("unsupported format"));
    }

    [Test]
    public async Task A_broken_ogg_file_and_a_missing_one_fail_with_a_short_reason()
    {
        await File.WriteAllBytesAsync(_file, "OggS and then nothing that makes sense"u8.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(() => new OggOpusDecoder().Decode(_file, 600), Throws.TypeOf<TranscriptionException>());
            Assert.That(() => new OggOpusDecoder().Decode(_file + ".gone", 600), Throws.TypeOf<TranscriptionException>());
        });
    }

    private float[] Decode(IEnumerable<byte[]> packets, int maxSeconds = 600)
    {
        File.WriteAllBytes(_file, OggFiles.Write(packets));
        return new OggOpusDecoder().Decode(_file, maxSeconds);
    }

    // A packet that says it holds no frames at all, which no decoder accepts.
    private static readonly byte[] Unreadable = [0x0B, 0x00];

    [Test]
    public void A_voice_note_comes_out_at_its_own_length()
    {
        var samples = Decode([.. OggFiles.Opening(), .. OggFiles.Tone(50)]);

        Assert.Multiple(() =>
        {
            Assert.That(samples, Has.Length.EqualTo(50 * OggFiles.PacketSamples));
            Assert.That(samples.Max(), Is.GreaterThan(0.1f), "there is sound in it");
        });
    }

    [Test]
    public void A_note_recorded_in_a_wide_mode_comes_out_with_its_sound_and_not_as_silence()
    {
        var samples = Decode([.. OggFiles.Opening(), .. OggFiles.Tone(50, wide: true)]);

        Assert.Multiple(() =>
        {
            Assert.That(samples, Has.Length.EqualTo(50 * OggFiles.PacketSamples));
            Assert.That(samples.Skip(1600).Max(), Is.GreaterThan(0.1f), "a phone that records wider than 16 kHz is still heard");
        });
    }

    [Test]
    [CancelAfter(10000)]
    public void A_packet_the_decoder_refuses_is_left_out_and_the_rest_is_kept()
    {
        var tone = OggFiles.Tone(50);

        var samples = Decode([.. OggFiles.Opening(), .. tone.Take(25), Unreadable, .. tone.Skip(25)]);

        Assert.That(samples, Has.Length.InRange(50 * OggFiles.PacketSamples, 56 * OggFiles.PacketSamples), "it ends, and only that packet is lost");
    }

    [Test]
    [CancelAfter(10000)]
    public void A_voice_note_that_is_mostly_unreadable_is_given_up_on()
    {
        var packets = OggFiles.Opening().Concat(OggFiles.Tone(4)).Concat(Enumerable.Repeat(Unreadable, 4));

        Assert.That(() => Decode(packets), Throws.TypeOf<TranscriptionException>().With.Message.EqualTo("the voice note could not be decoded"));
    }

    [Test]
    public void An_ogg_file_that_is_not_opus_and_one_with_no_sound_say_so()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Decode([[1, .. "vorbis"u8], [3, .. "vorbis"u8], [5, 6, 7]]), Throws.TypeOf<TranscriptionException>().With.Message.Contains("unsupported format"));
            Assert.That(() => Decode(OggFiles.Opening()), Throws.TypeOf<TranscriptionException>().With.Message.EqualTo("the voice note holds no sound"));
        });
    }

    [Test]
    public void A_recording_longer_than_allowed_is_stopped()
    {
        Assert.That(() => Decode([.. OggFiles.Opening(), .. OggFiles.Tone(120)], maxSeconds: 2), Throws.TypeOf<TranscriptionException>().With.Message.EqualTo("too long"));
    }
}
