using System;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender.Systems;

internal sealed class DriftSystem : QuerySystem<Placed, Drift>
{
    protected override void OnUpdate()
    {
        float time = Tick.time;
        foreach (var (placed, drifts, entities) in Query.Chunks)
        {
            Span<Placed> place = placed.Span;
            Span<Drift> drift = drifts.Span;
            for (int i = 0; i < entities.Length; i++)
            {
                ref readonly Drift d = ref drift[i];
                (float sin, float cos) = MathF.SinCos(d.Phase + time * d.Speed);
                place[i].Position = d.Home + new Vector2(cos, sin) * d.Radius;
                place[i].Turn = time * d.Speed;
            }
        }
    }
}
