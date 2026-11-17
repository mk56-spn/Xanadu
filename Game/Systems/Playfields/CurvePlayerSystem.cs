using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The curve rework's player (PlayRules): a square that hovers, airborne by
/// default. Once a curve note's head is hit, and while the song is on its
/// path, the path is an attractor line
/// (<see cref="CurveAttractor"/>): the square keeps its own motion and is pulled
/// toward the line's nearest point. Off a curve it glides on, slowly losing
/// speed. The fixed floor and the field's edges stop it.
/// </summary>
public sealed class CurvePlayerSystem : QuerySystem<Player, Position, PlayfieldView, Playfield, Playhead>
{
    /// <summary>Off a curve, how quickly the glide loses speed (per second, exponential).</summary>
    public const float GlideDrag = 0.6f;

    /// <summary>A jump back this far in the song (a restart or a seek) puts the square at its start again, ms.</summary>
    private const long RestartJumpMs = 500;

    private readonly Dictionary<int, CurvePath> _paths = new();
    private readonly List<CurvePath> _steering = new();
    private CurvePath? _first;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;

        // One play entity at most, and the body is added to it on its first frame.
        foreach (Entity entity in Query.ToEntityList())
        {
            if (!entity.TryGetComponent(out PlayfieldMode mode) || mode.Kind != PlayfieldKind.Play)
                continue;

            Gather(entity.Store);
            Playfield playfield = entity.GetComponent<Playfield>();
            CurveBody body = entity.TryGetComponent(out CurveBody found) ? found : default;
            long now = entity.GetComponent<Playhead>().TimeMs;
            if (!body.Placed || now < body.SongMs - RestartJumpMs)
                body = Start(playfield);

            Move(ref body, now, delta, playfield);
            body.SongMs = now;
            entity.AddComponent(body);
            entity.GetComponent<Position>().Value = entity.GetComponent<PlayfieldView>().PlayfieldToScreen(body.Position);
        }
    }

    /// <summary>
    /// The first curve note still waiting (where the body starts), and the
    /// paths of every curve note whose head was hit, by start time. A missed
    /// head steers nothing.
    /// </summary>
    private void Gather(EntityStore store)
    {
        _first = null;
        _steering.Clear();
        foreach (var (notes, chunk) in store.Query<Note>().Chunks)
        {
            Span<Note> span = notes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (!span[i].IsCurve)
                    continue;

                Entity entity = chunk.EntityAt(i);
                CurvePath path = PathOf(entity.Id, span[i]);
                if (entity.Tags.Has<NoteSteering>())
                    _steering.Add(path);
                if (_first is null || path.StartMs < _first.StartMs)
                    _first = path;
            }
        }

        _steering.Sort(static (a, b) => a.StartMs.CompareTo(b.StartMs));
    }

    /// <summary>A note's path, built once per note and kept while the note is the same.</summary>
    private CurvePath PathOf(int id, in Note note)
    {
        if (_paths.TryGetValue(id, out CurvePath? path) && path.StartMs == note.TimeMs && path.Points[0] == note.Position)
            return path;
        path = CurvePath.Of(note);
        _paths[id] = path;
        return path;
    }

    /// <summary>At the first curve note's head, still; with none, over the middle of the floor.</summary>
    private CurveBody Start(Playfield playfield)
    {
        Vector2 at = _first is not null
            ? _first.At(_first.StartMs).Position
            : new Vector2(playfield.Width / 2f, PlayRules.FloorY(playfield) - PlayRules.SquareSize * 3);
        return new CurveBody { Position = at, Target = at, Placed = true };
    }

    private void Move(ref CurveBody body, long now, float delta, Playfield playfield)
    {
        body.OnCurve = false;
        if (_first is not null && now < _first.StartMs)
        {
            // Waiting at the first curve note's head.
            body.Position = _first.At(_first.StartMs).Position;
            body.Velocity = Vector2.Zero;
            body.Target = body.Position;
            return;
        }

        CurvePath? on = On(now);
        bool held = false;
        if (on is not null)
        {
            body.Velocity += CurveAttractor.Accelerate(on, body.Position, body.Velocity, out Vector2 nearest, out held) * delta;
            body.Target = nearest;
            body.OnCurve = true;
        }

        if (!held)
        {
            body.Velocity *= MathF.Exp(-GlideDrag * delta);
            if (on is null)
                body.Target = body.Position;
        }

        body.Position += body.Velocity * delta;
        Contain(ref body, playfield);
    }

    /// <summary>Of the hit curves covering a time, the one that started last.</summary>
    private CurvePath? On(long now)
    {
        CurvePath? on = null;
        foreach (CurvePath curve in _steering)
        {
            if (curve.StartMs > now)
                break;
            if (curve.Covers(now))
                on = curve;
        }

        return on;
    }

    /// <summary>The floor and the field's edges stop the square, killing its speed into them.</summary>
    private static void Contain(ref CurveBody body, Playfield playfield)
    {
        float half = PlayRules.SquareSize / 2;
        float floor = PlayRules.FloorY(playfield) - half;
        if (body.Position.Y > floor)
        {
            body.Position.Y = floor;
            body.Velocity.Y = MathF.Min(body.Velocity.Y, 0f);
        }

        if (body.Position.Y < half)
        {
            body.Position.Y = half;
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
