using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Godot;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>Pointer interaction and fill/label rendering for ECS sliders.</summary>
public sealed class UiSliderSystem(IPointerInputSource pointer)
    : QuerySystem<UiSlider, Rectangle, Position>
{
    // The slider under the pointer when the press began. Only it follows the
    // pointer until release, wherever the pointer then goes.
    private Entity _dragged;
    private bool _wasPressed;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        if (!input.Pressed)
            _dragged = default;

        bool pressStarted = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref slider, ref rect, ref position, entity) =>
        {
            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 bounds = new(
                world + rect.Bounds.Position,
                rect.Bounds.Size);

            if (pressStarted && _dragged.IsNull && bounds.HasPoint(input.Position))
                _dragged = entity;

            if (_dragged == entity)
            {
                float t = Mathf.Clamp(
                    (input.Position.X - bounds.Position.X) /
                    Mathf.Max(1f, bounds.Size.X),
                    0f,
                    1f);
                slider.Value = Mathf.Lerp(slider.Min, slider.Max, t);
            }

            UpdateVisuals(
                slider,
                rect,
                position,
                buffer);
        });
    }

    private static void UpdateVisuals(
        UiSlider slider,
        Rectangle track,
        Position trackPosition,
        CommandBuffer buffer)
    {
        if (slider.Fill.IsNull || !slider.Fill.HasComponent<Rectangle>())
            return;

        float t = Mathf.Clamp(
            (slider.Value - slider.Min) /
            Mathf.Max(0.001f, slider.Max - slider.Min),
            0f,
            1f);
        float width = track.Bounds.Size.X * t;
        ref Rectangle fill = ref slider.Fill.GetComponent<Rectangle>();
        fill.Bounds = new Rect2(
            -width / 2,
            -track.Bounds.Size.Y / 2,
            width,
            track.Bounds.Size.Y);

        if (slider.Fill.HasComponent<Position>())
        {
            ref Position fillPosition = ref slider.Fill.GetComponent<Position>();
            fillPosition.Value = trackPosition.Value +
                new Vector2(
                    -track.Bounds.Size.X / 2 + width / 2,
                    0);
            buffer.AddTag<Moved>(slider.Fill.Id);
        }

        if (slider.Fill.HasComponent<VisualRid>())
        {
            RenderingServer.CanvasItemSetZIndex(
                slider.Fill.GetComponent<VisualRid>().Value,
                2);
        }
        if (!slider.Fill.Tags.Has<UiSliderFill>())
            buffer.AddTag<UiSliderFill>(slider.Fill.Id);
        buffer.AddTag<Dirty>(slider.Fill.Id);

        if (!slider.Label.IsNull && slider.Label.HasComponent<Text>())
        {
            slider.Label.GetComponent<Text>().Value =
                $"{slider.Prefix}{slider.Value.ToString(slider.Format, CultureInfo.InvariantCulture)}";
            buffer.AddTag<Dirty>(slider.Label.Id);
        }
    }
}
