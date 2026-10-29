using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// The seam is the AudioClip value type the microphone hands the core: 16-bit mono PCM, so two bytes per sample.
public class AudioClipTests
{
    [Fact]
    public void Three_seconds_of_16_kHz_samples_last_three_seconds()
    {
        var clip = new AudioClip(new byte[3 * 16000 * 2], 16000);

        Assert.Equal(TimeSpan.FromSeconds(3), clip.Duration);
    }

    [Fact]
    public void An_empty_clip_lasts_nothing()
    {
        var clip = new AudioClip([], 16000);

        Assert.Equal(TimeSpan.Zero, clip.Duration);
    }
}
