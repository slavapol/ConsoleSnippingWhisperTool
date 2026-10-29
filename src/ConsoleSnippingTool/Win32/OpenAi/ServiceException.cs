namespace ConsoleSnippingTool.Win32.OpenAi;

/// A request the Service answered with an error status. The message is the failure reason the core reports, in the
/// shape OpenAiJson.FailureReason gives it ("401 Incorrect API key provided"); nothing of the request is in it.
internal sealed class ServiceException(string reason) : Exception(reason);
