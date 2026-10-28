using System.Text.Encodings.Web;
using System.Text.Json;

namespace ConsoleSnippingTool.Core;

/// The settings read once at startup from config.json. Defaults only for now; ticket 04 adds parsing.
public sealed record Config
{
    public Hotkey Hotkey { get; init; } = Hotkey.Default;

    public int ArmedTimeoutSeconds { get; init; } = 30;

    public SoundSource ReadySound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource CornerSound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource DoneSound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource SoundFor(Signal signal) => signal switch
    {
        Signal.Ready => ReadySound,
        Signal.Corner => CornerSound,
        Signal.Done => DoneSound,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    // The relaxed encoder keeps "+" in the Hotkey literal (the default escapes it to +), so the file stays hand-editable.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// The config.json text for this config, in the shape the spec's Configuration section shows (camelCase, indented).
    public string ToJson() => JsonSerializer.Serialize(new
    {
        hotkey = Hotkey.Text,
        armedTimeoutSeconds = ArmedTimeoutSeconds,
        sounds = new
        {
            ready = SoundValue(ReadySound),
            corner = SoundValue(CornerSound),
            done = SoundValue(DoneSound),
        },
    }, Indented);

    private static string? SoundValue(SoundSource source) => source switch
    {
        SoundSource.None => "none",
        SoundSource.File file => file.Path,
        _ => null,
    };
}
