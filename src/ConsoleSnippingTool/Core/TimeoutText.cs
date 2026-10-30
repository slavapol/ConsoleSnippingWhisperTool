using System.Globalization;

namespace ConsoleSnippingTool.Core;

/// How both request records print their Timeout in ToString(): its seconds and " s", such as "21 s", whatever the
/// user's culture.
internal static class TimeoutText
{
    public static string Of(TimeSpan timeout) => $"{timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s";
}
