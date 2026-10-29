using System.Text.Encodings.Web;
using System.Text.Json;

namespace ConsoleSnippingTool.Core;

/// The settings read once at startup from config.json; see <see cref="Load"/> for the fallback rules.
public sealed record Config
{
    public const string FileName = "config.json";

    public Hotkey Hotkey { get; init; } = Hotkey.Default;

    public int ArmedTimeoutSeconds { get; init; } = 30;

    // An hour is more than any forgotten Hotkey press deserves, and far below where the adapter's millisecond
    // timer interval would overflow.
    private const int MinArmedTimeoutSeconds = 1;
    private const int MaxArmedTimeoutSeconds = 3600;

    public SoundSource ReadySound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource CornerSound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource DoneSound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource SentSound { get; init; } = new SoundSource.BuiltIn();

    public SoundSource SoundFor(Signal signal) => signal switch
    {
        Signal.Ready => ReadySound,
        Signal.Corner => CornerSound,
        Signal.Done => DoneSound,
        Signal.Sent => SentSound,
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

    // Hand-edited files get comments and trailing commas; both are harmless to accept.
    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private const string BuiltInClick = "built-in click";

    /// Reads config.json text with per-field fallback: every field that is missing, of the wrong type or invalid
    /// takes its default and adds one problem message naming it, in file order. Unparseable JSON yields all defaults
    /// and one problem. Null text (no file) yields the defaults and no problems. Unknown keys are ignored.
    public static (Config Config, IReadOnlyList<string> Problems) Load(string? json)
    {
        var defaults = new Config();
        if (json is null)
        {
            return (defaults, []);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, Lenient);
        }
        catch (JsonException e)
        {
            return (defaults, [$"{FileName} could not be parsed{Location(e)}; using defaults"]);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (defaults, [$"{FileName} does not hold a JSON object; using defaults"]);
            }

            var problems = new List<string>();
            var hotkey = LoadHotkey(root, defaults.Hotkey, problems);
            var armedTimeoutSeconds = LoadArmedTimeout(root, defaults.ArmedTimeoutSeconds, problems);
            var sounds = LoadSounds(root, problems);
            var config = defaults with
            {
                Hotkey = hotkey,
                ArmedTimeoutSeconds = armedTimeoutSeconds,
                ReadySound = LoadSound(sounds, "ready", defaults.ReadySound, problems),
                CornerSound = LoadSound(sounds, "corner", defaults.CornerSound, problems),
                DoneSound = LoadSound(sounds, "done", defaults.DoneSound, problems),
            };
            return (config, problems);
        }
    }

    private static Hotkey LoadHotkey(JsonElement root, Hotkey fallback, List<string> problems)
    {
        const string field = "hotkey";
        if (!root.TryGetProperty(field, out var value))
        {
            problems.Add(Missing(field, fallback.Text));
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String && Hotkey.TryParse(value.GetString(), out var hotkey))
        {
            return hotkey;
        }

        problems.Add(Invalid(field, value, "is not a valid Hotkey", fallback.Text));
        return fallback;
    }

    private static int LoadArmedTimeout(JsonElement root, int fallback, List<string> problems)
    {
        const string field = "armedTimeoutSeconds";
        if (!root.TryGetProperty(field, out var value))
        {
            problems.Add(Missing(field, fallback.ToString()));
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var seconds)
            && seconds is >= MinArmedTimeoutSeconds and <= MaxArmedTimeoutSeconds)
        {
            return seconds;
        }

        problems.Add(Invalid(field, value, $"is not an integer from {MinArmedTimeoutSeconds} to {MaxArmedTimeoutSeconds}", fallback.ToString()));
        return fallback;
    }

    /// The "sounds" object, or null (with one problem) when it is missing or not an object.
    private static JsonElement? LoadSounds(JsonElement root, List<string> problems)
    {
        const string field = "sounds";
        if (!root.TryGetProperty(field, out var value))
        {
            problems.Add(Missing(field, BuiltInClick));
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        problems.Add(Invalid(field, value, "is not an object", BuiltInClick));
        return null;
    }

    private static SoundSource LoadSound(JsonElement? sounds, string name, SoundSource fallback, List<string> problems)
    {
        if (sounds is null)
        {
            return fallback; // already reported once for the whole "sounds" field
        }

        var field = $"sounds.{name}";
        if (!sounds.Value.TryGetProperty(name, out var value))
        {
            problems.Add(Missing(field, BuiltInClick));
            return fallback;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return new SoundSource.BuiltIn();
            case JsonValueKind.String when value.GetString() is { } text && !string.IsNullOrWhiteSpace(text):
                return text.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? new SoundSource.None()
                    : new SoundSource.File(text);
            default:
                problems.Add(Invalid(field, value, "is not null, \"none\" or a .wav path", BuiltInClick));
                return fallback;
        }
    }

    private static string Missing(string field, string fallback) =>
        $"{FileName}: {field} is missing; using default {fallback}";

    private static string Invalid(string field, JsonElement value, string reason, string fallback) =>
        $"{FileName}: {field} {value.GetRawText()} {reason}; using default {fallback}";

    private static string Location(JsonException e) =>
        e.LineNumber is { } line ? $" (line {line + 1})" : "";
}
