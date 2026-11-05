using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Drives tree controls: arrow presses expand and collapse, label presses
/// select, and arrows and the selection highlight follow the state.
/// </summary>
public sealed class UiTreeSystem : QuerySystem<UiTree>
{
    private const string Expanded = "▾";
    private const string Collapsed = "▸";

    private readonly HashSet<int> _held = new();

    protected override void OnUpdate()
    {
        HandleDisclosures();
        HandleLabels();
        Present();
    }

    private void HandleDisclosures()
    {
        foreach (Entity arrow in Parts<UiTreeDisclosure>())
        {
            if (!PressedNow(arrow))
                continue;

            Entity body = UiTreeControl.Body(UiTreeControl.NodeOfPart(arrow));
            UiLayout.SetHidden(body, !UiLayout.IsHidden(body));
        }
    }

    private void HandleLabels()
    {
        foreach (Entity label in Parts<UiTreeLabel>())
        {
            if (!PressedNow(label))
                continue;

            Entity node = UiTreeControl.NodeOfPart(label);
            UiTreeControl.Select(UiTreeControl.TreeOf(node), node.GetComponent<UiTreeNode>().Id);
        }
    }

    private EntityList Parts<T>() where T : struct, ITag =>
        Query.Store.Query().WithDisabled().AllTags(Tags.Get<T>()).ToEntityList();

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
        foreach (Entity tree in Query.ToEntityList())
        {
            Present(tree, tree.GetComponent<UiTree>());
        }
    }

    /// <summary>Walks the tree directly: this runs every frame for every row.</summary>
    private static void Present(Entity holder, in UiTree state)
    {
        foreach (Entity node in holder.ChildEntities)
        {
            if (!node.HasComponent<UiTreeNode>())
                continue;

            Entity body = UiTreeControl.Body(node);
            if (!body.IsNull)
            {
                SetArrow(UiTreeControl.Disclosure(node), UiLayout.IsHidden(body) ? Collapsed : Expanded);
                Present(body, state);
            }

            string id = node.GetComponent<UiTreeNode>().Id;
            Highlight(UiTreeControl.Label(node), state.Selected == id || state.MultiSelected.Contains(id));
        }
    }

    private static void SetArrow(Entity arrow, string glyph)
    {
        if (!arrow.HasComponent<Text>())
            return;

        ref Text text = ref arrow.GetComponent<Text>();
        if (text.Value == glyph)
            return;

        text.Value = glyph;
        arrow.AddTag<Dirty>();
    }

    private static void Highlight(Entity label, bool selected)
    {
        if (!label.HasComponent<ButtonVisual>())
            return;

        Color target = selected ? UiTheme.Selected : UiTheme.PanelAlt;
        ref ButtonVisual visual = ref label.GetComponent<ButtonVisual>();
        if (visual.Normal == target)
            return;

        visual.Normal = target;
        visual.Hover = target.Lerp(Colors.White, 0.16f);
    }
}
