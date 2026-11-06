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

    public static bool IsExpanded(Entity tree, string id)
    {
        Entity body = Body(FindNode(tree, id));
        return body.IsNull || !UiLayout.IsHidden(body);
    }

    public static void SetExpanded(Entity tree, string id, bool expanded) =>
        UiLayout.SetHidden(Body(FindNode(tree, id)), !expanded);

    /// <summary>Every node of a tree, depth first, collapsed branches included.</summary>
    public static IEnumerable<Entity> Nodes(Entity tree)
    {
        foreach (Entity child in tree.ChildEntities)
        {
            if (!child.HasComponent<UiTreeNode>())
                continue;

            yield return child;
            Entity body = Body(child);
            if (body.IsNull)
                continue;

            foreach (Entity descendant in Nodes(body))
                yield return descendant;
        }
    }

    /// <summary>The node with this id, or a null entity when there is none.</summary>
    public static Entity FindNode(Entity tree, string id)
    {
        foreach (Entity node in Nodes(tree))
        {
            if (node.GetComponent<UiTreeNode>().Id == id)
                return node;
        }
        return default;
    }

    /// <summary>The row a node draws: its arrow slot and label.</summary>
    public static Entity Row(Entity node) =>
        !node.IsNull && node.ChildCount > 0 ? node.ChildEntities[0] : default;

    /// <summary>The stack holding a node's children, or a null entity for a leaf.</summary>
    public static Entity Body(Entity node) =>
        !node.IsNull && node.ChildCount > 1 ? node.ChildEntities[1] : default;

    /// <summary>A node's arrow button, or the empty slot that keeps a leaf's label aligned.</summary>
    public static Entity Disclosure(Entity node)
    {
        Entity row = Row(node);
        return row.ChildEntities[LabelIndex(row) - 1];
    }

    public static Entity Label(Entity node)
    {
        Entity row = Row(node);
        return row.ChildEntities[LabelIndex(row)];
    }

    /// <summary>A node's column texts, right of its label, in order.</summary>
    public static IEnumerable<Entity> Cells(Entity node)
    {
        Entity row = Row(node);
        for (int i = LabelIndex(row) + 1; i < row.ChildCount; i++)
            yield return row.ChildEntities[i];
    }

    /// <summary>The label is the last child, unless the tree has columns after it.</summary>
    private static int LabelIndex(Entity row)
    {
        for (int i = row.ChildCount - 1; i >= 0; i--)
        {
            if (row.ChildEntities[i].Tags.Has<UiTreeLabel>())
                return i;
        }

        return row.ChildCount - 1;
    }

    /// <summary>The node a row's arrow or label belongs to.</summary>
    public static Entity NodeOfPart(Entity part) => part.Parent.Parent;

    /// <summary>The node above this one, or a null entity for a root.</summary>
    public static Entity ParentNode(Entity node)
    {
        Entity above = node.Parent.Parent;
        return !above.IsNull && above.HasComponent<UiTreeNode>() ? above : default;
    }

    /// <summary>The tree a node belongs to.</summary>
    public static Entity TreeOf(Entity node)
    {
        Entity entity = node;
        while (!entity.IsNull && !entity.HasComponent<UiTree>())
            entity = entity.Parent;
        return entity;
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
            Reveal(FindNode(tree, id));

        if (tree.GetComponent<UiTree>().Selected == id)
            return false;

        tree.GetComponent<UiTree>().Selected = id;
        if (raiseChanged)
            tree.AddComponent(new UiTreeChanged(id));
        return true;
    }

    private static void Reveal(Entity node)
    {
        if (node.IsNull)
            return;

        for (Entity above = ParentNode(node); !above.IsNull; above = ParentNode(above))
            UiLayout.SetHidden(Body(above), false);
    }
}
