using System.Text;
using System.Text.Json;
using ConsoleSnippingTool.Win32.OpenAi;

namespace ConsoleSnippingTool.Tests;

// The seam is OpenAiJson, the pure part of the transport to the Service: the request bodies the adapter sends and the
// parsing of what comes back, exercised without HTTP. The plumbing around it (the HttpClient, the thread pool, the
// posting back to the message loop) is adapter code and is verified by building, as the spec's Testing Decisions say.
public class OpenAiJsonTests
{
    [Fact]
    public void The_Transcript_is_the_text_field_of_the_transcription_response()
    {
        Assert.Equal("hello", OpenAiJson.ParseTranscript("""{"text":"hello"}"""));
    }

    [Fact]
    public void A_transcription_response_without_text_is_malformed_and_the_message_is_the_failure_reason()
    {
        var malformed = Assert.Throws<FormatException>(() => OpenAiJson.ParseTranscript("""{"languages":[]}"""));

        Assert.Equal("response has no \"text\" field", malformed.Message);
    }

    [Fact]
    public void An_HTTP_error_reason_is_the_status_code_and_the_first_line_of_the_error_message()
    {
        // The shape OpenAI returns with a 4xx or 5xx status; the message is cut at its first line.
        const string body = """
            {"error":{"message":"Incorrect API key provided\nYou can find your API key at https://platform.openai.com/account/api-keys.","type":"invalid_request_error","param":null,"code":"invalid_api_key"}}
            """;

        Assert.Equal("401 Incorrect API key provided", OpenAiJson.FailureReason(401, "Unauthorized", body));
    }

    [Fact]
    public void An_HTTP_error_whose_body_is_not_the_error_shape_falls_back_to_the_status_and_reason_phrase()
    {
        Assert.Equal("502 Bad Gateway", OpenAiJson.FailureReason(502, "Bad Gateway", "<html><body>Bad Gateway</body></html>"));
        Assert.Equal("500 Internal Server Error", OpenAiJson.FailureReason(500, "Internal Server Error", """{"detail":"oops"}"""));
    }

    [Fact]
    public void The_Cleanup_body_carries_the_model_the_instructions_the_Transcript_as_input_and_no_reasoning_effort()
    {
        var body = OpenAiJson.CleanupBody("gpt-6-luna", "You are a literal dictation cleanup layer.", "um, so, hello");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
        Assert.Equal("You are a literal dictation cleanup layer.", root.GetProperty("instructions").GetString());
        Assert.Equal("um, so, hello", root.GetProperty("input").GetString());
        Assert.Equal("none", root.GetProperty("reasoning").GetProperty("effort").GetString());
    }

    [Fact]
    public void The_cleaned_Transcript_is_every_output_text_part_of_the_Responses_output_concatenated()
    {
        // The Responses object: a reasoning item first, then the message whose content parts carry the text.
        const string response = """
            {"id":"resp_1","object":"response","status":"completed","output":[
              {"type":"reasoning","id":"rs_1","summary":[]},
              {"type":"message","id":"msg_1","role":"assistant","status":"completed","content":[
                {"type":"output_text","text":"Hello, ","annotations":[]},
                {"type":"output_text","text":"world.","annotations":[]}]}]}
            """;

        Assert.Equal("Hello, world.", OpenAiJson.ParseCleanupOutput(response));
    }

    [Fact]
    public void A_refusal_only_output_yields_an_empty_cleaned_Transcript()
    {
        const string response = """
            {"id":"resp_2","object":"response","status":"completed","output":[
              {"type":"message","id":"msg_2","role":"assistant","status":"completed","content":[
                {"type":"refusal","refusal":"I can't help with that."}]}]}
            """;

        Assert.Equal("", OpenAiJson.ParseCleanupOutput(response));
    }

    [Fact]
    public async Task The_transcription_form_carries_the_model_the_json_format_the_language_the_prompt_and_the_WAV_as_dictation_wav()
    {
        var wav = "RIFF....WAVE"u8.ToArray();
        using var form = OpenAiJson.TranscriptionForm(wav, "gpt-transcribe", language: "en", prompt: "ConsoleSnippingTool, WASAPI");

        var parts = await PartsOf(form);
        Assert.Equal("gpt-transcribe", parts["model"].Text);
        Assert.Equal("json", parts["response_format"].Text);
        Assert.Equal("en", parts["languages[]"].Text);
        Assert.Equal("ConsoleSnippingTool, WASAPI", parts["prompt"].Text);
        Assert.Equal(wav, parts["file"].Bytes);
        Assert.Equal("audio/wav", parts["file"].MediaType);
        // The header as curl writes it, quotes included; .NET's default leaves tokens unquoted and adds filename*.
        Assert.Equal("form-data; name=\"file\"; filename=\"dictation.wav\"", parts["file"].Disposition);
    }

    [Fact]
    public async Task The_transcription_form_leaves_out_the_language_and_the_prompt_when_they_are_not_set()
    {
        using var form = OpenAiJson.TranscriptionForm("RIFF....WAVE"u8.ToArray(), "gpt-transcribe", language: null, prompt: null);

        var parts = await PartsOf(form);
        Assert.Equal(["file", "model", "response_format"], parts.Keys.Order());
    }

    private sealed record Part(string Disposition, string? MediaType, byte[] Bytes)
    {
        public string Text => Encoding.UTF8.GetString(Bytes);
    }

    // The parts by their form field name, read back from the content the HttpClient would send.
    private static async Task<Dictionary<string, Part>> PartsOf(MultipartFormDataContent form)
    {
        var parts = new Dictionary<string, Part>();
        foreach (var content in form)
        {
            var disposition = content.Headers.ContentDisposition!;
            parts[disposition.Name!.Trim('"')] =
                new Part(disposition.ToString(), content.Headers.ContentType?.MediaType, await content.ReadAsByteArrayAsync());
        }

        return parts;
    }
}
