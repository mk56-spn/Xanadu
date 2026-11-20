using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// F6 turns the speed graph on or off; while it is on, the play body's velocity
/// is written into <see cref="SpeedGraph"/> every frame for the graph drawer.
/// Stays on across previews and restarts until F6 again.
/// </summary>
public sealed class SpeedGraphSystem(IKeyboardState keyboard) : QuerySystem<CurveBody, PlayfieldMode>
{
    private bool _on;
    private bool _wasPressed;

    protected override void OnUpdate()
    {
        bool pressed = keyboard.IsPressed(DebugKeys.SpeedGraph);
        if (pressed && !_wasPressed)
            _on = !_on;
        _wasPressed = pressed;

        // One play entity at most.
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            if (!_on)
            {
                if (entity.HasComponent<SpeedGraph>())
                    entity.RemoveComponent<SpeedGraph>();
                continue;
            }

            if (!entity.HasComponent<SpeedGraph>())
                entity.AddComponent(new SpeedGraph());

            CurveBody body = entity.GetComponent<CurveBody>();
            var samples = entity.GetComponent<SpeedGraph>().Samples;
            if (samples.Count > 0 && body.SongMs < samples[^1].TimeMs)
                samples.Clear();
            if (samples.Count > 0 && body.SongMs == samples[^1].TimeMs)
                continue;

            samples.Add(new SpeedSample(body.SongMs, body.Velocity));
            int old = 0;
            while (old < samples.Count && samples[old].TimeMs < body.SongMs - SpeedGraph.WindowMs)
                old++;
            if (old > 0)
                samples.RemoveRange(0, old);
        }
    }
}
