using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;

namespace Writer.Systems.Ui;

/// <summary>
/// Drives tree controls: arrow presses expand and collapse, label presses
/// select, rows under a collapsed ancestor are hidden, and the selection is
/// highlighted.
/// </summary>
public sealed class UiTreeSystem : QuerySystem<UiTreeNode>
{
    private const string Expanded = "▾";
    private const string Collapsed = "▸";

    private readonly HashSet<int> _held = new();
    private readonly Dictionary<int, Dictionary<string, Entity>> _nodes = new();

    protected override void OnUpdate()
    {
        HandleDisclosures();
        HandleLabels();
        Present();
    }

    private void HandleDisclosures()
    {
        foreach (Entity arrow in Query.Store.Query<UiTreeDisclosure>().ToEntityList())
        {
            if (!PressedNow(arrow))
                continue;

            Entity node = arrow.GetComponent<UiTreeDisclosure>().Node;
            if (node.IsNull || !node.HasComponent<UiTreeNode>())
                continue;

            UiTreeNode data = node.GetComponent<UiTreeNode>();
            if (!data.Tree.IsNull)
            {
                UiTreeControl.SetExpanded(
                    data.Tree,
                    data.Id,
                    !UiTreeControl.IsExpanded(data.Tree, data.Id));
            }
        }
    }

    private void HandleLabels()
    {
        foreach (Entity label in Query.Store.Query<UiTreeLabel>().ToEntityList())
        {
            if (!PressedNow(label))
                continue;

            Entity node = label.GetComponent<UiTreeLabel>().Node;
            if (node.IsNull || !node.HasComponent<UiTreeNode>())
                continue;

            UiTreeNode data = node.GetComponent<UiTreeNode>();
            if (!data.Tree.IsNull)
                UiTreeControl.Select(data.Tree, data.Id);
        }
    }

    /// <summary>True on the first frame an entity is pressed.</summary>
    private bool PressedNow(Entity entity)
    {
        bool pressed = entity.Tags.Has<Pressed>();
        bool wasHeld = _held.Contains(entity.Id);
        if (pressed)
            _held.Add(entity.Id);
        else
            _held.Remove(entity.Id);
        return pressed && !wasHeld;
    }

    private void Present()
    {
        foreach (Dictionary<string, Entity> map in _nodes.Values)
            map.Clear();

        var rows = Query.ToEntityList();
        foreach (Entity row in rows)
        {
            UiTreeNode node = row.GetComponent<UiTreeNode>();
            if (node.Tree.IsNull || !node.Tree.HasComponent<UiTree>())
                continue;

            if (!_nodes.TryGetValue(node.Tree.Id, out Dictionary<string, Entity>? map))
                _nodes[node.Tree.Id] = map = new Dictionary<string, Entity>();
            map[node.Id] = row;
        }

        CommandBuffer buffer = CommandBuffer;
        foreach (Entity row in rows)
        {
            UiTreeNode node = row.GetComponent<UiTreeNode>();
            if (node.Tree.IsNull || !node.Tree.HasComponent<UiTree>())
                continue;

            UiTree tree = node.Tree.GetComponent<UiTree>();
            SetVisible(row, !IsHidden(node, tree, _nodes[node.Tree.Id]), buffer);
            if (node.HasChildren)
                SetArrow(node.Disclosure, tree.Collapsed.Contains(node.Id) ? Collapsed : Expanded, buffer);
            Highlight(
                node.Label,
                tree.Selected == node.Id || tree.MultiSelected.Contains(node.Id));
        }
    }

    private static bool IsHidden(
        UiTreeNode node,
        UiTree tree,
        Dictionary<string, Entity> byId)
    {
        string parent = node.Parent;
        for (int guard = 0; parent.Length > 0 && guard < byId.Count; guard++)
        {
            if (tree.Collapsed.Contains(parent))
                return true;
            if (!byId.TryGetValue(parent, out Entity parentRow))
                return false;
            parent = parentRow.GetComponent<UiTreeNode>().Parent;
        }
        return false;
    }

    private static void SetVisible(Entity row, bool visible, CommandBuffer buffer)
    {
        bool ignored = row.Tags.Has<LayoutIgnore>();
        if (!visible && !ignored)
            UiLayout.Hide(row, buffer);
        else if (visible && ignored)
            UiLayout.SetIgnored(row, false);
    }

    private static void SetArrow(Entity arrow, string glyph, CommandBuffer buffer)
    {
        if (arrow.IsNull || !arrow.HasComponent<Text>())
            return;

        ref Text text = ref arrow.GetComponent<Text>();
        if (text.Value == glyph)
            return;

        text.Value = glyph;
        buffer.AddTag<Dirty>(arrow.Id);
    }

    private static void Highlight(Entity label, bool selected)
    {
        if (label.IsNull || !label.HasComponent<ButtonVisual>())
            return;

        Color target = selected ? UiTheme.Selected : UiTheme.PanelAlt;
        ref ButtonVisual visual = ref label.GetComponent<ButtonVisual>();
        if (visual.Normal == target)
            return;

        visual.Normal = target;
        visual.Hover = target.Lerp(Colors.White, 0.16f);
    }
}
