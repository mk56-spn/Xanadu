using System;
using System.Linq;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas as text, without a window: a type's links, or the components most like one.</summary>
internal static class AtlasDump
{
    public static void Similar(CodeMap map, string name)
    {
        if (map.Find(name) is not { Kind: NodeKind.Component } node)
        {
            Console.WriteLine($"no component named {name}");
            return;
        }

        Console.WriteLine($"{node.FullName}: {string.Join(", ", node.Fields.Select(f => $"{f.Type} {f.Name}"))}");
        foreach (Match match in new Similarity(map).Like(node.Id).Take(20))
        {
            CodeNode other = map.Nodes[match.B];
            Console.WriteLine($"  {match.Score * 100,3:0}%  shape {match.Shape * 100,3:0}%  users {match.Users * 100,3:0}%  {other.Name}: {string.Join(", ", other.Fields.Select(f => f.Type))}");
        }
    }

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
}
