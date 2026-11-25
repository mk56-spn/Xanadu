using System.Globalization;

namespace Xanadu.Host;

/// <summary>
/// MoonWorks logs every tick that runs past the update budget. Overruns under
/// <see cref="IgnoreUnderMs"/> are noise, so only the bigger ones reach the console.
/// </summary>
internal static class HostLateTickLog
{
    private const string Prefix = "Exceeded max update time. Update time reduced from ";
    private const double IgnoreUnderMs = 4.3;

    public static void Install()
    {
        System.Action<string> info = MoonWorks.Logger.LogInfo;
        MoonWorks.Logger.LogInfo = message =>
        {
            if (!IsSmallOverrun(message))
                info(message);
        };
    }

    /// <summary>"... reduced from {accumulated} ms to {max} ms": true when accumulated − max is under the threshold.</summary>
    internal static bool IsSmallOverrun(string message)
    {
        if (!message.StartsWith(Prefix, System.StringComparison.Ordinal))
            return false;
        string[] parts = message[Prefix.Length..].Split(' ');
        // parts: {accumulated} ms to {max} ms
        return parts.Length >= 4
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out double accumulated)
            && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.CurrentCulture, out double max)
            && accumulated - max < IgnoreUnderMs;
    }
}
