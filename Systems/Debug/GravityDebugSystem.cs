using System;
using System.Collections.Generic;
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
    IOverlayToggleSource toggle,
    Func<bool> inGameplay) : QuerySystem<UiSlider>
{
    private readonly List<Entity> _panel = [];
    private readonly float _defaultGravity = physics.Gravity;
    private readonly OverlayLatch _latch = new(toggle);

    protected override void OnUpdate()
    {
        // The panel belongs to a gameplay instance: leaving one closes it.
        if (_latch.Update(inGameplay()))
        {
            if (_latch.Visible)
                Build();
            else
                Teardown();
        }

        if (!_latch.Visible)
            return;

        Query.ForEachEntity((ref slider, entity) =>
        {
            // Other panels, such as the shader tuner, own sliders too.
            if (!entity.HasComponent<ShaderParam>())
                physics.SetGravity(slider.Value);
        });
    }

    private void Teardown()
    {
        foreach (Entity part in _panel)
            part.DeleteEntity();
        _panel.Clear();
    }

    private void Build()
    {
        UiBuilder ui = UiBuilder.Begin(entities).Layer(1002);
        Entity background = ui.RectEntity(
            new Vector2(330, 170), new Vector2(420, 150), DebugPanelStyle.Background);
        Entity label = ui.RectEntity(
            new Vector2(330, 120),
            new Vector2(380, 28),
            new Color(0, 0, 0, 0),
            $"GRAVITY {physics.Gravity:0} m/s²");
        label.AddComponent(new TextStyle(DebugPanelStyle.TitleSize, DebugPanelStyle.Accent));

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
        value.AddComponent(new TextStyle(DebugPanelStyle.RowSize, DebugPanelStyle.Text));

        track.AddComponent(new UiSlider(
            fill, value, 0f, 80f, physics.Gravity, "GRAVITY ", defaultValue: _defaultGravity));
    }
}
