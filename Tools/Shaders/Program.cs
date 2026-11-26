using System.IO;
using Xanadu.Host;
using Xanadu.Tools.Shaders;

// dotnet run --project Tools/Shaders                        -> the shader tool, on the first shipped effect
// dotnet run --project Tools/Shaders -- --open=<name>       -> opens that one first (Game/note_background, Ui/colour_hue, or its path)
// dotnet run --project Tools/Shaders -- --graph            -> starts with the render graph where the code is (GRAPH on)
// dotnet run --project Tools/Shaders -- --fill             -> the preview over the whole window (a full-size shot)
// dotnet run --project Tools/Shaders -- --expand           -> starts with the preview expanded (its corner button brings the editor back)
// dotnet run --project Tools/Shaders -- --picker           -> starts with the SHADERS grid open, on the open stack's tab
// dotnet run --project Tools/Shaders -- --add-file         -> starts with "+ FILE"'s list of kinds of file up (a stack only)
// dotnet run --project Tools/Shaders -- --shot=<png>        -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Tools/Shaders -- --resolution=WxH    -> window size (default 1600x900)
// dotnet run --project Tools/Shaders -- --beside=<name>     -> another stack drawn after the open one, over the same quad (two materials)
// dotnet run --project Tools/Shaders -- --under=<name>      -> another stack drawn before the open one, over the same quad (what it reads under it)
// dotnet run --project Tools/Shaders -- --text=<line>       -> the open stack draws that line (a quad per glyph) instead of its quad
// dotnet run --project Tools/Shaders -- --post=<name>       -> a stack on the renderer's post list, drawn over the whole window last
string? shot = null, open = null;
bool graph = false;
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
    else if (arg.StartsWith("--beside=", StringComparison.Ordinal))
        EffectPreview.Beside = arg["--beside=".Length..];
    else if (arg.StartsWith("--under=", StringComparison.Ordinal))
        EffectPreview.Under = arg["--under=".Length..];
    else if (arg.StartsWith("--text=", StringComparison.Ordinal))
        EffectPreview.Text = arg["--text=".Length..];
    else if (arg.StartsWith("--post=", StringComparison.Ordinal))
        EffectPreview.Post = arg["--post=".Length..];
    else if (arg == "--graph")
        graph = true;
    else if (arg == "--expand")
        Xanadu.Host.Tools.ShaderEditing.PreviewExpand.StartExpanded = true;
    else if (arg == "--picker")
        ShaderGrid.StartOpen = true;
    else if (arg == "--add-file")
        Xanadu.Host.Tools.ShaderEditing.FilePresets.StartOpen = true;
    else if (arg == "--fill")
        Xanadu.Host.Tools.ShaderEditing.ShaderCodeEditor.FillPreview = new Rect2(0, 0, 1920, 1080);
    else if (arg.StartsWith("--resolution=", StringComparison.Ordinal))
    {
        string[] size = arg["--resolution=".Length..].Split('x');
        width = uint.Parse(size[0]);
        height = uint.Parse(size[1]);
    }
    else
        throw new ArgumentException($"Unknown argument: {arg}");
}

new ShaderToolApp(new HostWindowOptions(width, height, shot, frame), open, graph).Run();
