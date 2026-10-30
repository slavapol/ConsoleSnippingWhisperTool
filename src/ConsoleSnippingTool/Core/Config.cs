using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    // Dictation. dictation.hotkey and dictation.listeningTimeoutSeconds keep their names and mean the Microphone's
    // Hotkey and cap; the model, languages, prompt and Cleanup settings apply to both Sources.

    /// The Microphone Hotkey (dictation.hotkey).
    public Hotkey MicrophoneHotkey { get; init; } = Hotkey.MicrophoneDefault;

    /// The Microphone cap (dictation.listeningTimeoutSeconds).
    public int MicrophoneTimeoutSeconds { get; init; } = 120;

    /// The Hotkey that opens Listening on Playback and, pressed again, is its Release.
    public Hotkey PlaybackHotkey { get; init; } = Hotkey.PlaybackDefault;

    /// The Playback cap: Listening on Playback ends by itself after this many seconds and sends what it heard.
    public int PlaybackTimeoutSeconds { get; init; } = 600;

    // The bounds of both Listening caps. Ten minutes of 16 kHz mono is about 19 MB, under the Service's 25 MB ceiling
    // (ADR-0004).
    private const int MinListeningCapSeconds = 1;
    private const int MaxListeningCapSeconds = 600;

    /// How a Microphone Dictation ends: false leaves the Transcript on the clipboard (Copy, the default), true pastes it
    /// into the focused app (Insert). A Playback Dictation always ends in Copy.
    public bool Insert { get; init; }

    public string TranscriptionModel { get; init; } = "gpt-transcribe";

    /// The languages the user expects to speak, sent to the Service as a hint; empty means none is sent.
    public LanguageList Languages { get; init; } = LanguageList.Default;

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
        Converters = { new OneLineLanguageListConverter() },
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
            hotkey = MicrophoneHotkey.Text,
            listeningTimeoutSeconds = MicrophoneTimeoutSeconds,
            playbackHotkey = PlaybackHotkey.Text,
            playbackTimeoutSeconds = PlaybackTimeoutSeconds,
            insert = Insert,
            model = TranscriptionModel,
            languages = Languages,
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
    /// takes its default and adds one problem message naming it, in file order. The two Playback fields are the
    /// exception: missing, they take their defaults without a problem, so a file from before them loads quietly. An
    /// object field (sounds, openAi, dictation) that is missing or not an object is one problem for the whole object. Unparseable JSON yields all
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
            var microphoneHotkey = LoadHotkey(dictation, "dictation.hotkey", defaults.MicrophoneHotkey, problems);
            if (microphoneHotkey is { } parsed && parsed.SameKeys(hotkey))
            {
                // Spec: two identical Hotkeys are a config problem and the Microphone one is reset, so both jobs stay
                // reachable. The written dictation.hotkey is compared against the Capture Hotkey in effect, that is
                // after its own fallback, and the message says so.
                problems.Add($"{FileName}: dictation.hotkey {parsed.Text} is the same as the Capture Hotkey in effect; using default {defaults.MicrophoneHotkey.Text}");
                microphoneHotkey = null;
            }

            var microphoneTimeoutSeconds = LoadTimeout(dictation, "dictation.listeningTimeoutSeconds", MinListeningCapSeconds, MaxListeningCapSeconds, defaults.MicrophoneTimeoutSeconds, problems);
            // The two Playback fields came after Dictation, so a file written before them lacks them: missing, each takes
            // its default silently, and the upgrade costs nothing (Playback Dictation spec: user story 36).
            var playbackHotkey = LoadHotkey(dictation, "dictation.playbackHotkey", defaults.PlaybackHotkey, problems, optional: true);
            // Spec: the same rule for the Playback Hotkey, compared against the Capture Hotkey in effect and then the
            // Microphone Hotkey in effect, each after its own fallback. A missing or invalid playbackHotkey is not
            // compared, so a default that collides is left to fail its registration and be reported as taken.
            var clash = playbackHotkey switch
            {
                { } written when written.SameKeys(hotkey) => "Capture Hotkey",
                { } written when written.SameKeys(microphoneHotkey ?? defaults.MicrophoneHotkey) => "Microphone Hotkey",
                _ => null,
            };
            if (playbackHotkey is { } playback && clash is not null)
            {
                problems.Add($"{FileName}: dictation.playbackHotkey {playback.Text} is the same as the {clash} in effect; using default {defaults.PlaybackHotkey.Text}");
                playbackHotkey = null;
            }

            var playbackTimeoutSeconds = LoadTimeout(dictation, "dictation.playbackTimeoutSeconds", MinListeningCapSeconds, MaxListeningCapSeconds, defaults.PlaybackTimeoutSeconds, problems, optional: true);
            var insert = LoadBool(dictation, "dictation.insert", defaults.Insert, problems);
            var transcriptionModel = LoadString(dictation, "dictation.model", defaults.TranscriptionModel, problems);
            var languages = LoadLanguages(dictation, "dictation.languages", defaults.Languages, problems);
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
                MicrophoneHotkey = microphoneHotkey ?? defaults.MicrophoneHotkey,
                MicrophoneTimeoutSeconds = microphoneTimeoutSeconds,
                PlaybackHotkey = playbackHotkey ?? defaults.PlaybackHotkey,
                PlaybackTimeoutSeconds = playbackTimeoutSeconds,
                Insert = insert,
                TranscriptionModel = transcriptionModel,
                Languages = languages,
                Prompt = prompt,
                Cleanup = cleanup,
                CleanupModel = cleanupModel,
            };
            return (config, problems);
        }
    }

    // Every loader takes the object the field sits in (null when that object already fell back and was reported, so
    // the field takes its default silently) and the field's dotted path for the message ("dictation.model").

    /// The field, or null when the parent is absent or the field is missing (reported here with the fallback text,
    /// unless the field is optional: one added after files were already written, whose absence is no problem).
    private static JsonElement? FieldOrMissing(JsonElement? parent, string path, string fallback, List<string> problems, bool optional = false)
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

        if (!optional)
        {
            problems.Add(Missing(path, fallback));
        }

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
    private static Hotkey? LoadHotkey(JsonElement? parent, string path, Hotkey fallback, List<string> problems, bool optional = false)
    {
        if (FieldOrMissing(parent, path, fallback.Text, problems, optional) is not { } value)
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

    private static int LoadTimeout(JsonElement? parent, string path, int min, int max, int fallback, List<string> problems, bool optional = false)
    {
        if (FieldOrMissing(parent, path, fallback.ToString(), problems, optional) is not { } value)
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

        if (IsNonEmptyString(value))
        {
            return value.GetString()!;
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
            case JsonValueKind.String when allowEmpty || IsNonEmptyString(value):
                return value.GetString();
            default:
                problems.Add(Invalid(path, value, allowEmpty ? "is not null or a string" : "is not null or a non-empty string", fallback));
                return null;
        }
    }

    /// null or [] (no hint is sent), or an array of non-empty strings kept as written: the codes are the Service's to
    /// accept or reject at Dictation time. Anything else falls back to the default list, reported as its text (en, ru).
    private static LanguageList LoadLanguages(JsonElement? parent, string path, LanguageList fallback, List<string> problems)
    {
        if (FieldOrMissing(parent, path, fallback.ToString(), problems) is not { } value)
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return LanguageList.None;
        }

        if (value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(IsNonEmptyString))
        {
            return new LanguageList(value.EnumerateArray().Select(code => code.GetString()!));
        }

        problems.Add(Invalid(path, value, "is not null or an array of non-empty strings", fallback.ToString()));
        return fallback;
    }

    /// A JSON string with something in it: what a model name, a key and a language code must each be.
    private static bool IsNonEmptyString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString());

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

    /// Writes the language list on one line, ["en", "ru"], as the spec's default file shows it; the indented writer
    /// would otherwise put each code on a line of its own. Only ever writes: Load reads the file through JsonDocument.
    private sealed class OneLineLanguageListConverter : JsonConverter<LanguageList>
    {
        public override LanguageList Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException($"{FileName} is read through {nameof(Config)}.{nameof(Load)}");

        public override void Write(Utf8JsonWriter writer, LanguageList value, JsonSerializerOptions options) =>
            writer.WriteRawValue($"[{string.Join(", ", value.Select(code => JsonSerializer.Serialize(code, options)))}]");
    }
}
