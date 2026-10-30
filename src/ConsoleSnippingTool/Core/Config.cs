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

    // Dictation

    public Hotkey DictationHotkey { get; init; } = Hotkey.DictationDefault;

    public int ListeningTimeoutSeconds { get; init; } = 120;

    // Ten minutes of speech is well under the Service's 25 MB ceiling (about 13 minutes at 16 kHz, ADR-0004).
    private const int MinListeningTimeoutSeconds = 1;
    private const int MaxListeningTimeoutSeconds = 600;

    public string TranscriptionModel { get; init; } = "gpt-transcribe";

    public string? Language { get; init; }

    public string? Prompt { get; init; }

    public bool Cleanup { get; init; }

    public string CleanupModel { get; init; } = "gpt-6-luna";

    /// null = read OPENAI_API_KEY from the environment.
    public string? OpenAiApiKey { get; init; }

    // The relaxed encoder keeps "+" in the Hotkey literal (the default escapes it to +), so the file stays hand-editable.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// The config.json text for this config, in the shape the spec's Configuration section shows (camelCase, indented).
    /// The core only ever writes a default Config, so the file it creates never carries a key (spec: user story 22).
    public string ToJson() => JsonSerializer.Serialize(new
    {
        hotkey = Hotkey.Text,
        armedTimeoutSeconds = ArmedTimeoutSeconds,
        sounds = new
        {
            ready = SoundValue(ReadySound),
            corner = SoundValue(CornerSound),
            sent = SoundValue(SentSound),
            done = SoundValue(DoneSound),
        },
        openAi = new
        {
            apiKey = OpenAiApiKey,
        },
        dictation = new
        {
            hotkey = DictationHotkey.Text,
            listeningTimeoutSeconds = ListeningTimeoutSeconds,
            model = TranscriptionModel,
            language = Language,
            prompt = Prompt,
            cleanup = Cleanup,
            cleanupModel = CleanupModel,
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
    /// takes its default and adds one problem message naming it, in file order. An object field (sounds, openAi,
    /// dictation) that is missing or not an object is one problem for the whole object. Unparseable JSON yields all
    /// defaults and one problem. Null text (no file) yields the defaults and no problems. Unknown keys are ignored.
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

            // Locals in file order, because the problems list is in file order.
            var problems = new List<string>();
            var hotkey = LoadHotkey(root, "hotkey", defaults.Hotkey, problems) ?? defaults.Hotkey;
            var armedTimeoutSeconds = LoadTimeout(root, "armedTimeoutSeconds", MinArmedTimeoutSeconds, MaxArmedTimeoutSeconds, defaults.ArmedTimeoutSeconds, problems);
            var sounds = LoadObject(root, "sounds", problems, fallback: BuiltInClick);
            var readySound = LoadSound(sounds, "sounds.ready", defaults.ReadySound, problems);
            var cornerSound = LoadSound(sounds, "sounds.corner", defaults.CornerSound, problems);
            var sentSound = LoadSound(sounds, "sounds.sent", defaults.SentSound, problems);
            var doneSound = LoadSound(sounds, "sounds.done", defaults.DoneSound, problems);
            var openAi = LoadObject(root, "openAi", problems);
            var openAiApiKey = LoadOptionalString(openAi, "openAi.apiKey", allowEmpty: false, problems);
            var dictation = LoadObject(root, "dictation", problems);
            var dictationHotkey = LoadHotkey(dictation, "dictation.hotkey", defaults.DictationHotkey, problems);
            if (dictationHotkey is { } parsed && parsed.Modifiers == hotkey.Modifiers && parsed.VirtualKey == hotkey.VirtualKey)
            {
                // Spec: two identical Hotkeys are a config problem and the Dictation one is reset, so both jobs stay
                // reachable. The written dictation.hotkey is compared against the Capture Hotkey in effect, that is
                // after its own fallback, and the message says so.
                problems.Add($"{FileName}: dictation.hotkey {parsed.Text} is the same as the Capture Hotkey in effect; using default {defaults.DictationHotkey.Text}");
                dictationHotkey = null;
            }

            var listeningTimeoutSeconds = LoadTimeout(dictation, "dictation.listeningTimeoutSeconds", MinListeningTimeoutSeconds, MaxListeningTimeoutSeconds, defaults.ListeningTimeoutSeconds, problems);
            var transcriptionModel = LoadString(dictation, "dictation.model", defaults.TranscriptionModel, problems);
            var language = LoadOptionalString(dictation, "dictation.language", allowEmpty: false, problems);
            var prompt = LoadOptionalString(dictation, "dictation.prompt", allowEmpty: true, problems);
            var cleanup = LoadBool(dictation, "dictation.cleanup", defaults.Cleanup, problems);
            var cleanupModel = LoadString(dictation, "dictation.cleanupModel", defaults.CleanupModel, problems);
            var config = defaults with
            {
                Hotkey = hotkey,
                ArmedTimeoutSeconds = armedTimeoutSeconds,
                ReadySound = readySound,
                CornerSound = cornerSound,
                SentSound = sentSound,
                DoneSound = doneSound,
                OpenAiApiKey = openAiApiKey,
                DictationHotkey = dictationHotkey ?? defaults.DictationHotkey,
                ListeningTimeoutSeconds = listeningTimeoutSeconds,
                TranscriptionModel = transcriptionModel,
                Language = language,
                Prompt = prompt,
                Cleanup = cleanup,
                CleanupModel = cleanupModel,
            };
            return (config, problems);
        }
    }

    // Every loader takes the object the field sits in (null when that object already fell back and was reported, so
    // the field takes its default silently) and the field's dotted path for the message ("dictation.model").

    /// The field, or null when the parent is absent or the field is missing (reported here with the fallback text).
    private static JsonElement? FieldOrMissing(JsonElement? parent, string path, string fallback, List<string> problems)
    {
        if (parent is null)
        {
            return null;
        }

        var name = path[(path.LastIndexOf('.') + 1)..];
        if (parent.Value.TryGetProperty(name, out var value))
        {
            return value;
        }

        problems.Add(Missing(path, fallback));
        return null;
    }

    /// The object at path, or null (with one problem for the whole object) when it is missing or not an object. The
    /// problem says what its fields take: their several defaults, or the one fallback they share (the sounds' click).
    private static JsonElement? LoadObject(JsonElement root, string path, List<string> problems, string? fallback = null)
    {
        var taken = fallback is null ? "defaults" : $"default {fallback}";
        if (!root.TryGetProperty(path, out var value))
        {
            problems.Add($"{FileName}: {path} is missing; using {taken}");
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        problems.Add($"{FileName}: {path} {value.GetRawText()} is not an object; using {taken}");
        return null;
    }

    /// The parsed Hotkey, or null when the field fell back (already reported) so the caller can tell a fallback apart.
    private static Hotkey? LoadHotkey(JsonElement? parent, string path, Hotkey fallback, List<string> problems)
    {
        if (FieldOrMissing(parent, path, fallback.Text, problems) is not { } value)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && Hotkey.TryParse(value.GetString(), out var hotkey))
        {
            return hotkey;
        }

        problems.Add(Invalid(path, value, "is not a valid Hotkey", fallback.Text));
        return null;
    }

    private static int LoadTimeout(JsonElement? parent, string path, int min, int max, int fallback, List<string> problems)
    {
        if (FieldOrMissing(parent, path, fallback.ToString(), problems) is not { } value)
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var seconds) && seconds >= min && seconds <= max)
        {
            return seconds;
        }

        problems.Add(Invalid(path, value, $"is not an integer from {min} to {max}", fallback.ToString()));
        return fallback;
    }

    private static SoundSource LoadSound(JsonElement? sounds, string path, SoundSource fallback, List<string> problems)
    {
        if (FieldOrMissing(sounds, path, BuiltInClick, problems) is not { } value)
        {
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
                problems.Add(Invalid(path, value, "is not null, \"none\" or a .wav path", BuiltInClick));
                return fallback;
        }
    }

    /// A string that must have content: a model name.
    private static string LoadString(JsonElement? parent, string path, string fallback, List<string> problems)
    {
        if (FieldOrMissing(parent, path, fallback, problems) is not { } value)
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        problems.Add(Invalid(path, value, "is not a non-empty string", fallback));
        return fallback;
    }

    /// null or a string, passed through; the default is null. allowEmpty lets a blank string through (the prompt).
    private static string? LoadOptionalString(JsonElement? parent, string path, bool allowEmpty, List<string> problems)
    {
        const string fallback = "null";
        if (FieldOrMissing(parent, path, fallback, problems) is not { } value)
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String when allowEmpty || !string.IsNullOrWhiteSpace(value.GetString()):
                return value.GetString();
            default:
                problems.Add(Invalid(path, value, allowEmpty ? "is not null or a string" : "is not null or a non-empty string", fallback));
                return null;
        }
    }

    private static bool LoadBool(JsonElement? parent, string path, bool fallback, List<string> problems)
    {
        var fallbackText = fallback ? "true" : "false";
        if (FieldOrMissing(parent, path, fallbackText, problems) is not { } value)
        {
            return fallback;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        problems.Add(Invalid(path, value, "is not true or false", fallbackText));
        return fallback;
    }

    private static string Missing(string field, string fallback) =>
        $"{FileName}: {field} is missing; using default {fallback}";

    private static string Invalid(string field, JsonElement value, string reason, string fallback) =>
        $"{FileName}: {field} {value.GetRawText()} {reason}; using default {fallback}";

    private static string Location(JsonException e) =>
        e.LineNumber is { } line ? $" (line {line + 1})" : "";
}
