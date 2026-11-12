using System;
using System.IO;
using Xanadu.Host;
using Xanadu.Tools.Atlas;
using Xanadu.Tools.Autopsy;
using Xanadu.Tools.DevTools;

// dotnet run --project Tools/DevTools [atlas|autopsy] [flags]   -> opens on that tab (atlas by default)
//
// atlas --focus=TextEditor   -> opens on that type          atlas --compare=A,B    -> comparing two types
// atlas --sort=used          -> sorted: name, used, uses, type or project; --sort=used- flips it
// atlas --similar            -> opens on SIMILAR (with --focus=X: types like X)
// atlas --dump=TextEditor    -> prints a type's links as text, no window
// atlas --similar=Glow       -> prints the types most like one as text, no window (--similar= alone: the top pairs)
// atlas --dump-all=<path>    -> writes the whole map as greppable text, no window (run.sh devtools keeps one fresh)
// autopsy --methods          -> opens on the METHODS view
// autopsy --dump[=app]       -> prints the latest (or that app's) capture as text, no window
//         --under=<name>     -> with --dump: the methods run beneath methods named like that
// --shot=<png>               -> saves frame 60 and quits (--shot-frame=<n> to pick)
string? shot = null, focus = null, compare = null;
int frame = 60, tab = DevToolsApp.AtlasTab;
var sort = AtlasStage.SortMode.Name;
bool reverse = false, similar = false, methods = false;
string root = FindRoot();
int first = 0;
if (args.Length > 0 && args[0] is "atlas" or "autopsy")
{
    tab = args[0] == "atlas" ? DevToolsApp.AtlasTab : DevToolsApp.AutopsyTab;
    first = 1;
}

for (int i = first; i < args.Length; i++)
{
    string arg = args[i];
    if (tab == DevToolsApp.AtlasTab && arg.StartsWith("--dump=", StringComparison.Ordinal))
    {
        AtlasDump.Links(CodeMap.Read(root), arg["--dump=".Length..]);
        return;
    }

    if (tab == DevToolsApp.AtlasTab && arg.StartsWith("--dump-all=", StringComparison.Ordinal))
    {
        AtlasDump.All(CodeMap.Read(root), arg["--dump-all=".Length..]);
        return;
    }

    if (tab == DevToolsApp.AtlasTab && arg.StartsWith("--similar=", StringComparison.Ordinal))
    {
        AtlasDump.Similar(CodeMap.Read(root), arg["--similar=".Length..]);
        return;
    }

    if (tab == DevToolsApp.AutopsyTab && (arg == "--dump" || arg.StartsWith("--dump=", StringComparison.Ordinal)))
    {
        HostApp.UseGameRoots();
        string? under = Array.Find(args, a => a.StartsWith("--under=", StringComparison.Ordinal))?["--under=".Length..];
        AutopsyDump.Print(arg.Length > "--dump=".Length ? arg["--dump=".Length..] : null, under);
        return;
    }

    if (arg.StartsWith("--under=", StringComparison.Ordinal))
        continue;
    if (arg == "--similar")
        similar = true;
    else if (arg == "--methods")
        methods = true;
    else if (arg.StartsWith("--focus=", StringComparison.Ordinal))
        focus = arg["--focus=".Length..];
    else if (arg.StartsWith("--compare=", StringComparison.Ordinal))
        compare = arg["--compare=".Length..];
    else if (arg.StartsWith("--sort=", StringComparison.Ordinal))
    {
        string mode = arg["--sort=".Length..];
        reverse = mode.EndsWith('-');
        sort = Enum.Parse<AtlasStage.SortMode>(mode.TrimEnd('-'), ignoreCase: true);
    }
    else if (arg.StartsWith("--shot=", StringComparison.Ordinal))
        shot = Path.GetFullPath(arg["--shot=".Length..]);
    else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal))
        frame = int.Parse(arg["--shot-frame=".Length..]);
    else
        throw new ArgumentException($"Unknown argument: {arg}");
}

new DevToolsApp(
    new HostWindowOptions(1600, 900, shot, frame),
    new DevToolsOptions(root, tab, new AtlasOptions(focus, compare, sort, reverse, similar), methods)).Run();

/// <summary>The repository: the folder above this tool's build holding Xanadu.sln.</summary>
static string FindRoot()
{
    for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Xanadu.sln")))
            return dir.FullName;
    }

    return Directory.GetCurrentDirectory();
}
