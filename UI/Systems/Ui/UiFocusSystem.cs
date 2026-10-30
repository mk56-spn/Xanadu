using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Keyboard focus for ECS controls: Tab/Shift+Tab to move, Enter to activate,
/// pointer clicks to focus, and a four-edge ring around the focused control.
/// Disabled and clipped-out controls are skipped. Space is deliberately left
/// alone so game shortcuts keep working while a control has focus.
/// The ring is a keyboard affordance only: a pointer gives focus without it,
/// and pressing a control that already has focus dismisses it.
/// </summary>
public sealed class UiFocusSystem : QuerySystem<Rectangle, Drawable, Position>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly IKeyboardState? _keyboard;
    private readonly Vector2 _screen;
    private readonly UiFocusRing _ring;

    private Entity _focused;
    private bool _keyboardFocus;
    private bool _wasPressed;
    private bool _tabHeld;
    private bool _activateHeld;
    private bool _pressSeen;
    private int _syntheticId;

    public UiFocusSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        IKeyboardState? keyboard,
        IVisualRidFactory visuals,
        Vector2 screen = default)
    {
        _entities = entities;
        _pointer = pointer;
        _keyboard = keyboard;
        _screen = screen;
        Filter.AllTags(Tags.Get<TakesInput>());

        _ring = new UiFocusRing(entities, visuals);
    }

    /// <summary>The control currently holding focus, or a null entity.</summary>
    public Entity FocusedEntity => _focused;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        PointerInput input = _pointer.Read();
        RemoveSynthetic(input);
        if (!_focused.IsNull)
            _focused = _entities.GetEntityById(_focused.Id);

        List<FocusTarget> focusables = Collect();
        if (!_focused.IsNull && !IsVisible(_focused))
            ClearFocus(buffer);

        HandlePointer(input, focusables, buffer);
        HandleKeys(input, focusables, buffer);
        UpdateRing(buffer);
    }

    private List<FocusTarget> Collect()
    {
        var focusables = new List<FocusTarget>();
        foreach (Entity entity in Query.ToEntityList())
        {
            if (!IsFocusable(entity) || !IsVisible(entity))
                continue;

            Rectangle rect = entity.GetComponent<Rectangle>();
            Vector2 world = UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value);
            var bounds = new Rect2(world + rect.Bounds.Position, rect.Bounds.Size);
            focusables.Add(new FocusTarget(
                entity,
                entity.GetComponent<Drawable>().ZIndex,
                bounds.GetCenter()));
        }

        focusables.Sort(static (left, right) =>
        {
            int layer = right.Layer.CompareTo(left.Layer);
            if (layer != 0)
                return layer;
            int y = left.Center.Y.CompareTo(right.Center.Y);
            if (y != 0)
                return y;
            int x = left.Center.X.CompareTo(right.Center.X);
            return x != 0 ? x : left.Entity.Id.CompareTo(right.Entity.Id);
        });
        return focusables;
    }

    private static bool IsFocusable(Entity entity)
    {
        if (entity.Tags.Has<Disabled>() || entity.Tags.Has<FocusIgnore>())
            return false;

        if (entity.Tags.Has<Focusable>())
            return true;

        return entity.HasComponent<ButtonVisual>() ||
               entity.HasComponent<TextField>() ||
               entity.HasComponent<Toggle>() ||
               entity.HasComponent<Dropdown>() ||
               entity.HasComponent<UiSlider>() ||
               entity.HasComponent<UiWheel>();
    }

    private bool IsVisible(Entity entity)
    {
        if (entity.Tags.Has<Disabled>() ||
            !entity.HasComponent<Position>() ||
            !entity.HasComponent<Rectangle>())
        {
            return false;
        }

        Rectangle rect = entity.GetComponent<Rectangle>();
        if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
            return false;

        Position position = entity.GetComponent<Position>();
        if (position.Value == UiLayout.Hidden)
            return false;

        Vector2 world = UiTransform.WorldPosition(entity, position.Value);
        Rect2 bounds = new(world + rect.Bounds.Position, rect.Bounds.Size);
        if (_screen != Vector2.Zero && !bounds.Intersects(new Rect2(Vector2.Zero, _screen)))
            return false;

        for (Entity parent = entity.Parent; !parent.IsNull; parent = parent.Parent)
        {
            if (parent.HasComponent<Position>() &&
                parent.GetComponent<Position>().Value == UiLayout.Hidden)
            {
                return false;
            }

            if (!parent.TryGetComponent(out UiContainer container) ||
                !container.Clip ||
                !parent.TryGetComponent(out Rectangle parentRect) ||
                !parent.TryGetComponent(out Position parentPosition))
            {
                continue;
            }

            Vector2 parentWorld = UiTransform.WorldPosition(
                parent,
                parentPosition.Value);
            var clip = new Rect2(
                parentWorld + parentRect.Bounds.Position,
                parentRect.Bounds.Size);
            if (!clip.Intersects(bounds))
                return false;
        }

        return true;
    }

    private void HandlePointer(
        PointerInput input,
        List<FocusTarget> focusables,
        CommandBuffer buffer)
    {
        if (!input.Pressed)
        {
            _wasPressed = false;
            _pressSeen = false;
            return;
        }

        bool firstPressFrame = !_wasPressed;
        _wasPressed = true;

        foreach (FocusTarget target in focusables)
        {
            if (!target.Entity.Tags.Has<Pressed>())
                continue;

            SetFocus(target.Entity, buffer, keyboard: false);
            _pressSeen = true;
            return;
        }

        // InputSystem writes its Pressed tag at the end of its update, so the
        // first frame of a press may not have one yet. Wait one frame before
        // deciding the press landed on empty space.
        if (firstPressFrame || _pressSeen)
            return;

        ClearFocus(buffer);
        _pressSeen = true;
    }

    private void HandleKeys(
        PointerInput input,
        List<FocusTarget> focusables,
        CommandBuffer buffer)
    {
        if (_keyboard is null)
            return;

        bool tab = _keyboard.IsPressed(Key.Tab);
        if (tab && !_tabHeld)
            MoveFocus(focusables, _keyboard.IsPressed(Key.Shift), buffer);
        _tabHeld = tab;

        bool activate = _keyboard.IsPressed(Key.Enter) ||
                        _keyboard.IsPressed(Key.KpEnter);
        if (activate && !_activateHeld && !input.Pressed &&
            !_focused.IsNull && !_focused.Tags.Has<Disabled>() &&
            Activate(_focused))
        {
            _syntheticId = _focused.Id;
            _focused = _entities.GetEntityById(_focused.Id);
        }
        _activateHeld = activate;
    }

    private void MoveFocus(
        List<FocusTarget> focusables,
        bool backwards,
        CommandBuffer buffer)
    {
        if (focusables.Count == 0)
        {
            ClearFocus(buffer);
            return;
        }

        int current = -1;
        for (int i = 0; i < focusables.Count; i++)
        {
            if (focusables[i].Entity.Id == _focused.Id)
            {
                current = i;
                break;
            }
        }

        int next = current < 0
            ? (backwards ? focusables.Count - 1 : 0)
            : (current + (backwards ? -1 : 1) + focusables.Count) % focusables.Count;
        SetFocus(focusables[next].Entity, buffer, keyboard: true);
    }

    private static bool Activate(Entity entity)
    {
        if (entity.HasComponent<TextField>() ||
            entity.Tags.Has<Pressed>())
        {
            return false;
        }

        bool activatable =
            entity.HasComponent<ButtonVisual>() ||
            entity.HasComponent<Toggle>() ||
            entity.HasComponent<Dropdown>();
        if (!activatable)
            return false;

        entity.AddTag<Pressed>();
        return true;
    }

    private void SetFocus(Entity entity, CommandBuffer buffer, bool keyboard)
    {
        if (entity.Id == _focused.Id)
        {
            _keyboardFocus = keyboard;
            return;
        }

        if (!_focused.IsNull)
        {
            buffer.RemoveTag<Focused>(_focused.Id);
            buffer.RemoveTag<TextFieldFocused>(_focused.Id);
        }

        _focused = entity;
        _keyboardFocus = keyboard;
        if (entity.IsNull)
            return;

        buffer.AddTag<Focused>(entity.Id);
        if (entity.HasComponent<TextField>())
            buffer.AddTag<TextFieldFocused>(entity.Id);
    }

    private void ClearFocus(CommandBuffer buffer)
    {
        if (_focused.IsNull)
            return;

        buffer.RemoveTag<Focused>(_focused.Id);
        buffer.RemoveTag<TextFieldFocused>(_focused.Id);
        _focused = default;
        _keyboardFocus = false;
    }

    private void RemoveSynthetic(PointerInput input)
    {
        if (_syntheticId == 0 || input.Pressed)
            return;

        Entity synthetic = _entities.GetEntityById(_syntheticId);
        if (!synthetic.IsNull && synthetic.Tags.Has<Pressed>())
            synthetic.RemoveTag<Pressed>();
        _syntheticId = 0;
    }

    private void UpdateRing(CommandBuffer buffer)
    {
        if (_focused.IsNull || !_keyboardFocus || !IsVisible(_focused))
            _ring.Hide(buffer);
        else
            _ring.Show(_focused, buffer);
    }

    private readonly record struct FocusTarget(
        Entity Entity,
        int Layer,
        Vector2 Center);
}
