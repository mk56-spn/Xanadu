using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// The rework's player (PlayRules): a blue body, 1 wide by 2.5 tall, that hovers, airborne by
/// default. It spawns up in the air, unseen until it fades in over the second before the first note, heading right at momentum speed 1 and
/// cruises at that speed until a route takes it. Once a route note's head is hit, and while the song is on its
/// route, the route's point at the song time moves along it (turning where the player
/// turned it) and the body chases that point directly (<see cref="CurveAttractor"/>): it is never put
/// on the line, it keeps its own motion and heads straight for the point, so it rounds every corner on its own. Leaving a
/// route it blends, over the beat division nearest 0.15 s, to the route's speed
/// along its last stretch, then cruises at that fixed
/// speed until the next route. A hit dash teleports it from the dash note at the note's
/// time, keeping its velocity. The fixed floor and the field's edges stop it.
/// </summary>
public sealed class CurvePlayerSystem : QuerySystem<Player, Position, PlayfieldView, Playfield, Playhead>
{
    /// <summary>A jump back this far in the song (a restart or a seek) puts the body at its start again, ms.</summary>
    private const long RestartJumpMs = 500;

    /// <summary>How quickly the smooth song clock eases to the audio clock, per second.</summary>
    private const double ClockPull = 4d;

    /// <summary>The smooth clock is put straight on the audio clock when it is this far off, ms (a seek, a stall).</summary>
    private const double ClockSnapMs = 80d;

    private readonly List<RoutePath> _steering = new();
    private readonly List<Entity> _dashes = new();
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

            Move(ref body, clock, delta, playfield, playhead.Tempo);
            List<Vector2>? trace = entity.HasComponent<BodyTrace>() ? entity.GetComponent<BodyTrace>().Points : null;
            if (restarted)
                trace?.Clear();
            Dash(ref body, clock, playfield, trace);
            body.SongMs = clock;
            body.Alpha = PlayRules.BodyAlpha(clock, _firstNoteMs);
            if (trace is not null)
                Trace(trace, body.Position);
            entity.AddComponent(body);
            entity.GetComponent<Position>().Value = entity.GetComponent<PlayfieldView>().PlayfieldToScreen(body.Position);
        }
    }

    /// <summary>The routes of every route note whose head was hit, as the run took them, by start time (a missed head steers nothing), and the first note's time.</summary>
    private void Gather(EntityStore store)
    {
        _steering.Clear();
        _dashes.Clear();
        _firstNoteMs = null;
        foreach (var (notes, chunk) in store.Query<Note>().Chunks)
        {
            Span<Note> span = notes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (_firstNoteMs is not long first || span[i].TimeMs < first)
                    _firstNoteMs = span[i].TimeMs;
                Entity entity = chunk.EntityAt(i);
                if (span[i].IsDash && entity.TryGetComponent(out DashRun dash) && !dash.Applied)
                    _dashes.Add(entity);
                if (!span[i].IsRoute)
                    continue;

                if (entity.Tags.Has<NoteSteering>() && entity.TryGetComponent(out RouteRun run))
                    _steering.Add(RoutePath.Of(span[i], run.Start, run.Turns));
            }
        }

        _steering.Sort(static (a, b) => a.StartMs.CompareTo(b.StartMs));
    }

    /// <summary>
    /// Teleports the body for each hit dash whose time has come: from the dash
    /// note, its length the way it was hit, so where it lands never depends on
    /// the hit's timing; then on by the time since the note at the speed it
    /// keeps. A running route's point pulls it back on its own. The trace gets
    /// the takeoff and landing at the note's time, so the jump draws straight.
    /// </summary>
    private void Dash(ref CurveBody body, double now, Playfield playfield, List<Vector2>? trace)
    {
        foreach (Entity entity in _dashes)
        {
            Note note = entity.GetComponent<Note>();
            if (note.TimeMs > now)
                continue;

            ref DashRun run = ref entity.GetComponent<DashRun>();
            Vector2 landing = note.Position + RouteHeadings.Step(run.Heading) * note.DashLength;
            Vector2 since = body.Velocity * (float)((now - note.TimeMs) / 1000d);
            if (trace is not null)
            {
                trace.Add(body.Position - since);
                trace.Add(landing);
            }

            body.Position = landing + since;
            run.Applied = true;
            Contain(ref body, playfield);
        }
    }

    /// <summary>Up in the air, heading right at momentum speed 1 (<see cref="PlayRules.SpawnPosition"/>).</summary>
    private static CurveBody Start(Playfield playfield)
    {
        Vector2 at = PlayRules.SpawnPosition(playfield);
        return new CurveBody { Position = at, Velocity = PlayRules.SpawnVelocity, Target = at, Placed = true };
    }

    private void Move(ref CurveBody body, double now, float delta, Playfield playfield, TempoMap tempo)
    {
        bool wasOn = body.OnCurve;
        body.OnCurve = false;
        RoutePath? on = On(now);
        if (on is not null)
        {
            body.Velocity += CurveAttractor.Accelerate(on, now, body.Position, body.Velocity, out Vector2 point, out _) * delta;
            body.Target = point;
            body.OnCurve = true;
            body.Steered = true;
            body.Exiting = false;
        }
        else
        {
            if (wasOn && Left(now) is { } left)
                StartExit(ref body, left, tempo);
            body.Target = body.Position;
        }

        if (on is null && body.Exiting)
        {
            float t = body.ExitMs > 0d ? (float)Math.Clamp((now - body.ExitStartMs) / body.ExitMs, 0d, 1d) : 1f;
            body.Velocity = Vector2.Lerp(body.ExitFrom, body.ExitTo, t);
        }

        body.Position += body.Velocity * delta;
        Contain(ref body, playfield);
    }

    /// <summary>
    /// Starts the blend from the velocity the body left a curve with to the
    /// curve's exit speed along its end direction (the body's own heading when the path has none).
    /// </summary>
    private static void StartExit(ref CurveBody body, RoutePath left, TempoMap tempo)
    {
        Vector2 heading = left.ExitHeading;
        if (heading == Vector2.Zero)
            heading = body.Velocity.LengthSquared() > 0f ? Vector2.Normalize(body.Velocity) : Vector2.UnitX;

        body.ExitFrom = body.Velocity;
        body.ExitTo = heading * left.ExitSpeed;
        body.ExitStartMs = left.EndMs;
        body.ExitMs = PlayRules.ExitSettleMs(tempo, left.EndMs);
        body.Exiting = true;
    }

    /// <summary>Of the hit curves already over at a time, the one that ended last.</summary>
    private RoutePath? Left(double now)
    {
        RoutePath? left = null;
        foreach (RoutePath curve in _steering)
        {
            if (curve.EndMs <= now && (left is null || curve.EndMs >= left.EndMs))
                left = curve;
        }

        return left;
    }

    /// <summary>Lays the body's centre on the preview trace when it has moved far enough.</summary>
    private static void Trace(List<Vector2> points, Vector2 at)
    {
        if (points.Count == 0 || Vector2.Distance(points[^1], at) >= BodyTrace.Spacing)
            points.Add(at);
    }

    /// <summary>Of the hit curves covering a time, the one that started last.</summary>
    private RoutePath? On(double now)
    {
        RoutePath? on = null;
        foreach (RoutePath curve in _steering)
        {
            if (curve.StartMs > now)
                break;
            if (curve.Covers(now))
                on = curve;
        }

        return on;
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
