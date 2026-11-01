using System.Collections.Generic;
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
public sealed class UiTreeOverlaySystem : QuerySystem<Rectangle, Position>
{
    private const float PanelWidth = 420;
    private const float HeaderHeight = UiMetrics.Control.Xs;
    private const float LegendHeight = UiMetrics.Control.Xs;
    private const float Padding = UiMetrics.Space.Sm;

    private readonly IVisualRidFactory? _visuals;
    private readonly OverlayLatch _latch;
    private readonly HashSet<Entity> _roots = new();
    private readonly List<Entity> _ordered = new();
    private List<UiTreeItem> _items = new();
    private List<UiTreeItem> _shown = new();
    private readonly Dictionary<int, (string Source, string Id, string Label)> _labels = new();
    private int _buttons;
    private Entity _layer;
    private Entity _header;
    private Entity _tree;
    private Entity _selected;

    public UiTreeOverlaySystem(IOverlayToggleSource toggle, IVisualRidFactory? visuals = null)
    {
        _latch = new OverlayLatch(toggle);
        _visuals = visuals;
        Filter.WithoutAnyTags(Tags.Get<DebugUi>());
    }

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
        if (!IsShown())
            Refresh();
        SyncSelected();
    }

    private void Open()
    {
        Vector2 stage = UiStage.Full.Size;
        float height = stage.Y - UiMetrics.Space.Lg * 2;
        float inner = PanelWidth - Padding * 2;

        UiBuilder ui = UiBuilder.Begin(Query.Store, _visuals).Layer(UiLayers.DebugUiTree);
        _layer = ui.Current;
        _layer.AddTag<DebugUi>();
        ui.Container(
            new Vector2(stage.X - UiMetrics.Space.Lg - PanelWidth / 2, stage.Y / 2),
            new Vector2(PanelWidth, height),
            StackDirection.Vertical,
            spacing: UiMetrics.Space.Xs,
            color: DebugPalette.Panel,
            takesInput: true,
            padding: Padding,
            main: StackMainAlignment.Start);
        _header = ui.Text("", new Vector2(inner, HeaderHeight), UiType.Small, DebugPalette.Title, TextAlign.Left);
        ui.Container(
            Vector2.Zero,
            new Vector2(inner, LegendHeight),
            StackDirection.Horizontal,
            spacing: UiMetrics.Space.Md,
            color: new Color(0, 0, 0, 0),
            main: StackMainAlignment.Start);
        Legend(ui, "BUTTONS", DebugPalette.ButtonBorder);
        Legend(ui, "CONTAINERS", DebugPalette.ContainerBorder);
        Legend(ui, "PINS", DebugPalette.Pin);
        Legend(ui, "SELECTED", DebugPalette.SelectedBorder);
        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(inner, height - Padding * 2 - HeaderHeight - LegendHeight - UiMetrics.Space.Xs * 2),
            StackDirection.Vertical,
            clip: true,
            color: new Color(0, 0, 0, 0));
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

    /// <summary>A word in the colour of the F4 boxes it names.</summary>
    private static void Legend(UiBuilder ui, string word, Color color) =>
        ui.Text(word, UiNode.Measure(word, UiType.Caption), UiType.Caption, color, TextAlign.Left);

    private void Close()
    {
        if (_layer.IsNull)
            return;

        Mark(default);
        UiLayout.DeleteSubtree(_layer);
        _labels.Clear();
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
            if (_roots.Add(top))
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
            (string id, string label) = Row(entity, button);
            _items.Add(new UiTreeItem(id, parent, label));
            _buttons += button ? 1 : 0;
            parent = id;
        }

        foreach (Entity child in entity.ChildEntities)
            Visit(child, parent);
    }

    /// <summary>A row's id and label, built again only when what the label shows changes.</summary>
    private (string Id, string Label) Row(Entity entity, bool button)
    {
        string source = entity.TryGetComponent(out Text text) && !string.IsNullOrWhiteSpace(text.Value)
            ? text.Value
            : button ? "BUTTON" : ContainerKind(entity.GetComponent<UiContainer>());

        if (!_labels.TryGetValue(entity.Id, out (string Source, string Id, string Label) row) ||
            row.Source != source)
        {
            row = (source, entity.Id.ToString(), $"{source.ReplaceLineEndings(" ")}  #{entity.Id}");
            _labels[entity.Id] = row;
        }
        return (row.Id, row.Label);
    }

    private bool IsShown()
    {
        if (_items.Count != _shown.Count)
            return false;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != _shown[i])
                return false;
        }
        return true;
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
        UiBuilder.Begin(Query.Store, _visuals).SetTreeItems(_tree, _items);
        _header.GetComponent<Text>().Value =
            $"F4 UI TREE   CONTAINERS {_items.Count - _buttons}   BUTTONS {_buttons}";
        _header.AddTag<Dirty>();
        (_items, _shown) = (_shown, _items);
    }
}
