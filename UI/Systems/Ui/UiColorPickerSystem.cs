using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

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

    private readonly List<Entity> _pickers = new();
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
        Query.ForEachEntity((ref _, ref _, entity) => _pickers.Add(entity));

        foreach (Entity entity in _pickers)
        {
            ref UiColorPicker picker = ref entity.GetComponent<UiColorPicker>();
            Vector2 local = entity.GetComponent<Position>().Value;
            Vector2 world = UiTransform.WorldPosition(entity, local);

            if (pressStarted && _dragged.IsNull)
                Grab(entity, world, input.Position);

            if (_dragged == entity)
                Drag(ref picker, world, input.Position);

            Refresh(entity, ref picker, local);
        }
    }

    private void Grab(Entity entity, Vector2 world, Vector2 point)
    {
        _region = ColorPickerLayout.Square(world).HasPoint(point) ? Region.Square
            : ColorPickerLayout.HueBar(world).HasPoint(point) ? Region.Hue
            : ColorPickerLayout.AlphaBar(world).HasPoint(point) ? Region.Alpha
            : Region.None;
        if (_region != Region.None)
            _dragged = entity;
    }

    private void Drag(ref UiColorPicker picker, Vector2 world, Vector2 point)
    {
        switch (_region)
        {
            case Region.Square:
                Rect2 square = ColorPickerLayout.Square(world);
                picker.Saturation = Fraction(point.X, square.Position.X, square.Size.X);
                picker.Value = 1 - Fraction(point.Y, square.Position.Y, square.Size.Y);
                break;
            case Region.Hue:
                Rect2 hue = ColorPickerLayout.HueBar(world);
                // The bar runs red to red, so the very bottom is the same colour as the top.
                picker.Hue = Mathf.Min(Fraction(point.Y, hue.Position.Y, hue.Size.Y), 0.9999f);
                break;
            case Region.Alpha:
                Rect2 alpha = ColorPickerLayout.AlphaBar(world);
                picker.AlphaValue = Fraction(point.X, alpha.Position.X, alpha.Size.X);
                break;
        }
    }

    private static float Fraction(float value, float start, float length) =>
        Mathf.Clamp((value - start) / Mathf.Max(1f, length), 0f, 1f);

    private static void Refresh(Entity entity, ref UiColorPicker picker, Vector2 center)
    {
        Color color = picker.Color;
        Vector2 size = ColorPickerLayout.Size;

        Place(picker.SvCursor, center - size / 2 + new Vector2(
            picker.Saturation * size.X,
            (1 - picker.Value) * size.Y));
        Place(picker.HueCursor, center + ColorPickerLayout.HueOffset + new Vector2(
            0,
            -size.Y / 2 + picker.Hue * size.Y));
        Place(picker.AlphaCursor, center + ColorPickerLayout.AlphaOffset + new Vector2(
            -size.X / 2 + picker.AlphaValue * size.X,
            0));

        if (entity.TryGetComponent(out UiMaterial material) &&
            material.Material is ShaderMaterial shader)
        {
            shader.SetShaderParameter("hue", picker.Hue);
        }

        SetFill(picker.Swatch, color);
        SetAlphaRamp(picker.Alpha, color);
        SetHex(picker.Hex, color.A < 1 ? "#" + color.ToHtml(true) : "#" + color.ToHtml(false));
    }

    private static void Place(Entity entity, Vector2 position)
    {
        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return;

        current.Value = position;
        entity.AddTag<Moved>();
    }

    private static void SetFill(Entity entity, Color color)
    {
        ref FillColor fill = ref entity.GetComponent<FillColor>();
        if (fill.Value == color)
            return;

        fill.Value = color;
        entity.AddTag<Dirty>();
    }

    private static void SetAlphaRamp(Entity entity, Color color)
    {
        ref LinearGradient gradient = ref entity.GetComponent<LinearGradient>();
        Color end = new(color, 1);
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
}
