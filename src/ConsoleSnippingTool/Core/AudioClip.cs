namespace ConsoleSnippingTool.Core;

/// PCM audio recorded from a Dictation's Source: 16-bit little-endian mono samples at SampleRate Hz.
public sealed record AudioClip(byte[] Pcm16, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Pcm16.Length / 2.0 / SampleRate);
}
