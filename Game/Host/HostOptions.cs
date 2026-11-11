using System;
using System.IO;

namespace Xanadu.Host;

/// <summary>Command-line options for the host.</summary>
/// <param name="ShotPath">Where <c>--shot=&lt;path&gt;</c> saves a PNG of the frame before quitting; null to run normally.</param>
/// <param name="ShotFrame">Which frame the shot is taken on, so the game has settled (<c>--shot-frame=&lt;n&gt;</c>).</param>
/// <param name="Play">
/// <c>--play</c> starts the first playable level, <c>--play=&lt;name&gt;</c> the first whose name or file name contains it;
/// null starts on the profiles screen like the Godot build.
/// </param>
/// <param name="Editor"><c>--editor</c> starts in the editor.</param>
/// <param name="Demo"><c>--demo</c> plays a level made in memory (<see cref="DemoLevel"/>).</param>
/// <param name="Scene"><c>--scene=&lt;name&gt;</c> starts on that screen (a <c>SceneKind</c> name, any case), for checking one screen.</param>
internal sealed record HostOptions(string? ShotPath, int ShotFrame, uint WindowWidth, uint WindowHeight, string? Play, bool Editor, bool Demo, string? Scene)
{
    public static HostOptions Parse(string[] args)
    {
        string? shot = null;
        string? play = null;
        string? scene = null;
        bool editor = false, demo = false;
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
            else if (arg == "--play")
                play = "";
            else if (arg.StartsWith("--play=", StringComparison.Ordinal))
                play = arg["--play=".Length..];
            else if (arg == "--editor")
                editor = true;
            else if (arg == "--demo")
                demo = true;
            else if (arg.StartsWith("--scene=", StringComparison.Ordinal))
                scene = arg["--scene=".Length..];
            else
                throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new HostOptions(shot, frame, width, height, play, editor, demo, scene);
    }
}
