using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

public sealed partial class UiBuilder
{
    private const float DisclosureWidth = 22f;
    private const int DisclosureFontSize = 18;
    private const float TreeSpacing = 2f;

    /// <summary>
    /// Creates an empty tree control in the current parent. Fill it with
    /// <see cref="SetTreeItems"/>; drive and read it with <see cref="UiTreeControl"/>.
    /// </summary>
    public Entity Tree(
        float width,
        float rowHeight = UiTheme.TreeRowHeight,
        float indent = UiTheme.TreeIndent)
    {
        Entity tree = TreeStack(width);
        tree.AddComponent(UiTree.Create(width, rowHeight, indent));
        return tree;
    }

    /// <summary>
    /// Makes a tree hold exactly these items, in any order; siblings keep the order
    /// given. Nodes are matched by id, so one that stays keeps its entity, its
    /// expanded state and any interaction it holds.
    /// </summary>
    public void SetTreeItems(Entity tree, IReadOnlyList<UiTreeItem> items)
    {
        var existing = new Dictionary<string, Entity>(StringComparer.Ordinal);
        foreach (Entity node in UiTreeControl.Nodes(tree))
            existing[node.GetComponent<UiTreeNode>().Id] = node;

        UiTree state = tree.GetComponent<UiTree>();
        var path = new List<Entity>();
        var filled = new Dictionary<Entity, int>();
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (TreeRow row in UiTreeOrder.DepthFirst(items))
        {
            kept.Add(row.Item.Id);
            path.RemoveRange(row.Depth, path.Count - row.Depth);

            Entity holder = row.Depth == 0 ? tree : Body(path[^1], state);
            filled.TryGetValue(holder, out int index);
            filled[holder] = index + 1;

            existing.TryGetValue(row.Item.Id, out Entity node);
            bool moved = !node.IsNull && node.Parent != holder;
            if (node.IsNull)
                node = TreeNodeEntity(holder, row.Item.Id, state);
            UiLayout.MoveChild(holder, node, index);

            SyncRow(node, row, state, moved);
            path.Add(node);
        }

        // Survivors have moved out from under anything that is going away.
        foreach ((string id, Entity node) in existing)
        {
            if (!kept.Contains(id))
                UiLayout.DeleteSubtree(node);
        }
        foreach (Entity node in UiTreeControl.Nodes(tree))
        {
            Entity body = UiTreeControl.Body(node);
            if (!body.IsNull && body.ChildCount == 0)
                UiLayout.DeleteSubtree(body);
        }

        if (!kept.Contains(tree.GetComponent<UiTree>().Selected))
            tree.GetComponent<UiTree>().Selected = "";
    }

    private Entity TreeStack(float width)
    {
        Container(
            Vector2.Zero,
            new Vector2(width, 4),
            StackDirection.Vertical,
            spacing: TreeSpacing,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Start,
            autoHeight: true,
            main: StackMainAlignment.Start);
        Entity stack = _current;
        End();
        return stack;
    }

    private Entity TreeNodeEntity(Entity holder, string id, in UiTree state)
    {
        UiBuilder builder = new UiBuilder(_entities, _visuals).Use(holder);
        Entity node = builder.TreeStack(state.Width);
        node.AddComponent(new UiTreeNode { Id = id });
        return node;
    }

    /// <summary>The node's body, created the first time it has children to hold.</summary>
    private Entity Body(Entity node, in UiTree state)
    {
        Entity body = UiTreeControl.Body(node);
        return body.IsNull
            ? new UiBuilder(_entities, _visuals).Use(node).TreeStack(state.Width)
            : body;
    }

    /// <summary>Builds the row, or rebuilds it when its arrow or indent no longer fits.</summary>
    private void SyncRow(Entity node, in TreeRow row, in UiTree state, bool moved)
    {
        Entity current = UiTreeControl.Row(node);
        if (!current.IsNull &&
            !moved &&
            !UiTreeControl.Disclosure(node).Tags.Has<UiTreeDisclosure>() == !row.HasChildren)
        {
            UiText.Set(UiTreeControl.Label(node), row.Item.Label);
            return;
        }

        if (!current.IsNull)
            UiLayout.DeleteSubtree(current);

        UiBuilder builder = new UiBuilder(_entities, _visuals).Use(node);
        builder.TreeRowEntity(row, state);
        UiLayout.MoveChild(node, node.ChildEntities[node.ChildCount - 1], 0);
    }

    private void TreeRowEntity(in TreeRow row, in UiTree state)
    {
        float indent = row.Depth * state.Indent;
        Container(
            Vector2.Zero,
            new Vector2(state.Width, state.RowHeight),
            StackDirection.Horizontal,
            color: new Color(0, 0, 0, 0));

        if (indent > 0)
            RectEntity(Vector2.Zero, new Vector2(indent, state.RowHeight), new Color(0, 0, 0, 0));

        Entity disclosure = row.HasChildren
            ? Button("", new Vector2(DisclosureWidth, state.RowHeight), UiTheme.ButtonMuted, fontSize: DisclosureFontSize)
            : RectEntity(Vector2.Zero, new Vector2(DisclosureWidth, state.RowHeight), new Color(0, 0, 0, 0));
        Entity label = Button(
            row.Item.Label,
            new Vector2(state.Width - indent - DisclosureWidth, state.RowHeight),
            UiTheme.PanelAlt);
        label.AddComponent(new TextAlignment(TextAlign.Left));
        label.AddTag<UiTreeLabel>();
        if (row.HasChildren)
            disclosure.AddTag<UiTreeDisclosure>();
        End();
    }

    internal readonly record struct TreeRow(UiTreeItem Item, int Depth, bool HasChildren);
}
