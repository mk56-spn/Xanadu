using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Orderings over trees: depth-first for a flat list of items, and paint order for entities.</summary>
public static class UiTreeOrder
{
    internal static List<UiBuilder.TreeRow> DepthFirst(IReadOnlyList<UiTreeItem> items)
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

    /// <summary>
    /// True when <paramref name="entity"/> paints after <paramref name="other"/>
    /// on the same layer: a child after its ancestors, a later sibling after an
    /// earlier one and everything under it. Separate trees fall back on ids.
    /// </summary>
    public static bool IsPaintedAfter(Entity entity, Entity other)
    {
        var path = new List<Entity>();
        for (Entity at = entity; !at.IsNull; at = at.Parent)
            path.Add(at);

        var otherPath = new List<Entity>();
        for (Entity at = other; !at.IsNull; at = at.Parent)
            otherPath.Add(at);

        int i = path.Count - 1;
        int j = otherPath.Count - 1;
        if (path[i] != otherPath[j])
            return entity.Id > other.Id;

        while (i > 0 && j > 0 && path[i - 1] == otherPath[j - 1])
        {
            i--;
            j--;
        }

        if (i == 0)
            return false;
        if (j == 0)
            return true;

        Entity parent = path[i];
        return parent.GetChildIndex(path[i - 1]) > parent.GetChildIndex(otherPath[j - 1]);
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
