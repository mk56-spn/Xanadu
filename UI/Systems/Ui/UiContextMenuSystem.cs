using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Right-click menus (<see cref="MenuItems"/>). A right press on a node that has a menu, or on anything under it that
/// takes input, opens one panel at the pointer on <see cref="UiLayers.ContextMenu"/>, its entries as buttons. A press
/// on an entry picks it: <see cref="MenuPicked"/> on the node, and the menu closes. An entry that confirms first turns
/// the menu into its question, CONFIRM and CANCEL. A cover behind the panel takes any press elsewhere and closes it, so
/// that press never lands on what is under it; so does Escape. There is one menu, made once; this system keeps its handles.
/// </summary>
public sealed class UiContextMenuSystem : BaseSystem
{
    private const float Width = 220;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly IKeyboardState _keyboard;
    private readonly Vector2 _screen;
    private readonly UiNode _layer;
    private readonly UiNode _cover;
    private readonly UiNode _panel;
    private readonly ButtonPress _presses = new();
    private readonly List<Entity> _buttons = [];

    /// <summary>The node whose menu is open; null when closed.</summary>
    private Entity _owner;

    /// <summary>The entry waiting to be confirmed, or -1 while the entries show.</summary>
    private int _confirming = -1;

    private bool _wasSecondary;
    private bool _wasPressed;

    public UiContextMenuSystem(EntityStore entities, IPointerInputSource pointer, IKeyboardState keyboard, IVisualItemFactory visuals, Vector2 screen)
    {
        _entities = entities;
        _pointer = pointer;
        _keyboard = keyboard;
        _screen = screen;
        _layer = UiNode.Layer(entities, default, UiLayers.ContextMenu, visuals);
        _cover = _layer.Column().Cover().Input();
        _panel = _layer.Panel().Pad(UiMetrics.Space.Xs).Gap(UiMetrics.Space.Xxs).Cross(StackCrossAlignment.Stretch).Input();
        _layer.Hidden();
    }

    /// <summary>The menu's panel, for tests: its children are the entries shown.</summary>
    public Entity Panel => _panel;

    public bool IsOpen => !_owner.IsNull;

    protected override void OnUpdateGroup()
    {
        PointerInput input = _pointer.Read();
        bool rightStarted = input.SecondaryPressed && !_wasSecondary;
        bool pressStarted = input.Pressed && !_wasPressed;
        _wasSecondary = input.SecondaryPressed;
        _wasPressed = input.Pressed;

        if (IsOpen)
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_presses.Started(_buttons[i]))
                {
                    Press(i, input.Position);
                    return;
                }

            if (pressStarted || rightStarted && Owner(TargetOfRight()).IsNull || _keyboard.IsPressed(Key.Escape))
                Close();
        }

        if (rightStarted && Owner(TargetOfRight()) is { IsNull: false } owner)
            Open(owner, input.Position);
    }

    /// <summary>What the right button went down on this frame, if it takes input.</summary>
    private Entity TargetOfRight()
    {
        foreach (Entity entity in _entities.Query().AllTags(Tags.Get<SecondaryPressed>()).Entities)
            return entity;
        return default;
    }

    /// <summary>The nearest node from <paramref name="entity"/> up that has a menu; null when none does.</summary>
    private static Entity Owner(Entity entity)
    {
        for (Entity at = entity; !at.IsNull; at = at.Parent)
            if (at.HasComponent<MenuItems>())
                return at;
        return default;
    }

    private void Open(Entity owner, Vector2 at)
    {
        _owner = owner;
        _confirming = -1;
        // The menu looks like what it opened on.
        _layer.Theme(UiNode.Wrap(owner).Roles);
        IReadOnlyList<MenuItem> items = owner.GetComponent<MenuItems>().Items;
        Fill([.. items.Select(item => item.Label)], at);
    }

    /// <summary>A press on shown entry <paramref name="i"/>: picks it, or asks first, or answers the question.</summary>
    private void Press(int i, Vector2 at)
    {
        Entity owner = _owner;
        IReadOnlyList<MenuItem> items = owner.IsNull ? [] : owner.GetComponent<MenuItems>().Items;
        if (_confirming >= 0)
        {
            // The question is a title, then CONFIRM and CANCEL.
            int picked = _confirming;
            Close();
            if (i == 1 && !owner.IsNull)
                owner.EmitSignal(new MenuPicked(picked));
            return;
        }

        if (i < items.Count && items[i].Confirm)
        {
            _confirming = i;
            Fill([items[i].Question ?? $"{items[i].Label}?", "CONFIRM", "CANCEL"], at, question: true);
            return;
        }

        Close();
        if (!owner.IsNull)
            owner.EmitSignal(new MenuPicked(i));
    }

    /// <summary>The panel holding <paramref name="labels"/> as buttons (a question's first is its title), at the pointer, inside the screen.</summary>
    private void Fill(IReadOnlyList<string> labels, Vector2 at, bool question = false)
    {
        UiLayout.DeleteChildren(_panel);
        _buttons.Clear();
        // It opens over panels of the same surface, so its edge sets it apart.
        _panel.Fill(_panel.Roles.Surface).Border(_panel.Roles.Edge, UiMetrics.Border.Thin);
        float height = UiMetrics.Space.Xs * 2;
        for (int i = 0; i < labels.Count; i++)
        {
            UiNode button = _panel.Button(labels[i], UiType.Small).Height(UiMetrics.Control.Sm);
            if (question && i == 0)
                button.Quiet().Input(false);
            else if (question && i == 1)
                button.Danger();
            else
                button.Quiet();
            _buttons.Add(button);
            height += UiMetrics.Control.Sm + (i > 0 ? UiMetrics.Space.Xxs : 0);
        }

        var size = new Vector2(Width, height);
        Vector2 corner = at;
        if (_screen != Vector2.Zero)
            corner = Vector2.Clamp(corner, Vector2.Zero, Vector2.Max(Vector2.Zero, _screen - size));
        _panel.Size(size.X, size.Y);
        UiLayout.Move(_panel, corner + size / 2);
        UiLayout.SetHidden(_layer, false);
    }

    private void Close()
    {
        _owner = default;
        _confirming = -1;
        UiLayout.SetHidden(_layer, true);
    }
}
