using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Reads and drives a tree created by <see cref="UiBuilder.Tree"/>.</summary>
public static class UiTreeControl
{
    public static string Selected(Entity tree) =>
        tree.GetComponent<UiTree>().Selected;

    /// <summary>Tints a second set of rows, for trees whose owner selects several nodes.</summary>
    public static void SetMultiSelected(Entity tree, IEnumerable<string> ids)
    {
        HashSet<string> selected = tree.GetComponent<UiTree>().MultiSelected;
        selected.Clear();
        foreach (string id in ids)
            selected.Add(id);
    }

    public static bool IsExpanded(Entity tree, string id) =>
        !tree.GetComponent<UiTree>().Collapsed.Contains(id);

    public static void SetExpanded(Entity tree, string id, bool expanded)
    {
        SetCollapsed(tree, id, expanded);
        MarkDirty(tree);
    }

    /// <summary>The row entity of a node, or a null entity when there is none.</summary>
    public static Entity FindNode(Entity tree, string id)
    {
        foreach (Entity row in tree.ChildEntities)
        {
            if (row.HasComponent<UiTreeNode>() &&
                row.GetComponent<UiTreeNode>().Id == id)
            {
                return row;
            }
        }
        return default;
    }

    /// <summary>
    /// Selects a node. <paramref name="reveal"/> expands its ancestors so it is
    /// visible; <paramref name="raiseChanged"/> sets <see cref="UiTreeChanged"/>,
    /// which a caller reflecting its own state into the tree leaves off.
    /// Returns false when the selection was already that node.
    /// </summary>
    public static bool Select(
        Entity tree,
        string id,
        bool raiseChanged = true,
        bool reveal = true)
    {
        // Revealing tags entities, which moves components; take no reference before it.
        if (reveal && id.Length > 0)
            Reveal(tree, id);

        if (tree.GetComponent<UiTree>().Selected == id)
            return false;

        tree.GetComponent<UiTree>().Selected = id;
        if (raiseChanged)
            tree.AddComponent(new UiTreeChanged(id));
        return true;
    }

    private static void Reveal(Entity tree, string id)
    {
        Entity node = FindNode(tree, id);
        int guard = 0;
        while (!node.IsNull && guard++ < 1024)
        {
            string parent = node.GetComponent<UiTreeNode>().Parent;
            if (parent.Length == 0)
                return;

            if (!IsExpanded(tree, parent))
                SetExpanded(tree, parent, true);
            node = FindNode(tree, parent);
        }
    }

    private static void SetCollapsed(Entity tree, string id, bool expanded)
    {
        var collapsed = tree.GetComponent<UiTree>().Collapsed;
        if (expanded)
            collapsed.Remove(id);
        else
            collapsed.Add(id);
    }

    private static void MarkDirty(Entity tree) => tree.AddTag<LayoutDirty>();
}
