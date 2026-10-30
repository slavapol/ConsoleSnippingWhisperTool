using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// The two request records carry the API key to the adapter, so whatever formats one (a log line, an exception
// message, a debugger, string interpolation) must see the other members and never the key (spec: user story 39).
// Each also carries the timeout the adapter applies to it, which the core computes (Playback Dictation spec:
// Requests and their timeout).
public class RequestTests
{
    private static readonly TimeSpan TwentySeconds = TimeSpan.FromSeconds(20);

    [Fact]
    public void A_TranscriptionRequest_prints_its_model_languages_prompt_and_timeout_but_never_its_key()
    {
        var request = new TranscriptionRequest("sk-secret", "gpt-transcribe", ["en", "ru"], "ACME, Kubernetes", TwentySeconds);

        var text = $"{request}";

        Assert.Equal("TranscriptionRequest { Model = gpt-transcribe, Languages = en, ru, Prompt = ACME, Kubernetes, Timeout = 20 s }", text);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_TranscriptionRequest_prints_none_for_an_empty_language_list_and_null_for_an_unset_prompt()
    {
        var request = new TranscriptionRequest("sk-secret", "gpt-transcribe", [], null, TwentySeconds);

        Assert.Equal("TranscriptionRequest { Model = gpt-transcribe, Languages = none, Prompt = null, Timeout = 20 s }", request.ToString());
    }

    [Fact]
    public void A_TranscriptionRequest_prints_the_timeout_it_carries_in_seconds()
    {
        var request = new TranscriptionRequest("sk-secret", "gpt-transcribe", ["en"], null, TimeSpan.FromMinutes(2));

        var text = request.ToString();

        Assert.EndsWith(", Timeout = 120 s }", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CleanupRequest_prints_its_model_instructions_and_timeout_but_never_its_key()
    {
        var request = new CleanupRequest("sk-secret", "gpt-6-luna", "Return only the cleaned text.", TwentySeconds);

        var text = $"{request}";

        Assert.Equal("CleanupRequest { Model = gpt-6-luna, Instructions = Return only the cleaned text., Timeout = 20 s }", text);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CleanupRequest_prints_the_timeout_it_carries_in_seconds()
    {
        var request = new CleanupRequest("sk-secret", "gpt-6-luna", "Return only the cleaned text.", TimeSpan.FromSeconds(21));

        var text = request.ToString();

        Assert.EndsWith(", Timeout = 21 s }", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }
}
