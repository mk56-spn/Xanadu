using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Puffs <see cref="GameParticles.Thrust"/> out of the hovering body's bottom
/// every <see cref="PlayRules.ThrustEveryMs"/> of song time, turned with the
/// body's lean and following its bob, so it looks held up by something.
/// Nothing comes out while the body is unseen or the song holds still.
/// </summary>
public sealed class BodyThrustSystem : QuerySystem<Player, Position, PlayfieldView, CurveBody>
{
    private double _lastMs = double.NaN;
    private double _owedMs;

    protected override void OnUpdate()
    {
        Query.Each(new Thrust(this, CommandBuffer));
    }

    private readonly record struct Thrust(BodyThrustSystem System, CommandBuffer Buffer) : IEach<Player, Position, PlayfieldView, CurveBody>
    {
        public void Execute(ref Player player, ref Position position, ref PlayfieldView view, ref CurveBody body)
        {
            double step = double.IsNaN(System._lastMs) ? 0d : body.SongMs - System._lastMs;
            System._lastMs = body.SongMs;
            if (step <= 0d || step > 250d || body.Alpha <= 0f)
            {
                System._owedMs = 0d;
                return;
            }

            System._owedMs += step;
            if (System._owedMs < PlayRules.ThrustEveryMs)
                return;
            System._owedMs %= PlayRules.ThrustEveryMs;

            float tilt = PlayRules.Tilt(body.Velocity);
            Vector2 down = new(-MathF.Sin(tilt), MathF.Cos(tilt));
            Vector2 bottom = position.Value
                + new Vector2(0f, PlayRules.Bob(body.SongMs) * view.Scale)
                + down * (PlayRules.BodyHeight / 2f * view.Scale);
            Color fill = PlayfieldColors.BodyFill;
            int burst = Buffer.CreateEntity();
            Buffer.AddComponent(burst, new ParticleBurst(
                bottom,
                GameParticles.Thrust,
                new Color(fill.R, fill.G, fill.B, fill.A * body.Alpha),
                area: new Vector2(PlayRules.BodyWidth * 0.6f * view.Scale, 2f),
                angle: tilt));
        }
    }
}
