using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// The rework's player (PlayRules): a blue body, 1 wide by 2.5 tall. It doesn't exist until the
/// level's entrance note: until then it waits, unseen, in the entrance where that note was placed, and on its
/// time it pops out and runs right at <see cref="PlayRules.CruiseSpeed"/> under <see cref="PlayRules.Gravity"/>.
/// The fixed floor holds it up, and the top and both sides are walls.
/// </summary>
public sealed class CurvePlayerSystem : QuerySystem<Player, Position, PlayfieldView, Playfield, Playhead>
{
    /// <summary>A jump back this far in the song (a restart or a seek) puts the body at its start again, ms.</summary>
    private const long RestartJumpMs = 500;

    protected override void OnUpdate()
    {
        // One play entity at most, and the body is added to it on its first frame.
        foreach (Entity entity in Query.ToEntityList())
        {
            if (!entity.TryGetComponent(out PlayfieldMode mode) || mode.Kind != PlayfieldKind.Play)
                continue;

            Playfield playfield = entity.GetComponent<Playfield>();
            CurveBody body = entity.TryGetComponent(out CurveBody found) ? found : default;
            Playhead playhead = entity.GetComponent<Playhead>();
            long audioMs = playhead.TimeMs;
            bool restarted = !body.Placed || audioMs < body.SongMs - RestartJumpMs;
            if (restarted)
            {
                // Hit notes are taken away, so the entrance is read as the run starts, not every frame.
                body = Start(entity.Store, playfield) with { SongMs = audioMs };
            }

            // The body runs on the playhead's smooth clock, as the notes are drawn.
            double clock = playhead.ShownMs;
            double was = body.SongMs;
            float delta = (float)Math.Max(0d, clock - body.SongMs) / 1000f;

            if (body.ReleaseMs is var release && !double.IsNaN(release) && body.SongMs < release && clock >= release)
            {
                // Launched on the release itself, not on this frame's start.
                Release(entity, body);
                Launch(ref body, release);
                delta = (float)(clock - release) / 1000f;
            }

            if (!double.IsNaN(body.ReleaseMs) && clock < body.ReleaseMs)
                body = Waiting(body);
            else if (body.OnOrb)
                Ride(ref body, playfield, clock);
            else if (body.Dashing)
            {
                // Instant: straight to where it ends. A pad it reaches ends it below, in TouchPads.
                if (Sweep(ref body, entity.Store, playfield))
                    EndDash(ref body, entity, clock);
            }
            else
            {
                // In an updraft's column the draft holds the body up; elsewhere gravity has it.
                if (DraftHolding(entity.Store, body.Position) is { } draft)
                    body.Velocity.Y = Drafts.Lift(draft, body.Position.Y, body.Velocity.Y, playhead.Tempo.BeatAt(clock), delta);
                else
                    body.Velocity.Y += PlayRules.Gravity * delta;
                body.Position += body.Velocity * delta;
                Contain(ref body, playfield);
            }
            if (restarted)
                _touching.Clear();
            bool released = double.IsNaN(body.ReleaseMs) || clock >= body.ReleaseMs;
            if (released && !body.OnOrb)
            {
                FireTimedPads(entity, ref body, playfield, was, clock);
                TouchPads(entity, ref body, clock);
            }

            List<Vector2>? trace = entity.HasComponent<BodyTrace>() ? entity.GetComponent<BodyTrace>().Points : null;
            if (restarted)
                trace?.Clear();
            body.SongMs = clock;
            body.Alpha = PlayRules.BodyAlpha(clock, body.ReleaseMs);
            if (trace is not null)
                Trace(trace, body.Position);
            entity.AddComponent(body);
            entity.GetComponent<Position>().Value = entity.GetComponent<PlayfieldView>().PlayfieldToScreen(body.Position);
        }
    }

    /// <summary>
    /// Hanging from an orb: placed on it by song time. At its tail the body
    /// leaves with the orb's motion there and flies on for the rest of the frame.
    /// </summary>
    private static void Ride(ref CurveBody body, Playfield playfield, double clock)
    {
        Note orb = body.Ride;
        if (clock < orb.EndMs)
        {
            body.Position = Orbs.At(orb, clock) + Orbs.Hang;
            body.Velocity = Orbs.Velocity(orb, clock);
            return;
        }

        body.OnOrb = false;
        body.Position = Orbs.At(orb, orb.EndMs) + Orbs.Hang;
        body.Velocity = Orbs.Velocity(orb, orb.EndMs);
        float after = (float)(clock - orb.EndMs) / 1000f;
        body.Velocity.Y += PlayRules.Gravity * after;
        body.Position += body.Velocity * after;
        Contain(ref body, playfield);
    }

    /// <summary>
    /// The entrance's onset, at its release: a fall runs right under gravity; a
    /// dash goes its way until a wall; an orb exit puts the body on the orb
    /// starting at the rift, ridden to its tail with nothing left to judge.
    /// </summary>
    private static void Launch(ref CurveBody body, double release)
    {
        Note entrance = body.Entrance;
        body.Position = body.EntranceAt;
        body.SongMs = release;
        switch (entrance.Exit)
        {
            case EntranceExit.Dash when PlayRules.DashDirection(entrance.Target) is var way && way != Vector2.Zero:
                body.Dash = way;
                body.DashFrom = body.Position;
                body.DashStartMs = release;
                body.Velocity = way;
                break;
            case EntranceExit.Orb when entrance.EndMs > entrance.TimeMs:
                body.OnOrb = true;
                body.HoldOpen = false;
                body.Ride = Orbs.OfEntrance(entrance);
                body.Velocity = Orbs.Velocity(body.Ride, release);
                break;
            default:
                body.Velocity = PlayRules.SpawnVelocity;
                break;
        }
    }

    /// <summary>How far a dash's sweep steps at a time, playfield units: finer than any item.</summary>
    private const float SweepStep = 4f;

    /// <summary>
    /// Runs a dash out at once: the body goes its way until a wall stops it, it
    /// enters a dash ender (stopped on the ender's centre) or it comes onto a pad
    /// (left dashing, for the pad to end). True when the dash is over here.
    /// Enders the body starts in are passed out of, not stopped in.
    /// </summary>
    private bool Sweep(ref CurveBody body, EntityStore store, Playfield playfield)
    {
        Vector2 way = body.Dash;
        Vector2 from = body.Position;
        body.Velocity = way;
        int steps = (int)((playfield.Width + playfield.Height) / SweepStep) + 2;
        bool over = false;
        for (int i = 0; i < steps && !over; i++)
        {
            body.Position += way * SweepStep;
            foreach (var (stops, _) in store.Query<DashStop>().Chunks)
            {
                foreach (DashStop stop in stops.Span)
                {
                    if (!over && DashStops.Holds(stop, body.Position) && !DashStops.Holds(stop, from))
                    {
                        body.Position = DashStops.StopAt(stop, body.Position, way);
                        over = true;
                    }
                }
            }

            if (over || Contain(ref body, playfield))
            {
                over = true;
                break;
            }

            if (ReachesPad(store, body.Position))
                break;
        }

        body.DashTo = body.Position;
        return over;
    }

    /// <summary>Whether the body has just come onto an untimed pad it wasn't on, which then ends the dash.</summary>
    private bool ReachesPad(EntityStore store, Vector2 at)
    {
        foreach (Entity padEntity in store.Query<Pad>().Entities)
        {
            Pad pad = padEntity.GetComponent<Pad>();
            if (!pad.IsTimed && !_touching.Contains(padEntity.Id) && Pads.Touches(pad, at))
                return true;
        }

        return false;
    }

    /// <summary>The updraft whose column the body is in, the nearest emitter's when columns overlap; null in none.</summary>
    private static Draft? DraftHolding(EntityStore store, Vector2 body)
    {
        Draft? holding = null;
        float best = float.MaxValue;
        foreach (var (drafts, _) in store.Query<Draft>().Chunks)
        {
            foreach (Draft draft in drafts.Span)
            {
                float gap = MathF.Abs(body.X - draft.Position.X);
                if (Drafts.Holds(draft, body) && gap < best)
                {
                    best = gap;
                    holding = draft;
                }
            }
        }

        return holding;
    }

    /// <summary>The pads the body touched last frame: a pad fires as the body comes onto it, not again until it has left.</summary>
    private readonly HashSet<int> _touching = [];
    private readonly HashSet<int> _touchingNow = [];

    /// <summary>
    /// Fires every pad the body has just come onto: its upward speed is set
    /// (never added), and a dash that reached it ends there, carrying on
    /// sideways at cruise speed the way it went. A ride on an orb passes pads by.
    /// </summary>
    private void TouchPads(Entity entity, ref CurveBody body, double clock)
    {
        _touchingNow.Clear();
        foreach (Entity padEntity in entity.Store.Query<Pad>().Entities)
        {
            ref Pad pad = ref padEntity.GetComponent<Pad>();
            if (pad.IsTimed || !Pads.Touches(pad, body.Position))
                continue;

            _touchingNow.Add(padEntity.Id);
            if (_touching.Contains(padEntity.Id))
                continue;

            if (body.Dashing)
            {
                float side = MathF.Abs(body.Dash.X) > 0.5f ? MathF.Sign(body.Dash.X) : MathF.Sign(body.Velocity.X);
                body.Dash = Vector2.Zero;
                body.Velocity.X = side * PlayRules.CruiseSpeed;
            }

            body.Velocity.Y = -pad.Speed;
            pad.FiredMs = clock;
            Burst(entity, pad);
        }

        _touching.Clear();
        _touching.UnionWith(_touchingNow);
    }

    /// <summary>
    /// Fires every timed pad whose time came this frame and that reaches the
    /// body: the body stands on it at that moment exactly, launches at its
    /// speed (a dash ends there), and flies on for the rest of the frame.
    /// </summary>
    private void FireTimedPads(Entity entity, ref CurveBody body, Playfield playfield, double was, double clock)
    {
        foreach (Entity padEntity in entity.Store.Query<Pad>().Entities)
        {
            ref Pad pad = ref padEntity.GetComponent<Pad>();
            if (!pad.IsTimed || pad.TimeMs <= was || pad.TimeMs > clock || !Pads.Reaches(pad, body.Position))
                continue;

            if (body.Dashing)
            {
                float side = MathF.Abs(body.Dash.X) > 0.5f ? MathF.Sign(body.Dash.X) : MathF.Sign(body.Velocity.X);
                body.Dash = Vector2.Zero;
                body.Velocity.X = side * PlayRules.CruiseSpeed;
            }

            body.Position = Pads.StandingOn(pad);
            body.Velocity.Y = -pad.Speed;
            float after = (float)(clock - pad.TimeMs) / 1000f;
            body.Velocity.Y += PlayRules.Gravity * after;
            body.Position += body.Velocity * after;
            Contain(ref body, playfield);
            pad.FiredMs = pad.TimeMs;
            Burst(entity, pad);
        }
    }

    /// <summary>A pad's sparks thrown up off it, and a kick.</summary>
    private void Burst(Entity entity, in Pad pad)
    {
        PlayfieldView view = entity.GetComponent<PlayfieldView>();
        int burst = CommandBuffer.CreateEntity();
        CommandBuffer.AddComponent(burst, new ParticleBurst(
            view.PlayfieldToScreen(pad.Position),
            GameParticles.Hit,
            PlayfieldColors.PadColor,
            Pads.Size * view.Scale * new Vector2(0.8f, 0.2f),
            heading: new Vector2(0f, -1f)));
        PlayCameraSystem.Kick(entity, 1.2f);
    }

    /// <summary>The body bursts out of the entrance: a hard kick (the flare and sparks are drawn by the entrance).</summary>
    private static void Release(Entity entity, in CurveBody body) => PlayCameraSystem.Kick(entity, 2f);

    /// <summary>
    /// The body at the start of a run: in the level's entrance (its earliest
    /// entrance note), waiting to be released on its time; without one, out
    /// from the start in the middle of the field. Set to head right either way.
    /// </summary>
    private static CurveBody Start(EntityStore store, Playfield playfield)
    {
        Note? entrance = null;
        foreach (var (notes, _) in store.Query<Note>().Chunks)
        {
            foreach (Note note in notes.Span)
            {
                if (note.IsEntrance && (entrance is not { } earliest || note.TimeMs < earliest.TimeMs))
                    entrance = note;
            }
        }

        Vector2 at = entrance?.Position ?? PlayRules.EntrancePosition(playfield);
        return new CurveBody
        {
            Position = at,
            Velocity = PlayRules.SpawnVelocity,
            Target = at,
            Placed = true,
            EntranceAt = at,
            Entrance = entrance ?? default,
            ReleaseMs = entrance?.TimeMs ?? double.NaN,
        };
    }

    /// <summary>Still in the entrance: put back there, keeping what the run started with.</summary>
    private static CurveBody Waiting(in CurveBody body) => new()
    {
        Position = body.EntranceAt,
        Velocity = PlayRules.SpawnVelocity,
        Target = body.EntranceAt,
        Placed = true,
        EntranceAt = body.EntranceAt,
        Entrance = body.Entrance,
        ReleaseMs = body.ReleaseMs,
        SongMs = body.SongMs,
    };

    /// <summary>Lays the body's centre on the preview trace when it has moved far enough.</summary>
    private static void Trace(List<Vector2> points, Vector2 at)
    {
        if (points.Count == 0 || Vector2.Distance(points[^1], at) >= BodyTrace.Spacing)
            points.Add(at);
    }

    /// <summary>
    /// A dash is over: the body runs right again at cruise speed, and gravity
    /// takes it from wherever the wall stopped it.
    /// </summary>
    private void EndDash(ref CurveBody body, Entity entity, double clock)
    {
        body.ImpactWay = body.Dash;
        body.ImpactMs = clock;
        body.Dash = Vector2.Zero;
        body.Velocity = PlayRules.SpawnVelocity;

        // The wall takes the hit: sparks thrown back off it, and a kick.
        PlayfieldView view = entity.GetComponent<PlayfieldView>();
        Vector2 contact = body.Position + body.ImpactWay * (PlayRules.BodySize / 2f);
        int burst = CommandBuffer.CreateEntity();
        CommandBuffer.AddComponent(burst, new ParticleBurst(
            view.PlayfieldToScreen(contact),
            GameParticles.Hit,
            PlayfieldColors.NeutralColor,
            PlayRules.BodySize * view.Scale * 0.4f,
            heading: -body.ImpactWay));
        PlayCameraSystem.Kick(entity, 1.2f);
    }

    /// <summary>
    /// The floor and the field's edges are walls: they stop the body, killing
    /// its speed into them; true when the body was moving into one.
    /// </summary>
    private static bool Contain(ref CurveBody body, Playfield playfield)
    {
        bool stopped = false;
        float half = PlayRules.BodyWidth / 2;
        float halfHeight = PlayRules.BodyHeight / 2;
        float floor = PlayRules.FloorY(playfield) - halfHeight;
        if (body.Position.Y > floor)
        {
            stopped |= body.Velocity.Y > 0f;
            body.Position.Y = floor;
            body.Velocity.Y = MathF.Min(body.Velocity.Y, 0f);
        }

        if (body.Position.Y < halfHeight)
        {
            stopped |= body.Velocity.Y < 0f;
            body.Position.Y = halfHeight;
            body.Velocity.Y = MathF.Max(body.Velocity.Y, 0f);
        }

        if (body.Position.X < half)
        {
            stopped |= body.Velocity.X < 0f;
            body.Position.X = half;
            body.Velocity.X = MathF.Max(body.Velocity.X, 0f);
        }

        if (body.Position.X > playfield.Width - half)
        {
            stopped |= body.Velocity.X > 0f;
            body.Position.X = playfield.Width - half;
            body.Velocity.X = MathF.Min(body.Velocity.X, 0f);
        }

        return stopped;
    }
}
