using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Physics;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Debug;

/// <summary>F6 ECS gravity slider for live character tuning.</summary>
public sealed class GravityDebugSystem : QuerySystem<UiSlider, Rectangle, Position>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly Box2DPhysicsWorld _physics;
    private readonly IOverlayToggleSource _toggle;
    private bool _built;
    private bool _dragging;
    private bool _wasPressed;

    public GravityDebugSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        Box2DPhysicsWorld physics,
        IOverlayToggleSource toggle)
    {
        _entities = entities;
        _pointer = pointer;
        _physics = physics;
        _toggle = toggle;
    }

    protected override void OnUpdate()
    {
        if (!_built && _toggle.IsTogglePressed())
            Build();
        if (!_built)
            return;

        PointerInput input = _pointer.Read();
        if (!input.Pressed)
            _dragging = false;

        Query.ForEachEntity((ref slider, ref rect, ref position, entity) =>
        {
            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 bounds = new(
                world + rect.Bounds.Position,
                rect.Bounds.Size);

            if (input.Pressed && bounds.HasPoint(input.Position))
                _dragging = true;

            if (!_dragging)
                return;

            float t = Mathf.Clamp(
                (input.Position.X - bounds.Position.X) / Mathf.Max(1f, bounds.Size.X),
                0f,
                1f);
            slider.Value = Mathf.Lerp(slider.Min, slider.Max, t);
            _physics.SetGravity(slider.Value);
            UpdateVisuals(slider, rect, position);
        });
    }

    private void Build()
    {
        _built = true;
        UiBuilder ui = UiBuilder.Begin(_entities).Layer(1002);
        ui.RectEntity(new Vector2(330, 170), new Vector2(420, 150), new Color("#0b111b"));
        Entity label = ui.RectEntity(
            new Vector2(330, 120),
            new Vector2(380, 28),
            new Color(0, 0, 0, 0),
            $"GRAVITY {_physics.Gravity:0} m/s²");
        label.AddComponent(new TextStyle(18, new Color("#facc15")));

        Entity track = ui.RectEntity(
            new Vector2(330, 190),
            new Vector2(340, 16),
            new Color("#334155"),
            takesInput: true);
        Entity fill = ui.RectEntity(
            new Vector2(200, 190),
            new Vector2(170, 16),
            new Color("#38bdf8"));
        Entity value = ui.RectEntity(
            new Vector2(330, 230),
            new Vector2(300, 24),
            new Color(0, 0, 0, 0),
            $"GRAVITY {_physics.Gravity:0}");
        value.AddComponent(new TextStyle(16, new Color("#e2e8f0")));

        track.AddComponent(new UiSlider(fill, value, 0f, 80f, _physics.Gravity));
    }

    private void UpdateVisuals(UiSlider slider, Rectangle rect, Position position)
    {
        if (slider.Fill.IsNull || !slider.Fill.HasComponent<Rectangle>())
            return;

        float t = (slider.Value - slider.Min) / Mathf.Max(0.001f, slider.Max - slider.Min);
        float width = rect.Bounds.Size.X * t;
        ref Rectangle fill = ref slider.Fill.GetComponent<Rectangle>();
        fill.Bounds = new Rect2(-width / 2, -rect.Bounds.Size.Y / 2, width, rect.Bounds.Size.Y);
        if (slider.Fill.HasComponent<Position>())
        {
            ref Position fillPosition = ref slider.Fill.GetComponent<Position>();
            fillPosition.Value = position.Value + new Vector2(
                -rect.Bounds.Size.X / 2 + width / 2,
                0);
            slider.Fill.AddTag<Moved>();
        }
        slider.Fill.AddTag<Dirty>();

        if (!slider.Label.IsNull && slider.Label.HasComponent<Text>())
        {
            slider.Label.GetComponent<Text>().Value = $"GRAVITY {slider.Value:0}";
            slider.Label.AddTag<Dirty>();
        }
    }
}
