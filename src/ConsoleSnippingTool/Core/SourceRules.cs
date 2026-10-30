namespace ConsoleSnippingTool.Core;

/// Everything about a Dictation that depends on its Source, decided here once from config.json so the state machine
/// asks instead of switching on the Source (Playback Dictation spec: Core states and transitions).
/// <param name="Source">What the adapter is asked to open.</param>
/// <param name="Name">The Source as the Console names it: "Microphone hotkey ignored ...", "Listening (microphone)".</param>
/// <param name="OpenFailure">The report when the device cannot be opened.</param>
/// <param name="Hotkey">The Source's Dictation Hotkey; for a held one, the chord the keyboard hook watches.</param>
/// <param name="Held">True when the Hotkey is held for the whole of Listening, as the Microphone's is: the keyboard hook
/// is in, the chord's first key-up is the Release, Esc discards and a repeated press is autorepeat. False when it is
/// pressed, as Playback's is: no hook, Esc left to the user's apps, and a second press is the Release.</param>
/// <param name="Cap">How long Listening may run before the cap ends it as a Release.</param>
/// <param name="Inserts">True when the Transcript is Inserted rather than Copied: the Microphone with dictation.insert
/// on, never Playback (Playback Dictation spec: user story 18).</param>
/// <param name="MinimumAudio">A clip shorter than this is not sent; zero for the Microphone, which sends every Release
/// that is not a Tap.</param>
internal sealed record SourceRules(
    DictationSource Source,
    string Name,
    string OpenFailure,
    Hotkey Hotkey,
    bool Held,
    TimeSpan Cap,
    bool Inserts,
    TimeSpan MinimumAudio)
{
    /// A Playback clip with less audio than this is not sent. Loopback delivers nothing while no app plays, but the
    /// Ready click lands in the clip whenever the default output is also the communication one, followed by the 0.4 s
    /// of silence the sound player keeps rendering after it, so a clip in which nothing else played is about 0.44 s
    /// long (Playback Dictation spec: user story 17).
    private static readonly TimeSpan MinimumPlayback = TimeSpan.FromSeconds(1);

    public static SourceRules For(DictationSource source, Config config) => source switch
    {
        DictationSource.Microphone => new(
            source,
            Name: "Microphone",
            OpenFailure: "No microphone found",
            config.MicrophoneHotkey,
            Held: true,
            Cap: TimeSpan.FromSeconds(config.MicrophoneTimeoutSeconds),
            Inserts: config.Insert,
            MinimumAudio: TimeSpan.Zero),
        DictationSource.Playback => new(
            source,
            Name: "Playback",
            OpenFailure: "Playback could not be opened",
            config.PlaybackHotkey,
            Held: false,
            Cap: TimeSpan.FromSeconds(config.PlaybackTimeoutSeconds),
            Inserts: false,
            MinimumAudio: MinimumPlayback),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };
}
