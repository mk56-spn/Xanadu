using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Pointer interaction and fill/label rendering for ECS sliders.</summary>
public sealed class UiSliderSystem(IPointerInputSource pointer)
    : QuerySystem<UiSlider, Rectangle, Position>
{
    // The slider under the pointer when the press began. Only it follows the
    // pointer until release, wherever the pointer then goes.
    private Entity _dragged;
    private bool _wasPressed;
    private bool _wasSecondary;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        if (!input.Pressed)
            _dragged = default;

        bool pressStarted = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        bool resetStarted = input.SecondaryPressed && !_wasSecondary;
        _wasSecondary = input.SecondaryPressed;

        CommandBuffer buffer = CommandBuffer;
        Query.EachEntity(new DragSliders(this, Query.Store, CommandBuffer, input.Position, pressStarted, resetStarted));
    }

    private static void UpdateVisuals(
        UiSlider slider,
        Rectangle track,
        Position trackPosition,
        CommandBuffer buffer)
    {
        if (slider.Fill.IsNull || !slider.Fill.HasComponent<Rectangle>())
            return;

        float t = Math.Clamp(
            (slider.Value - slider.Min) /
            Math.Max(0.001f, slider.Max - slider.Min),
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

    private readonly record struct DragSliders(UiSliderSystem System, EntityStore Store, CommandBuffer Buffer, Vector2 Pointer, bool PressStarted, bool ResetStarted) : IEachEntity<UiSlider, Rectangle, Position>
    {
        public void Execute(ref UiSlider slider, ref Rectangle rect, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 bounds = new(
                world + rect.Bounds.Position,
                rect.Bounds.Size);

            if (PressStarted && System._dragged.IsNull && bounds.HasPoint(Pointer))
                System._dragged = entity;

            if (ResetStarted && bounds.HasPoint(Pointer))
                slider.Value = slider.Default;

            if (System._dragged == entity)
            {
                float t = Math.Clamp(
                    (Pointer.X - bounds.Position.X) /
                    Math.Max(1f, bounds.Size.X),
                    0f,
                    1f);
                slider.Value = float.Lerp(slider.Min, slider.Max, t);
            }

            UpdateVisuals(
                slider,
                rect,
                position,
                Buffer);
        }
    }
}
