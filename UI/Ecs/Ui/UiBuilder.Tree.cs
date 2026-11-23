using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;

namespace Xanadu.Ui.Ecs.Ui;

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
        float indent = UiTheme.TreeIndent,
        float[]? cellWidths = null)
    {
        Entity tree = TreeStack(width);
        tree.AddComponent(UiTree.Create(width, rowHeight, indent, cellWidths));
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
            !UiTreeControl.Disclosure(node).Tags.Has<UiTreeDisclosure>() == !row.HasChildren &&
            SameBadges(node.GetComponent<UiTreeNode>().Badges, row.Item.Badges))
        {
            UiText.Set(UiTreeControl.Label(node), row.Item.Label);
            TintLabel(UiTreeControl.Label(node), row.Item.Tint);
            int column = 0;
            foreach (Entity cell in UiTreeControl.Cells(node))
                UiText.Set(cell, CellText(row.Item, column++));
            return;
        }

        if (!current.IsNull)
            UiLayout.DeleteSubtree(current);

        UiBuilder builder = new UiBuilder(_entities, _visuals).Use(node);
        builder.TreeRowEntity(row, state);
        node.GetComponent<UiTreeNode>().Badges = row.Item.Badges;
        UiLayout.MoveChild(node, node.ChildEntities[node.ChildCount - 1], 0);
    }

    private void TreeRowEntity(in TreeRow row, in UiTree state)
    {
        float indent = row.Depth * state.Indent;
        float cells = 0;
        if (state.CellWidths is { } widths)
        {
            foreach (float width in widths)
                cells += width;
        }

        Container(
            Vector2.Zero,
            new Vector2(state.Width, state.RowHeight),
            StackDirection.Horizontal,
            color: new Color(0, 0, 0, 0));

        if (row.Depth > 0)
            IndentGuides(row.Depth, state);

        Entity disclosure = row.HasChildren
            ? Button("", new Vector2(DisclosureWidth, state.RowHeight), UiTheme.ButtonMuted, fontSize: DisclosureFontSize)
            : RectEntity(Vector2.Zero, new Vector2(DisclosureWidth, state.RowHeight), new Color(0, 0, 0, 0));
        UiTreeBadge[] badges = row.Item.Badges ?? [];
        Entity label = Button(
            row.Item.Label,
            new Vector2(state.Width - indent - DisclosureWidth - cells - badges.Length * state.RowHeight, state.RowHeight),
            UiTheme.PanelAlt);
        label.AddComponent(new TextAlignment(TextAlign.Left));
        label.AddTag<UiTreeLabel>();
        TintLabel(label, row.Item.Tint);
        string tips = BadgeTips(badges);
        if (tips.Length > 0)
            label.AddComponent(new UiTooltip(tips));
        foreach (UiTreeBadge badge in badges)
        {
            float side = state.RowHeight * BadgeScale;
            Entity mark = RectEntity(Vector2.Zero, new Vector2(state.RowHeight, state.RowHeight), Colors.White);
            mark.AddComponent(new UiMaterial(UiShapes.Material(
                badge.Shape,
                badge.Shape == UiSdfShape.Ellipse ? new Vector2(side, side * 0.65f) : new Vector2(side * 0.75f, side * 0.75f),
                badge.Color,
                badge.Filled)));
        }
        for (int column = 0; column < (state.CellWidths?.Length ?? 0); column++)
        {
            Text(CellText(row.Item, column), new Vector2(state.CellWidths![column], state.RowHeight), align: TextAlign.Right);
        }
        if (row.HasChildren)
            disclosure.AddTag<UiTreeDisclosure>();
        End();
    }

    private static void TintLabel(Entity label, Color? tint)
    {
        if (label.IsNull || !label.HasComponent<TextStyle>())
            return;
        Color colour = tint ?? UiTheme.Text;
        ref TextStyle style = ref label.GetComponent<TextStyle>();
        if (style.Color == colour)
            return;
        style.Color = colour;
        label.AddTag<Dirty>();
    }

    /// <summary>
    /// The row's indent with a line down from each level's arrow, so a branch can
    /// be followed by eye. Each line runs on through the gap below the row, so the
    /// lines of rows one under another join up.
    /// </summary>
    private void IndentGuides(int depth, in UiTree state)
    {
        var clear = new Color(0, 0, 0, 0);
        float offset = Math.Min(MathF.Floor(DisclosureWidth / 2f), state.Indent - GuideWidth);
        var line = new Vector2(GuideWidth, state.RowHeight + TreeSpacing);
        RectEntity(Vector2.Zero, new Vector2(offset, state.RowHeight), clear);
        for (int level = 0; level < depth; level++)
        {
            RectEntity(Vector2.Zero, line, UiTheme.TreeGuide);
            float after = level < depth - 1 ? state.Indent - GuideWidth : state.Indent - offset - GuideWidth;
            if (after > 0)
                RectEntity(Vector2.Zero, new Vector2(after, state.RowHeight), clear);
        }
    }

    private const float GuideWidth = 1f;

    /// <summary>A badge's shape as a share of the row's height.</summary>
    private const float BadgeScale = 0.7f;

    private static bool SameBadges(UiTreeBadge[]? a, UiTreeBadge[]? b) =>
        (a ?? []).AsSpan().SequenceEqual(b ?? []);

    private static string BadgeTips(UiTreeBadge[] badges)
    {
        var tips = new List<string>();
        foreach (UiTreeBadge badge in badges)
        {
            if (!string.IsNullOrEmpty(badge.Tip))
                tips.Add(badge.Tip);
        }

        return string.Join(" · ", tips);
    }

    private static string CellText(in UiTreeItem item, int column) =>
        item.Cells is { } cells && column < cells.Length ? cells[column] : "";

    internal readonly record struct TreeRow(UiTreeItem Item, int Depth, bool HasChildren);
}
