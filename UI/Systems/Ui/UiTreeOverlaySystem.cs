using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// F4 panel: a scrolling tree control of the containers and buttons on screen,
/// nested by the entity tree. Entities that are neither hang off their nearest
/// listed ancestor. Rows are rebuilt only when the listing changes, so expanding
/// and collapsing keep working while it is open. The selected row's entity is
/// tagged for the bounds overlay to colour.
/// </summary>
public sealed class UiTreeOverlaySystem(
    IOverlayToggleSource toggle,
    IVisualRidFactory? visuals = null)
    : QuerySystem<Rectangle, Position>
{
    private const int LayerZ = 1002;
    private const float PanelWidth = 420;
    private const float HeaderHeight = UiMetrics.Control.Xs;
    private const float Padding = UiMetrics.Space.Sm;

    private readonly OverlayLatch _latch = new(toggle);
    private readonly HashSet<Entity> _roots = new();
    private readonly List<Entity> _ordered = new();
    private List<UiTreeItem> _items = new();
    private List<UiTreeItem> _shown = new();
    private int _buttons;
    private Entity _layer;
    private Entity _header;
    private Entity _tree;
    private Entity _selected;

    protected override void OnUpdate()
    {
        _latch.Update();
        if (!_latch.Visible)
        {
            Close();
            return;
        }

        if (_layer.IsNull)
            Open();

        Collect();
        if (!_items.SequenceEqual(_shown))
            Refresh();
        SyncSelected();
    }

    private void Open()
    {
        Vector2 stage = UiStage.Full.Size;
        float height = stage.Y - UiMetrics.Space.Lg * 2;
        float inner = PanelWidth - Padding * 2;

        UiBuilder ui = UiBuilder.Begin(Query.Store, visuals).Layer(LayerZ);
        _layer = ui.Current;
        ui.Container(
            new Vector2(stage.X - UiMetrics.Space.Lg - PanelWidth / 2, stage.Y / 2),
            new Vector2(PanelWidth, height),
            StackDirection.Vertical,
            spacing: UiMetrics.Space.Xs,
            color: UiTheme.Panel,
            takesInput: true,
            padding: Padding,
            main: StackMainAlignment.Start);
        _header = ui.Text("", new Vector2(inner, HeaderHeight), UiType.Small, Palette.Accent, TextAlign.Left);

        ui.Container(
            Vector2.Zero,
            new Vector2(inner, height - Padding * 2 - HeaderHeight - UiMetrics.Space.Xs),
            StackDirection.Vertical,
            clip: true,
            color: UiTheme.Panel);
        Entity viewport = ui.Current;
        ui.Container(
            Vector2.Zero,
            new Vector2(inner, 4),
            StackDirection.Vertical,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Start,
            autoHeight: true,
            main: StackMainAlignment.Start);
        Entity content = ui.Current;
        _tree = ui.Tree(inner);
        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollView());
        ui.End();
        _shown.Clear();
    }

    private void Close()
    {
        if (_layer.IsNull)
            return;

        Mark(default);
        UiLayout.DeleteSubtree(_layer);
        _layer = default;
        _header = default;
        _tree = default;
    }

    private void Collect()
    {
        _roots.Clear();
        _ordered.Clear();
        _items.Clear();
        _buttons = 0;

        Query.ForEachEntity((ref _, ref _, entity) =>
        {
            Entity top = entity;
            while (!top.Parent.IsNull)
                top = top.Parent;
            if (top != _layer && _roots.Add(top))
                _ordered.Add(top);
        });

        // Archetype order shifts as entities change; ids keep the listing still.
        _ordered.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        foreach (Entity root in _ordered)
            Visit(root, null);
    }

    private void Visit(Entity entity, string? parent)
    {
        if (!entity.Enabled)
            return;

        bool button = entity.HasComponent<ButtonVisual>();
        if ((button || entity.HasComponent<UiContainer>()) &&
            entity.TryGetComponent(out Rectangle rect) &&
            rect.Bounds.Size.X > 0 &&
            rect.Bounds.Size.Y > 0)
        {
            string id = entity.Id.ToString();
            _items.Add(new UiTreeItem(id, parent, Label(entity, button)));
            _buttons += button ? 1 : 0;
            parent = id;
        }

        foreach (Entity child in entity.ChildEntities)
            Visit(child, parent);
    }

    private static string Label(Entity entity, bool button)
    {
        if (entity.TryGetComponent(out Text text) && !string.IsNullOrWhiteSpace(text.Value))
            return $"{text.Value.ReplaceLineEndings(" ")}  #{entity.Id}";
        string kind = button ? "BUTTON" : ContainerKind(entity.GetComponent<UiContainer>());
        return $"{kind}  #{entity.Id}";
    }

    private static string ContainerKind(in UiContainer container)
    {
        if (container.Columns > 0)
            return $"GRID {container.Columns}";
        return container.Direction == StackDirection.Vertical ? "V-STACK" : "H-STACK";
    }

    private void SyncSelected()
    {
        if (_tree.HasComponent<UiTreeChanged>())
            _tree.RemoveComponent<UiTreeChanged>();

        Mark(int.TryParse(UiTreeControl.Selected(_tree), out int id)
            ? Query.Store.GetEntityById(id)
            : default);
    }

    private void Mark(Entity entity)
    {
        if (entity == _selected)
            return;

        if (!_selected.IsNull)
            _selected.RemoveTag<UiTreeOverlaySelected>();
        if (!entity.IsNull)
            entity.AddTag<UiTreeOverlaySelected>();
        _selected = entity;
    }

    private void Refresh()
    {
        UiBuilder.Begin(Query.Store, visuals).SetTreeItems(_tree, _items);
        _header.GetComponent<Text>().Value =
            $"F4 UI TREE   CONTAINERS {_items.Count - _buttons}   BUTTONS {_buttons}";
        _header.AddTag<Dirty>();
        (_items, _shown) = (_shown, _items);
    }
}
