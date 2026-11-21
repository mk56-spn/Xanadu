using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// The rework's player (PlayRules): a blue body, 1 wide by 2.5 tall, that hovers, airborne by
/// default. It spawns up in the air, unseen until it fades in over the second before the first note,
/// and cruises right at <see cref="PlayRules.CruiseSpeed"/>; no note moves it yet (v4 is being built).
/// The fixed floor and the field's edges stop it.
/// </summary>
public sealed class CurvePlayerSystem : QuerySystem<Player, Position, PlayfieldView, Playfield, Playhead>
{
    /// <summary>A jump back this far in the song (a restart or a seek) puts the body at its start again, ms.</summary>
    private const long RestartJumpMs = 500;

    /// <summary>How quickly the smooth song clock eases to the audio clock, per second.</summary>
    private const double ClockPull = 4d;

    /// <summary>The smooth clock is put straight on the audio clock when it is this far off, ms (a seek, a stall).</summary>
    private const double ClockSnapMs = 80d;

    private long? _firstNoteMs;

    protected override void OnUpdate()
    {
        // One play entity at most, and the body is added to it on its first frame.
        foreach (Entity entity in Query.ToEntityList())
        {
            if (!entity.TryGetComponent(out PlayfieldMode mode) || mode.Kind != PlayfieldKind.Play)
                continue;

            Gather(entity.Store);
            Playfield playfield = entity.GetComponent<Playfield>();
            CurveBody body = entity.TryGetComponent(out CurveBody found) ? found : default;
            Playhead playhead = entity.GetComponent<Playhead>();
            long audioMs = playhead.TimeMs;
            bool restarted = !body.Placed || audioMs < body.SongMs - RestartJumpMs;
            if (restarted)
                body = Start(playfield) with { SongMs = audioMs };

            // The audio clock moves in uneven chunks, so the body runs on its own
            // smooth song clock: frame time while the song plays, nothing while it
            // holds, eased toward the audio clock and snapped to it on a jump.
            float delta = playhead.Playing ? Tick.deltaTime : 0f;
            double clock = body.SongMs + delta * 1000d;
            double drift = audioMs - clock;
            clock = Math.Abs(drift) > ClockSnapMs ? audioMs : clock + drift * (1d - Math.Exp(-ClockPull * delta));
            delta = (float)Math.Max(0d, clock - body.SongMs) / 1000f;

            body.Position += body.Velocity * delta;
            Contain(ref body, playfield);
            List<Vector2>? trace = entity.HasComponent<BodyTrace>() ? entity.GetComponent<BodyTrace>().Points : null;
            if (restarted)
                trace?.Clear();
            body.SongMs = clock;
            body.Alpha = PlayRules.BodyAlpha(clock, _firstNoteMs);
            if (trace is not null)
                Trace(trace, body.Position);
            entity.AddComponent(body);
            entity.GetComponent<Position>().Value = entity.GetComponent<PlayfieldView>().PlayfieldToScreen(body.Position);
        }
    }

    /// <summary>The first note's time.</summary>
    private void Gather(EntityStore store)
    {
        _firstNoteMs = null;
        foreach (var (notes, _) in store.Query<Note>().Chunks)
        {
            Span<Note> span = notes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (_firstNoteMs is not long first || span[i].TimeMs < first)
                    _firstNoteMs = span[i].TimeMs;
            }
        }
    }

    /// <summary>Up in the air, heading right at momentum speed 1 (<see cref="PlayRules.SpawnPosition"/>).</summary>
    private static CurveBody Start(Playfield playfield)
    {
        Vector2 at = PlayRules.SpawnPosition(playfield);
        return new CurveBody { Position = at, Velocity = PlayRules.SpawnVelocity, Target = at, Placed = true };
    }

    /// <summary>Lays the body's centre on the preview trace when it has moved far enough.</summary>
    private static void Trace(List<Vector2> points, Vector2 at)
    {
        if (points.Count == 0 || Vector2.Distance(points[^1], at) >= BodyTrace.Spacing)
            points.Add(at);
    }

    /// <summary>The floor and the field's edges stop the body, killing its speed into them.</summary>
    private static void Contain(ref CurveBody body, Playfield playfield)
    {
        float half = PlayRules.BodyWidth / 2;
        float halfHeight = PlayRules.BodyHeight / 2;
        float floor = PlayRules.FloorY(playfield) - halfHeight;
        if (body.Position.Y > floor)
        {
            body.Position.Y = floor;
            body.Velocity.Y = MathF.Min(body.Velocity.Y, 0f);
        }

        if (body.Position.Y < halfHeight)
        {
            body.Position.Y = halfHeight;
            body.Velocity.Y = MathF.Max(body.Velocity.Y, 0f);
        }

        if (body.Position.X < half)
        {
            body.Position.X = half;
            body.Velocity.X = MathF.Max(body.Velocity.X, 0f);
        }

        if (body.Position.X > playfield.Width - half)
        {
            body.Position.X = playfield.Width - half;
            body.Velocity.X = MathF.Min(body.Velocity.X, 0f);
        }
    }
}
