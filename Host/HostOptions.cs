using System;
using System.IO;

namespace Xanadu.Host;

/// <summary>Command-line options for the host.</summary>
/// <param name="ShotPath">Where <c>--shot=&lt;path&gt;</c> saves a PNG of the frame before quitting; null to run normally.</param>
/// <param name="ShotFrame">Which frame the shot is taken on, so the game has settled (<c>--shot-frame=&lt;n&gt;</c>).</param>
internal sealed record HostOptions(string? ShotPath, int ShotFrame, uint WindowWidth, uint WindowHeight)
{
    public static HostOptions Parse(string[] args)
    {
        string? shot = null;
        int frame = 60;
        uint width = 1280, height = 720;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--shot=", StringComparison.Ordinal))
                shot = Path.GetFullPath(arg["--shot=".Length..]);
            else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal))
                frame = int.Parse(arg["--shot-frame=".Length..]);
            else if (arg.StartsWith("--resolution=", StringComparison.Ordinal))
            {
                string[] size = arg["--resolution=".Length..].Split('x');
                width = uint.Parse(size[0]);
                height = uint.Parse(size[1]);
            }
            else
                throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new HostOptions(shot, frame, width, height);
    }
}
