using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Debug;

/// <summary>F6 ECS gravity slider panel.</summary>
public sealed class GravityDebugSystem(
    EntityStore entities,
    Box2DPhysicsWorld physics,
    IOverlayToggleSource toggle) : QuerySystem<UiSlider>
{
    private bool _built;

    protected override void OnUpdate()
    {
        if (!_built && toggle.IsTogglePressed())
            Build();
        if (!_built)
            return;

        Query.ForEachEntity((ref slider, entity) =>
        {
            // Other panels, such as the shader tuner, own sliders too.
            if (!entity.HasComponent<ShaderParam>())
                physics.SetGravity(slider.Value);
        });
    }

    private void Build()
    {
        _built = true;
        UiBuilder ui = UiBuilder.Begin(entities).Layer(1002);
        ui.RectEntity(new Vector2(330, 170), new Vector2(420, 150), new Color("#0b111b"));
        Entity label = ui.RectEntity(
            new Vector2(330, 120),
            new Vector2(380, 28),
            new Color(0, 0, 0, 0),
            $"GRAVITY {physics.Gravity:0} m/s²");
        label.AddComponent(new TextStyle(18, new Color("#facc15")));

        Entity track = ui.RectEntity(
            new Vector2(330, 190),
            new Vector2(340, 16),
            new Color("#334155"),
            takesInput: true);
        Entity fill = ui.RectEntity(
            new Vector2(160, 190),
            new Vector2(340, 16),
            new Color("#38bdf8"), "");
        Entity value = ui.RectEntity(
            new Vector2(330, 230),
            new Vector2(300, 24),
            new Color(0, 0, 0, 0),
            $"GRAVITY {physics.Gravity:0}");
        value.AddComponent(new TextStyle(16, new Color("#e2e8f0")));

        track.AddComponent(new UiSlider(
            fill, value, 0f, 80f, physics.Gravity, "GRAVITY "));
    }
}
