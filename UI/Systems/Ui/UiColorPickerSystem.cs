using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Color = Xanadu.Core.Maths.Color;
using Position = Xanadu.Core.Position;
using Rect2 = Xanadu.Core.Maths.Rect2;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Pointer interaction and display updates for ECS colour pickers.</summary>
public sealed class UiColorPickerSystem(IPointerInputSource pointer) : QuerySystem<UiColorPicker, Position>
{
    private enum Region
    {
        None,
        Square,
        Hue,
        Alpha,
    }

    private readonly List<Entity> _pickers = [];
    private Entity _dragged;
    private Region _region;
    private bool _wasPressed;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        if (!input.Pressed)
        {
            _dragged = default;
            _region = Region.None;
        }

        bool pressStarted = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        // Showing a picker tags entities, which a running query forbids.
        _pickers.Clear();
        Query.EachEntity(new CollectPickers(Query.Store, _pickers));

        foreach (Entity entity in _pickers)
        {
            ref UiColorPicker picker = ref entity.GetComponent<UiColorPicker>();

            // A picker scrolled out of its panel must not take presses meant for what is there now.
            if (pressStarted && _dragged.IsNull && !InputSystem.IsClippedOut(entity, input.Position))
                Grab(entity, picker, input.Position);

            picker.Dragging = _dragged == entity;
            if (picker.Dragging)
                Drag(ref picker, input.Position);

            Refresh(ref picker);
        }
    }

    private void Grab(Entity entity, in UiColorPicker picker, Vector2 point)
    {
        _region = Box(picker.Square).HasPoint(point) ? Region.Square
            : Box(picker.HueBar).HasPoint(point) ? Region.Hue
            : !picker.Alpha.IsNull && Box(picker.Alpha).HasPoint(point) ? Region.Alpha
            : Region.None;
        if (_region != Region.None)
            _dragged = entity;
    }

    private void Drag(ref UiColorPicker picker, Vector2 point)
    {
        switch (_region)
        {
            case Region.Square:
                Rect2 square = Box(picker.Square);
                picker.Saturation = Fraction(point.X, square.Position.X, square.Size.X);
                picker.Value = 1 - Fraction(point.Y, square.Position.Y, square.Size.Y);
                break;
            case Region.Hue:
                Rect2 hue = Box(picker.HueBar);
                // The bar runs red to red, so the very bottom is the same colour as the top.
                picker.Hue = Math.Min(Fraction(point.Y, hue.Position.Y, hue.Size.Y), 0.9999f);
                break;
            case Region.Alpha:
                Rect2 alpha = Box(picker.Alpha);
                picker.AlphaValue = Fraction(point.X, alpha.Position.X, alpha.Size.X);
                break;
        }
    }

    /// <summary>Where a part sits on screen, as laid out.</summary>
    private static Rect2 Box(Entity part)
    {
        Rect2 bounds = part.GetComponent<Rectangle>().Bounds;
        Vector2 world = UiTransform.WorldPosition(part, part.GetComponent<Position>().Value);
        return new Rect2(world + bounds.Position, bounds.Size);
    }

    private static Vector2 Extent(Entity part) => part.GetComponent<Rectangle>().Bounds.Size;

    private static float Fraction(float value, float start, float length) =>
        Math.Clamp((value - start) / Math.Max(1f, length), 0f, 1f);

    /// <summary>Puts each cursor where the colour is on its bar; a bar's space is centred on it.</summary>
    private static void Refresh(ref UiColorPicker picker)
    {
        Color color = picker.Color;

        Vector2 square = Extent(picker.Square);
        Place(picker.SvCursor, new Vector2(
            (picker.Saturation - 0.5f) * square.X,
            (0.5f - picker.Value) * square.Y));
        Place(picker.HueCursor, new Vector2(0, (picker.Hue - 0.5f) * Extent(picker.HueBar).Y));

        if (picker.Square.TryGetComponent(out UiMaterial material))
            material.Material.Set("hue", picker.Hue);

        if (!picker.Alpha.IsNull)
        {
            Place(picker.AlphaCursor, new Vector2((picker.AlphaValue - 0.5f) * Extent(picker.Alpha).X, 0));
            SetAlphaRamp(picker.Alpha, color);
        }

        if (!picker.Swatch.IsNull)
            UiText.SetFill(picker.Swatch, color);
        if (!picker.Hex.IsNull)
            SetHex(picker.Hex, color.A < 1 ? "#" + color.ToHtml() : "#" + color.ToHtml(false));
    }

    private static void Place(Entity entity, Vector2 position) => UiLayout.Move(entity, position);

    private static void SetAlphaRamp(Entity entity, Color color)
    {
        ref LinearGradient gradient = ref entity.GetComponent<LinearGradient>();
        Color end = new(color);
        if (gradient.End == end)
            return;

        gradient.Start = new Color(color, 0);
        gradient.End = end;
        entity.AddTag<Dirty>();
    }

    private static void SetHex(Entity entity, string text)
    {
        ref Text current = ref entity.GetComponent<Text>();
        if (current.Value == text)
            return;

        current.Value = text;
        entity.AddTag<Dirty>();
    }

    private readonly record struct CollectPickers(EntityStore Store, List<Entity> Pickers) : IEachEntity<UiColorPicker, Position>
    {
        public void Execute(ref UiColorPicker _, ref Position __, int id) => Pickers.Add(Store.GetEntityById(id));
    }
}
