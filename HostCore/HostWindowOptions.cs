using System.IO;

namespace Xanadu.Host;

/// <summary>Window size and the <c>--shot</c> settings every host exe takes.</summary>
internal sealed record HostWindowOptions(uint Width, uint Height, string? ShotPath, int ShotFrame)
{
    /// <summary>
    /// The <c>--shot</c> path due after <paramref name="frames"/> frames, if any. <c>XANADU_SHOT_SERIES=count,step</c>
    /// takes <c>count</c> shots <c>step</c> frames apart from the shot frame, numbered <c>name_0.png</c> on, and
    /// <paramref name="last"/> says when the last is due; without it, one shot.
    /// </summary>
    public string? ShotDue(int frames, out bool last)
    {
        last = true;
        if (ShotPath is not { } path)
            return null;

        int count = 1, step = 1;
        if (Environment.GetEnvironmentVariable("XANADU_SHOT_SERIES") is { } series &&
            series.Split(',') is [var c, var st] && int.TryParse(c, out int parsedCount) && int.TryParse(st, out int parsedStep) &&
            parsedCount > 0 && parsedStep > 0)
        {
            count = parsedCount;
            step = parsedStep;
        }

        int since = frames - ShotFrame;
        if (since < 0 || since % step != 0 || since / step >= count)
            return null;

        int index = since / step;
        last = index == count - 1;
        return count == 1 ? path : Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}_{index}{Path.GetExtension(path)}");
    }
}
