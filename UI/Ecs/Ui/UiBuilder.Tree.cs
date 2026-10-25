using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

public sealed partial class UiBuilder
{
    private const float DisclosureWidth = 22f;
    private const int DisclosureFontSize = 18;

    /// <summary>
    /// Creates an empty tree control in the current parent. Fill it with
    /// <see cref="SetTreeItems"/>; drive and read it with <see cref="UiTreeControl"/>.
    /// </summary>
    public Entity Tree(
        float width,
        float rowHeight = UiTheme.TreeRowHeight,
        float indent = UiTheme.TreeIndent)
    {
        Container(
            Vector2.Zero,
            new Vector2(width, 4),
            StackDirection.Vertical,
            spacing: 2,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Start,
            autoHeight: true);
        Entity tree = _current;
        End();
        tree.AddComponent(UiTree.Create(width, rowHeight, indent));
        return tree;
    }

    /// <summary>
    /// Replaces the rows of a tree. Items may come in any order; rows are laid
    /// out depth-first with siblings in the order given. Expanded/collapsed
    /// state and the selection survive for ids that are still present.
    /// </summary>
    public void SetTreeItems(Entity tree, IReadOnlyList<UiTreeItem> items)
    {
        var rows = new List<Entity>();
        foreach (Entity row in tree.ChildEntities)
            rows.Add(row);
        foreach (Entity row in rows)
            UiLayout.DeleteSubtree(row);

        List<TreeRow> ordered = UiTreeOrder.DepthFirst(items);

        // Adding rows changes the tree entity's structure and moves its
        // components, so work on a copy and write the selection back afterwards.
        UiTree state = tree.GetComponent<UiTree>();
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (TreeRow row in ordered)
            present.Add(row.Item.Id);
        state.Collapsed.RemoveWhere(id => !present.Contains(id));

        UiBuilder rowsBuilder = new UiBuilder(_entities, _visuals).Use(tree);
        foreach (TreeRow row in ordered)
            rowsBuilder.TreeRowEntity(tree, row, state);

        if (!present.Contains(state.Selected))
            tree.GetComponent<UiTree>().Selected = "";
        tree.AddTag<LayoutDirty>();
    }

    private void TreeRowEntity(Entity tree, TreeRow row, in UiTree state)
    {
        float indent = row.Depth * state.Indent;
        Container(
            Vector2.Zero,
            new Vector2(state.Width, state.RowHeight),
            StackDirection.Horizontal,
            color: new Color(0, 0, 0, 0));
        Entity container = _current;

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
        End();

        container.AddComponent(new UiTreeNode
        {
            Tree = tree,
            Id = row.Item.Id,
            Parent = row.Item.Parent ?? "",
            Depth = row.Depth,
            HasChildren = row.HasChildren,
            Disclosure = disclosure,
            Label = label,
        });
        if (row.HasChildren)
            disclosure.AddComponent(new UiTreeDisclosure(container));
        label.AddComponent(new UiTreeLabel(container));
    }

    internal readonly record struct TreeRow(UiTreeItem Item, int Depth, bool HasChildren);
}
