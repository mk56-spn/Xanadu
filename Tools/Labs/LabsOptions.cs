using System;
using System.IO;

namespace Xanadu.Labs;

/// <summary>Command-line options for the lab.</summary>
/// <param name="Tab"><c>--tab=N</c> opens that tab first.</param>
/// <param name="Shader"><c>--shader=path</c>: the file the shader playground watches; null for its own working copy.</param>
internal sealed record LabsOptions(string? ShotPath, int ShotFrame, uint WindowWidth, uint WindowHeight, int Tab, string? Shader)
{
    public static LabsOptions Parse(string[] args)
    {
        string? shot = null, shader = null;
        int frame = 60, tab = 0;
        uint width = 1600, height = 900;
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
            else if (arg.StartsWith("--tab=", StringComparison.Ordinal))
                tab = int.Parse(arg["--tab=".Length..]);
            else if (arg.StartsWith("--shader=", StringComparison.Ordinal))
                shader = Path.GetFullPath(arg["--shader=".Length..]);
            else
                throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new LabsOptions(shot, frame, width, height, tab, shader);
    }
}
