using System;
using System.IO;
using System.Linq;
using Xanadu.Host;
using Xanadu.Tools.Atlas;

// dotnet run --project Tools/Atlas                       -> the tool
// dotnet run --project Tools/Atlas -- --focus=TextEditor  -> opens on that type
// dotnet run --project Tools/Atlas -- --compare=A,B       -> opens comparing two types
// dotnet run --project Tools/Atlas -- --dump=TextEditor   -> prints a type's links as text, no window
// dotnet run --project Tools/Atlas -- --sort=used       -> opens sorted: name, used, uses, type or project
// dotnet run --project Tools/Atlas -- --shot=<png>        -> saves frame 60 and quits (--shot-frame=<n> to pick)
string? shot = null, focus = null, compare = null;
int frame = 60;
var sort = AtlasApp.SortMode.Name;
string root = FindRoot();
foreach (string arg in args)
{
    if (arg.StartsWith("--dump=", StringComparison.Ordinal))
    {
        Dump(CodeMap.Read(root), arg["--dump=".Length..]);
        return;
    }

    if (arg.StartsWith("--focus=", StringComparison.Ordinal))
        focus = arg["--focus=".Length..];
    else if (arg.StartsWith("--compare=", StringComparison.Ordinal))
        compare = arg["--compare=".Length..];
    else if (arg.StartsWith("--sort=", StringComparison.Ordinal))
        sort = Enum.Parse<AtlasApp.SortMode>(arg["--sort=".Length..], ignoreCase: true);
    else if (arg.StartsWith("--shot=", StringComparison.Ordinal))
        shot = Path.GetFullPath(arg["--shot=".Length..]);
    else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal))
        frame = int.Parse(arg["--shot-frame=".Length..]);
    else
        throw new ArgumentException($"Unknown argument: {arg}");
}

new AtlasApp(new HostWindowOptions(1600, 900, shot, frame), root, focus, compare, sort).Run();

static void Dump(CodeMap map, string name)
{
    Console.WriteLine($"{map.Nodes.Count} types from {string.Join(", ", map.Assemblies.Select(a => a.Name))}");
    var (heaviest, components, tags) = map.Heaviest();
    foreach (var group in map.Nodes.GroupBy(n => n.Assembly).OrderBy(g => g.Key))
        Console.WriteLine($"  {group.Key}: {group.Count(n => n.Kind == NodeKind.Component)} components, {group.Count(n => n.Kind == NodeKind.Tag)} tags");
    Console.WriteLine($"heaviest exe {heaviest}: {components}/{CodeMap.MaxComponents} component types, {tags}/{CodeMap.MaxTags} tags (Friflo's own included)");
    if (map.Find(name) is not { } node)
    {
        Console.WriteLine($"no type named {name}");
        return;
    }

    Console.WriteLine($"{node.FullName}  [{node.Kind}, {node.Assembly}]");
    foreach (var (title, links) in new[] { ("out", node.Out), ("in", node.In) })
    {
        foreach (var (kind, ids) in links.OrderBy(pair => pair.Key))
            Console.WriteLine($"  {title} {kind}: {string.Join(", ", ids.Select(id => map.Nodes[id].Name))}");
    }
}

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
