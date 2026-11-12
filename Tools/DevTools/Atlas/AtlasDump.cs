using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas as text, without a window: a type's links, or the types most like one.</summary>
internal static class AtlasDump
{
    /// <summary>The types most like <paramref name="name"/>, or with no name, the most alike pairs of all; every kind but enums, as the chart starts.</summary>
    public static void Similar(CodeMap map, string name)
    {
        var pool = Enum.GetValues<NodeKind>().Where(kind => kind != NodeKind.Enum).ToHashSet();
        if (name.Length == 0)
        {
            foreach (Match pair in new Similarity(map).Pairs(pool, 30))
                Console.WriteLine($"  {pair.Score * 100,3:0}%  {Signals(pair)}  {map.Nodes[pair.A].Name} ({map.Nodes[pair.A].Kind})  ~  {map.Nodes[pair.B].Name} ({map.Nodes[pair.B].Kind})");
            return;
        }

        if (map.Find(name) is not { } node)
        {
            Console.WriteLine($"no type named {name}");
            return;
        }

        Console.WriteLine($"{node.FullName}: {string.Join(", ", node.Fields.Select(f => $"{f.Type} {f.Name}"))}");
        foreach (Match match in new Similarity(map).Like(node.Id, pool).Take(20))
        {
            CodeNode other = map.Nodes[match.B];
            Console.WriteLine($"  {match.Score * 100,3:0}%  {Signals(match)}  {other.Name} ({other.Kind}): {string.Join(", ", other.Fields.Select(f => f.Type))}");
        }
    }

    private static string Signals(Match match) =>
        $"shape {Match.Percent(match.Shape),4}  users {Match.Percent(match.Users),4}  uses {Match.Percent(match.Uses),4}";

    public static void Links(CodeMap map, string name)
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

    /// <summary>
    /// The whole map as text, for reading with grep: a header, then one block
    /// per type opening with <c>## Name [Kind, Assembly] FullName</c>, every
    /// line after it starting with the type's name, so a search finds a fact
    /// either way round (<c>grep "queried-by  ToggleSystem"</c>).
    /// </summary>
    public static void All(CodeMap map, string path)
    {
        var pool = Enum.GetValues<NodeKind>().Where(kind => kind != NodeKind.Enum).ToHashSet();
        var similarity = new Similarity(map);
        var (heaviest, components, tags) = map.Heaviest();
        using var file = new StreamWriter(path);

        file.WriteLine($"# atlas  {DateTime.Now:yyyy-MM-dd HH:mm}  {map.Nodes.Count} types  builds: {string.Join(", ", map.Assemblies.Select(a => $"{a.Name} {a.Built:HH:mm}"))}");
        file.WriteLine($"# budget  heaviest exe {heaviest}: {components}/{CodeMap.MaxComponents} components, {tags}/{CodeMap.MaxTags} tags (Friflo's own included)");
        foreach (var group in map.Nodes.GroupBy(n => n.Assembly).OrderBy(g => g.Key))
            file.WriteLine($"# assembly  {group.Key}: {group.Count()} types, {group.Count(n => n.Kind == NodeKind.Component)} components, {group.Count(n => n.Kind == NodeKind.Tag)} tags");
        file.WriteLine("# lines: <Type>  <relation>  <other>; relations: field, queries, implements, inherits, uses, queried-by, implemented-by, inherited-by, used-by, like");
        file.WriteLine("# like: most alike types of any kind but enums (score, then shape / users / uses; — when neither has any)");

        foreach (CodeNode node in map.Nodes.OrderBy(n => n.Name, StringComparer.Ordinal))
        {
            int usedBy = node.In.Values.SelectMany(set => set).Distinct().Count();
            int uses = node.Out.Values.SelectMany(set => set).Distinct().Count();
            file.WriteLine();
            file.WriteLine($"## {node.Name}  [{node.Kind}, {node.Assembly}]  {node.FullName}  used-by {usedBy}  uses {uses}");
            foreach (var (name, type) in node.Fields)
                file.WriteLine($"{node.Name}  field  {TypeNames.Short(type)} {name}");

            Links(file, map, node, node.Out, Out);
            Links(file, map, node, node.In, In);
            foreach (Match match in similarity.Like(node.Id, pool).Take(5))
                file.WriteLine($"{node.Name}  like  {map.Nodes[match.B].Name}  {match.Score * 100:0}%  {Signals(match)}");
        }
    }

    private static readonly Dictionary<LinkKind, string> Out = new()
    {
        [LinkKind.Queries] = "queries", [LinkKind.Implements] = "implements", [LinkKind.Inherits] = "inherits", [LinkKind.Uses] = "uses",
    };

    private static readonly Dictionary<LinkKind, string> In = new()
    {
        [LinkKind.Queries] = "queried-by", [LinkKind.Implements] = "implemented-by", [LinkKind.Inherits] = "inherited-by", [LinkKind.Uses] = "used-by",
    };

    private static void Links(TextWriter file, CodeMap map, CodeNode node, Dictionary<LinkKind, SortedSet<int>> links, Dictionary<LinkKind, string> names)
    {
        foreach (var (kind, ids) in links.OrderBy(pair => pair.Key))
        {
            foreach (string other in ids.Select(id => map.Nodes[id].Name).Order(StringComparer.Ordinal))
                file.WriteLine($"{node.Name}  {names[kind]}  {other}");
        }
    }
}
