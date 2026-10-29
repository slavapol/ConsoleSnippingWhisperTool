using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// The two request records carry the API key to the adapter, so whatever formats one (a log line, an exception
// message, a debugger, string interpolation) must see the other members and never the key (spec: user story 39).
public class RequestTests
{
    [Fact]
    public void A_TranscriptionRequest_prints_its_model_language_and_prompt_but_never_its_key()
    {
        var request = new TranscriptionRequest("sk-secret", "gpt-transcribe", "en", "ACME, Kubernetes");

        var text = $"{request}";

        Assert.Equal("TranscriptionRequest { Model = gpt-transcribe, Language = en, Prompt = ACME, Kubernetes }", text);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_TranscriptionRequest_prints_null_for_an_unset_language_and_prompt()
    {
        var request = new TranscriptionRequest("sk-secret", "gpt-transcribe", null, null);

        Assert.Equal("TranscriptionRequest { Model = gpt-transcribe, Language = null, Prompt = null }", request.ToString());
    }

    [Fact]
    public void A_CleanupRequest_prints_its_model_and_instructions_but_never_its_key()
    {
        var request = new CleanupRequest("sk-secret", "gpt-6-luna", "Return only the cleaned text.");

        var text = $"{request}";

        Assert.Equal("CleanupRequest { Model = gpt-6-luna, Instructions = Return only the cleaned text. }", text);
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
    }
}
