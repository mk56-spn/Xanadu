using System;
using System.Collections.Generic;

namespace Xanadu.Ecs;

/// <summary>Depth-first ordering and depths for a flat list of tree items.</summary>
internal static class UiTreeOrder
{
    public static List<UiBuilder.TreeRow> DepthFirst(IReadOnlyList<UiTreeItem> items)
    {
        var byId = new Dictionary<string, UiTreeItem>(StringComparer.Ordinal);
        foreach (UiTreeItem item in items)
        {
            if (!byId.TryAdd(item.Id, item))
                throw new ArgumentException($"Duplicate tree item id '{item.Id}'.", nameof(items));
        }

        // An item whose parent is missing is shown as a root rather than lost.
        var children = new Dictionary<string, List<UiTreeItem>>(StringComparer.Ordinal);
        var roots = new List<UiTreeItem>();
        foreach (UiTreeItem item in items)
        {
            if (item.Parent is { } parent && byId.ContainsKey(parent) && parent != item.Id)
            {
                if (!children.TryGetValue(parent, out List<UiTreeItem>? list))
                    children[parent] = list = new List<UiTreeItem>();
                list.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        var ordered = new List<UiBuilder.TreeRow>(items.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (UiTreeItem root in roots)
            Visit(root, 0, children, ordered, visited);

        // Items in a parent cycle are unreachable from any root; keep them as roots.
        foreach (UiTreeItem item in items)
        {
            if (!visited.Contains(item.Id))
                Visit(item, 0, children, ordered, visited);
        }
        return ordered;
    }

    private static void Visit(
        UiTreeItem item,
        int depth,
        Dictionary<string, List<UiTreeItem>> children,
        List<UiBuilder.TreeRow> ordered,
        HashSet<string> visited)
    {
        if (!visited.Add(item.Id))
            return;

        bool has = children.TryGetValue(item.Id, out List<UiTreeItem>? list);
        ordered.Add(new UiBuilder.TreeRow(item, depth, has));
        if (!has)
            return;

        foreach (UiTreeItem child in list!)
            Visit(child, depth + 1, children, ordered, visited);
    }
}
