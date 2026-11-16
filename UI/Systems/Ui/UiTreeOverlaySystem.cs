using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>What the F4 panel's buttons ask for; row commands carry their pooled row's index.</summary>
public enum UiTreeCommand
{
    Pick,
    Up,
    All,
    Toggle,
    Inspect,

    /// <summary>The full-screen catcher in pick mode: take what is under the cursor.</summary>
    PickHere,
}

/// <summary>
/// F4: the draw visualiser, after osu!framework's. It opens picking: whatever is
/// under the cursor is outlined and named, and a click opens the tree on it. The
/// tree lists the chosen root's whole subtree on a pooled list, so its rows are a
/// fixed set refilled in place and never rebuilt under the pointer. Hovering a row
/// outlines its entity; clicking a name inspects it (every component and its
/// fields, live, below the tree), and clicking the inspected name again makes it
/// the root. UP climbs to the root's parent, ALL lists every root.
/// </summary>
/// <remarks>The query is what picking hit-tests: everything with a box.</remarks>
public sealed partial class UiTreeOverlaySystem : QuerySystem<Rectangle, Position>
{
    private readonly IVisualRidFactory? _visuals;
    private readonly IPointerInputSource? _pointer;
    private readonly OverlayLatch _latch;
    private readonly Queue<UiCommand> _commands = new();
    private System.IDisposable? _listening;

    /// <summary>The tree's root, or null for every root at once.</summary>
    private Entity _root;

    private Entity _inspected;
    private Entity _hovered;
    private bool _picking;

    public UiTreeOverlaySystem(
        IOverlayToggleSource toggle,
        IVisualRidFactory? visuals = null,
        IPointerInputSource? pointer = null)
    {
        _latch = new OverlayLatch(toggle);
        _visuals = visuals;
        _pointer = pointer;
    }

    /// <summary>Whether the panel is waiting for a click on the screen.</summary>
    public bool Picking => _picking;

    /// <summary>The entity the inspector shows, or null.</summary>
    public Entity Inspected => _inspected;

    /// <summary>The tree's root, or null when it lists every root.</summary>
    public Entity Root => _root;

    protected override void OnUpdate()
    {
        _latch.Update();
        if (!_latch.Visible)
        {
            Close();
            return;
        }

        if (_layer.IsNull)
            Open(Query.Store);

        while (_commands.TryDequeue(out UiCommand command))
            Run(command);

        Entity hover = _picking ? PickAt(Pointer()) : HoveredRow();
        Hover(hover);
        ShowPickHint(hover);
        ShowRows();
        ShowInspector();
    }

    private void Run(UiCommand command)
    {
        switch (command.As<UiTreeCommand>())
        {
            case UiTreeCommand.Pick:
                SetPicking(!_picking);
                break;
            case UiTreeCommand.PickHere:
                Entity picked = PickAt(Pointer());
                SetPicking(false);
                if (!picked.IsNull)
                    Reveal(picked);
                break;
            case UiTreeCommand.Up:
                if (!_root.IsNull)
                    Reroot(_root.Parent);
                break;
            case UiTreeCommand.All:
                Reroot(default);
                break;
            case UiTreeCommand.Toggle:
                Entity toggled = RowEntity(command.Arg);
                if (!toggled.IsNull && HasListedChild(toggled) && !_expanded.Add(toggled.Id))
                    _expanded.Remove(toggled.Id);
                break;
            case UiTreeCommand.Inspect:
                Entity row = RowEntity(command.Arg);
                if (row.IsNull)
                    break;
                if (row == _inspected && row != _root)
                    Reroot(row);
                else
                    Inspect(row);
                break;
        }
    }

    private Vector2 Pointer() => _pointer?.Read().Position ?? new Vector2(float.NaN);

    /// <summary>Opens the tree on <paramref name="entity"/>'s parent with everything under it open, and inspects it.</summary>
    public void Reveal(Entity entity)
    {
        Entity parent = entity.Parent;
        Reroot(parent.IsNull ? entity : parent);
        for (Entity up = entity.Parent; !up.IsNull; up = up.Parent)
            _expanded.Add(up.Id);
        Inspect(entity);
        _revealInspected = true;
    }

    /// <summary>A new root: its whole subtree opens, as osu's visualiser expands all on a new target.</summary>
    private void Reroot(Entity root)
    {
        _root = root;
        if (!root.IsNull)
            ExpandAll(root);
        _toTop = true;
    }

    private void ExpandAll(Entity entity)
    {
        _expanded.Add(entity.Id);
        foreach (Entity child in entity.ChildEntities)
            ExpandAll(child);
    }

    private void Inspect(Entity entity)
    {
        if (entity == _inspected)
            return;

        if (!_inspected.IsNull)
            _inspected.RemoveTag<UiTreeOverlaySelected>();
        if (!entity.IsNull)
            entity.AddTag<UiTreeOverlaySelected>();
        _inspected = entity;
    }

    private void Hover(Entity entity)
    {
        if (entity == _hovered)
            return;

        if (!_hovered.IsNull)
            _hovered.RemoveTag<UiTreeOverlayHovered>();
        if (!entity.IsNull)
            entity.AddTag<UiTreeOverlayHovered>();
        _hovered = entity;
    }

    private void Close()
    {
        if (_layer.IsNull)
            return;

        _listening?.Dispose();
        _listening = null;
        _commands.Clear();
        Hover(default);
        Inspect(default);
        UiLayout.DeleteSubtree(_layer);
        _layer = default;
        _picking = false;
        _root = default;
        _expanded.Clear();
        _listed.Clear();
        _rowEntities.Clear();
        _rowParts.Clear();
        _rowLooks.Clear();
        _everyRoot = null;
        _propRows.Clear();
        _props.Clear();
    }

    /// <summary>Whether the entity belongs to a debug view (this panel among them), which nothing here lists or picks.</summary>
    private static bool IsDebug(Entity entity)
    {
        for (Entity up = entity; !up.IsNull; up = up.Parent)
        {
            if (up.Tags.Has<DebugUi>())
                return true;
        }
        return false;
    }
}
