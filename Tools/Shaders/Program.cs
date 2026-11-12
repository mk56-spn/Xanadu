using System;
using System.IO;
using Xanadu.Host;
using Xanadu.Tools.Shaders;

// dotnet run --project Tools/Shaders                        -> the shader tool, on the first shipped effect
// dotnet run --project Tools/Shaders -- --open=<name>       -> opens that one first (note_background, Ui/colour_hue, or its path)
// dotnet run --project Tools/Shaders -- --shot=<png>        -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Tools/Shaders -- --resolution=WxH    -> window size (default 1600x900)
string? shot = null, open = null;
int frame = 60;
uint width = 1600, height = 900;
foreach (string arg in args)
{
    if (arg.StartsWith("--shot=", StringComparison.Ordinal))
        shot = Path.GetFullPath(arg["--shot=".Length..]);
    else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal))
        frame = int.Parse(arg["--shot-frame=".Length..]);
    else if (arg.StartsWith("--open=", StringComparison.Ordinal))
        open = arg["--open=".Length..];
    else if (arg.StartsWith("--resolution=", StringComparison.Ordinal))
    {
        string[] size = arg["--resolution=".Length..].Split('x');
        width = uint.Parse(size[0]);
        height = uint.Parse(size[1]);
    }
    else
        throw new ArgumentException($"Unknown argument: {arg}");
}

new ShaderToolApp(new HostWindowOptions(width, height, shot, frame), open).Run();
