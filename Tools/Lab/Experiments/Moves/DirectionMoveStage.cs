using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Moves;

/// <summary>
/// The field for <see cref="DirectionMoveExperiment"/>: a square with gravity on
/// a floor and a few platforms, and notes placed in the field along a route.
/// The route is written by a hidden copy of the square that picks a direction
/// at each note; each note sits where that copy is on its beat, so the next
/// note's place is the only hint of which way to go. Sizes are in field units,
/// 900 to the field's height, so it plays the same at any window size.
/// </summary>
public partial class DirectionMoveStage : Node2D
{
    private enum Kind { Dash, Momentum, Hit }

    /// <summary>Everything that moves the square, so the route and the player share one simulation.</summary>
    private struct Body
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public bool Grounded;
        public double HangUntil;
    }

    private sealed class RouteNote(double timeMs, Kind kind, int strength, Body before, Vector2 direction)
    {
        public readonly double TimeMs = timeMs;
        public readonly Kind Kind = kind;
        public readonly int Strength = strength;

        /// <summary>The route's square on the note's beat, just before the note moves it.</summary>
        public readonly Body Before = before;

        /// <summary>The way the route went; the player only ever sees where the next note is.</summary>
        public readonly Vector2 Direction = direction;

        public Vector2 Position => Before.Position;
        public bool Done;
        public bool Missed;
        public double DoneMs;
    }

    private const float Height = 900f;
    private const float Size = 40f;
    private const float NoteRadius = 30f;
    private const float Gravity = 2400f;
    private const double StepMs = 1000.0 / 240.0;
    private const float HitWindowMs = 130f;

    /// <summary>How far from the square a note can be and still be hit: the game's reach.</summary>
    private const float Reach = 100f;

    /// <summary>How long after an action a direction press still steers it.</summary>
    private const float ChordMs = 80f;

    /// <summary>How long a dash holds the square still in the air where it lands.</summary>
    private const float HangMs = 120f;

    private const float LeadInMs = 1500f;
    private const float StrongestFriction = 2600f;

    private static readonly float[] DashDistance = [0, 120, 220, 320];
    private static readonly float[] MomentumSpeed = [0, 520, 780, 1040];

    /// <summary>One bar each: beat, kind, strength.</summary>
    private static readonly (float Beat, Kind Kind, int Strength)[][] Bars =
    [
        [(0, Kind.Momentum, 1), (1, Kind.Hit, 1), (2, Kind.Dash, 1), (3, Kind.Hit, 1)],
        [(0, Kind.Momentum, 2), (1.5f, Kind.Dash, 1), (2, Kind.Hit, 1), (3, Kind.Momentum, 1)],
        [(0, Kind.Dash, 2), (1, Kind.Hit, 1), (2, Kind.Momentum, 3), (3.5f, Kind.Hit, 1)],
        [(0, Kind.Momentum, 1), (1, Kind.Momentum, 1), (2, Kind.Dash, 1), (3, Kind.Dash, 2)],
    ];

    private static readonly Vector2[] Ways = [Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down];

    private static readonly Dictionary<Key, Vector2> Directions = new()
    {
        [Key.S] = Vector2.Left,
        [Key.D] = Vector2.Right,
        [Key.K] = Vector2.Up,
        [Key.L] = Vector2.Down,
    };

    private static readonly Key[] Actions = [Key.F, Key.Space, Key.J];

    private readonly Rect2 _area;
    private readonly DirectionMoveExperiment _experiment = null!;
    private readonly Rect2[] _blocks = [];
    private readonly List<RouteNote> _notes = [];
    private readonly List<Key> _held = [];
    private readonly RandomNumberGenerator _random = new() { Seed = 11 };
    private readonly Font _font = ThemeDB.FallbackFont;

    private Rect2 _field;
    private float _scale;
    private double _now;

    /// <summary>The time the player's square has been simulated to, in fixed steps.</summary>
    private double _simMs;
    private Body _body;

    /// <summary>The hidden square writing the route, and how far it has got.</summary>
    private Body _writer;
    private double _writerMs;
    private int _bar;
    private double _barStartMs = LeadInMs;

    /// <summary>A note fired with no direction held, waiting a moment for one.</summary>
    private RouteNote? _pending;
    private double _pendingUntil;

    private int _hits;
    private int _misses;
    private string _judgement = "";
    private double _judgementAt = double.NegativeInfinity;

    public DirectionMoveStage() { }

    public DirectionMoveStage(Rect2 area, DirectionMoveExperiment experiment)
    {
        _area = area;
        _experiment = experiment;

        _field = area.Grow(-20);
        _scale = _field.Size.Y / Height;
        float width = _field.Size.X / _scale;
        _blocks =
        [
            new Rect2(0, Height - 40, width, 40),
            new Rect2(width * 0.12f, 600, width * 0.22f, 30),
            new Rect2(width * 0.62f, 560, width * 0.24f, 30),
            new Rect2(width * 0.38f, 360, width * 0.2f, 30),
        ];
        _body = new Body { Position = new Vector2(width / 2, Height - 40 - Size / 2), HangUntil = double.NegativeInfinity };
        _writer = _body;
    }

    private float FieldWidth => _field.Size.X / _scale;

    /// <summary>How long before its beat a note shows.</summary>
    private double ApproachMs => _experiment.BeatMs * 2;

    public override void _Process(double delta)
    {
        _experiment.Poll();
        _now += Math.Min(delta, 0.05) * 1000.0;
        WriteRoute();
        if (_experiment.Auto)
            PlayItself();
        Judge();
        _body = Advance(_body, _simMs, _now, out _simMs);
        _notes.RemoveAll(note => _now - note.TimeMs > 1000);
        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (_experiment.Auto || @event is not InputEventKey { Echo: false } key)
            return;

        if (Directions.ContainsKey(key.Keycode))
        {
            _held.Remove(key.Keycode);
            if (key.Pressed)
            {
                _held.Add(key.Keycode);
                if (_pending is { } waiting && _now <= _pendingUntil)
                {
                    Resume(waiting, Directions[key.Keycode]);
                    _pending = null;
                }
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        int action = Array.IndexOf(Actions, key.Keycode);
        if (!key.Pressed || action < 0)
            return;

        Press(action);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>The way the last direction key still held points, or zero.</summary>
    private Vector2 HeldDirection => _held.Count > 0 ? Directions[_held[^1]] : Vector2.Zero;

    private void Press(int action)
    {
        RouteNote? best = null;
        foreach (RouteNote note in _notes)
        {
            if (note.Done || Math.Abs(_now - note.TimeMs) > HitWindowMs)
                continue;
            if (best is null || note.TimeMs < best.TimeMs)
                best = note;
        }

        if (best is null)
            return;

        if (_body.Position.DistanceTo(best.Position) > Reach)
        {
            Judgement("OUT OF REACH");
            return;
        }

        if (_experiment.Typed && action != KeyFor(best.Kind))
        {
            Judgement("WRONG KEY");
            return;
        }

        best.Done = true;
        best.DoneMs = _now;
        _hits++;
        float offset = (float)(_now - best.TimeMs);
        float early = Math.Abs(offset);
        string grade = early <= 35 ? "PERFECT" : early <= 80 ? "GREAT" : "GOOD";
        Judgement($"{grade} {offset:+0;-0;0}");

        Vector2 direction = HeldDirection;
        if (best.Kind == Kind.Hit || direction != Vector2.Zero)
        {
            Resume(best, direction);
            return;
        }

        // No direction yet: the square carries on from the note without a move until one comes.
        Resume(best, Vector2.Zero);
        _pending = best;
        _pendingUntil = _now + ChordMs;
    }

    /// <summary>
    /// Runs the square on from a note's beat, not from the press: it starts from
    /// the route's square there, takes the move the way given, and is simulated
    /// up to now. Press timing never pushes the square off the chart.
    /// </summary>
    private void Resume(RouteNote note, Vector2 direction)
    {
        Body body = note.Before;
        if (note.Kind != Kind.Hit && direction != Vector2.Zero)
            Fire(ref body, note.Kind, note.Strength, direction, note.TimeMs);
        _body = Advance(body, note.TimeMs, _simMs, out _);
    }

    /// <summary>F fires dashes, Space hits and J momentum, when actions are typed.</summary>
    private static int KeyFor(Kind kind) => kind switch
    {
        Kind.Dash => 0,
        Kind.Hit => 1,
        _ => 2,
    };

    private void Judge()
    {
        if (_pending is not null && _now > _pendingUntil)
        {
            _pending = null;
            Judgement("NO DIRECTION");
        }

        foreach (RouteNote note in _notes)
        {
            if (note.Done || _now - note.TimeMs <= HitWindowMs)
                continue;

            // A miss puts the square back on the route, so the next notes stay in reach.
            note.Done = true;
            note.Missed = true;
            note.DoneMs = _now;
            _misses++;
            Judgement("MISS");
            Resume(note, note.Direction);
        }
    }

    private void Judgement(string text)
    {
        _judgement = text;
        _judgementAt = _now;
    }

    /// <summary>Fires each note on its beat the way the route went.</summary>
    private void PlayItself()
    {
        foreach (RouteNote note in _notes)
        {
            if (note.Done || _now < note.TimeMs)
                continue;
            note.Done = true;
            note.DoneMs = _now;
            _hits++;
            Judgement("AUTO");
            Resume(note, note.Direction);
        }
    }

    // ---- The route ----

    private void WriteRoute()
    {
        float beat = _experiment.BeatMs;
        while (_barStartMs < _now + ApproachMs * 2 + beat * 4)
        {
            foreach ((float b, Kind kind, int strength) in Bars[_bar % Bars.Length])
                WriteNote(_barStartMs + b * beat, kind, strength);
            _barStartMs += beat * 4;
            _bar++;
        }
    }

    /// <summary>Moves the hidden square on to the note's beat, places the note there and picks a way to go.</summary>
    private void WriteNote(double timeMs, Kind kind, int strength)
    {
        _writer = Advance(_writer, _writerMs, timeMs, out _);
        _writerMs = timeMs;
        Body before = _writer;
        Vector2 direction = kind == Kind.Hit ? Vector2.Zero : PickWay(before, kind, strength, timeMs);
        if (direction != Vector2.Zero)
            Fire(ref _writer, kind, strength, direction, timeMs);
        _notes.Add(new RouteNote(timeMs, kind, strength, before, direction));
    }

    /// <summary>
    /// A random way that does something worth reading: it moves the square at
    /// least a little over the next beat and keeps it off the ceiling.
    /// </summary>
    private Vector2 PickWay(Body from, Kind kind, int strength, double timeMs)
    {
        int start = _random.RandiRange(0, Ways.Length - 1);
        for (int i = 0; i < Ways.Length; i++)
        {
            Vector2 way = Ways[(start + i) % Ways.Length];
            Body trial = from;
            Fire(ref trial, kind, strength, way, timeMs);
            Body later = Advance(trial, timeMs, timeMs + _experiment.BeatMs, out _);
            bool moved = Wrapped(later.Position - from.Position).Length() > 60;
            bool clear = later.Position.Y > Size * 2;
            if (moved && clear)
                return way;
        }

        return Ways[start];
    }

    /// <summary>The shortest offset across the wrapped field.</summary>
    private Vector2 Wrapped(Vector2 offset) =>
        new(Mathf.PosMod(offset.X + FieldWidth / 2, FieldWidth) - FieldWidth / 2, offset.Y);

    // ---- The simulation ----

    private void Fire(ref Body body, Kind kind, int strength, Vector2 direction, double atMs)
    {
        if (kind == Kind.Dash)
        {
            body.Position += direction * DashDistance[strength];
            body.Position.X = Mathf.PosMod(body.Position.X, FieldWidth);
            body.Position.Y = Math.Clamp(body.Position.Y, Size / 2, Height - Size / 2);
            for (int i = 0; i < 400 && Blocked(body.Position); i++)
                body.Position -= direction * 2f;
            body.Velocity = Vector2.Zero;
            body.HangUntil = atMs + HangMs;
        }
        else
        {
            float speed = MomentumSpeed[strength];
            body.Velocity = new Vector2(
                Along(body.Velocity.X, direction.X * speed),
                Along(body.Velocity.Y, direction.Y * speed));
            body.HangUntil = double.NegativeInfinity;
        }
    }

    /// <summary>The game's rule on one axis: the push's speed, unless already going faster that way.</summary>
    private static float Along(float current, float pushed)
    {
        if (pushed == 0)
            return current;
        return Math.Sign(current) == Math.Sign(pushed) && Math.Abs(current) > Math.Abs(pushed) ? current : pushed;
    }

    /// <summary>Simulates a body in fixed steps from one time towards another; <paramref name="reached"/> is where the steps ended.</summary>
    private Body Advance(Body body, double fromMs, double toMs, out double reached)
    {
        double t = fromMs;
        while (t + StepMs <= toMs)
        {
            Step(ref body, t, (float)(StepMs / 1000.0));
            t += StepMs;
        }

        reached = t;
        return body;
    }

    private void Step(ref Body body, double atMs, float dt)
    {
        if (atMs >= body.HangUntil)
            body.Velocity.Y += Gravity * dt;
        if (body.Grounded && body.Velocity.Y >= 0)
        {
            float loss = _experiment.Friction * StrongestFriction * dt;
            body.Velocity.X = Math.Abs(body.Velocity.X) <= loss ? 0 : body.Velocity.X - Math.Sign(body.Velocity.X) * loss;
        }

        body.Position.X = Mathf.PosMod(body.Position.X + body.Velocity.X * dt, FieldWidth);
        Resolve(ref body, horizontal: true);

        body.Position.Y += body.Velocity.Y * dt;
        body.Grounded = false;
        if (body.Position.Y < Size / 2)
        {
            body.Position.Y = Size / 2;
            body.Velocity.Y = Math.Max(body.Velocity.Y, 0);
        }

        Resolve(ref body, horizontal: false);
        if (!body.Grounded && body.Velocity.Y >= 0)
        {
            var feet = new Rect2(body.Position + new Vector2(-Size / 2, Size / 2), new Vector2(Size, 1));
            foreach (Rect2 block in _blocks)
            {
                if (feet.Intersects(block))
                    body.Grounded = true;
            }
        }
    }

    /// <summary>Moves a body out of any block it overlaps, along one axis, stopping its speed into it.</summary>
    private void Resolve(ref Body body, bool horizontal)
    {
        foreach (Rect2 block in _blocks)
        {
            Rect2 box = Box(body.Position);
            if (!box.Intersects(block))
                continue;

            if (horizontal)
            {
                bool fromLeft = body.Velocity.X > 0 || (body.Velocity.X == 0 && box.GetCenter().X < block.GetCenter().X);
                body.Position.X = fromLeft ? block.Position.X - Size / 2 : block.End.X + Size / 2;
                body.Velocity.X = 0;
            }
            else if (body.Velocity.Y >= 0)
            {
                body.Position.Y = block.Position.Y - Size / 2;
                body.Velocity.Y = 0;
                body.Grounded = true;
            }
            else
            {
                body.Position.Y = block.End.Y + Size / 2;
                body.Velocity.Y = 0;
            }
        }
    }

    private bool Blocked(Vector2 at)
    {
        foreach (Rect2 block in _blocks)
        {
            if (Box(at).Intersects(block))
                return true;
        }

        return false;
    }

    private static Rect2 Box(Vector2 centre) => new(centre - new Vector2(Size, Size) / 2, new Vector2(Size, Size));

    // ---- Drawing ----

    private Vector2 Screen(Vector2 field) => _field.Position + field * _scale;

    private static Color KindColor(Kind kind) => kind switch
    {
        Kind.Dash => Palette.Cyan,
        Kind.Momentum => Palette.Violet,
        _ => Palette.BorderSubtle,
    };

    public override void _Draw()
    {
        DrawRect(_area, Colors.White);
        DrawRect(_field, new Color("#eef2f7"));
        foreach (Rect2 block in _blocks)
            DrawRect(new Rect2(Screen(block.Position), block.Size * _scale), Palette.SurfaceAlt);

        DrawGuides();
        if (_experiment.Guides)
            DrawGhostPath();
        foreach (RouteNote note in _notes)
            DrawNote(note);
        DrawSquare();
        DrawHud();
    }

    /// <summary>Faint lines joining the coming notes in order, as the game's guides do.</summary>
    private void DrawGuides()
    {
        RouteNote? previous = null;
        foreach (RouteNote note in _notes)
        {
            if (note.Done || note.TimeMs - _now > ApproachMs)
                continue;
            if (previous is not null && Wrapped(note.Position - previous.Position).Length() < FieldWidth / 2)
                DrawLine(Screen(previous.Position), Screen(note.Position), Palette.BorderSubtle with { A = 0.35f }, 2);
            previous = note;
        }
    }

    /// <summary>A note in the field: it fades in while a ring shrinks onto it, then pops or greys out.</summary>
    private void DrawNote(RouteNote note)
    {
        Vector2 at = Screen(note.Position);
        float r = NoteRadius * _scale;
        Color color = KindColor(note.Kind);

        if (note.Done)
        {
            float age = (float)((_now - note.DoneMs) / 300.0);
            if (age >= 1)
                return;
            Color gone = note.Missed ? Palette.TextDim : color;
            DrawArc(at, r * (1 + age * 0.8f), 0, Mathf.Tau, 40, gone with { A = 1 - age }, 3);
            return;
        }

        float until = (float)((note.TimeMs - _now) / ApproachMs);
        if (until > 1)
            return;

        float alpha = Math.Clamp((1 - until) * 2.5f, 0, 1);
        DrawCircle(at, r, color with { A = 0.85f * alpha });
        DrawArc(at, r * (1 + Math.Max(until, 0) * 2.2f), 0, Mathf.Tau, 48, color with { A = alpha }, 3);
        if (note.Kind != Kind.Hit)
        {
            for (int s = 0; s < note.Strength; s++)
                DrawRect(new Rect2(at + new Vector2((-note.Strength * 9 + 3 + s * 18) * _scale, -4 * _scale), new Vector2(12, 8) * _scale), Palette.TextBright with { A = alpha });
        }

        if (_experiment.Guides && note.Direction != Vector2.Zero)
            DrawArrow(at, note.Direction, r, Palette.SurfaceDeep with { A = alpha });

        if (_experiment.Typed)
            DrawString(_font, at + new Vector2(-6, r + 18), KeyLabel(note.Kind), HorizontalAlignment.Left, -1, 16, Palette.SurfaceDeep with { A = alpha });
    }

    /// <summary>A chevron just outside the note, pointing the way the route went.</summary>
    private void DrawArrow(Vector2 at, Vector2 direction, float r, Color color)
    {
        Vector2 tip = at + direction * (r + 16 * _scale);
        Vector2 back = tip - direction * 14 * _scale;
        Vector2 side = direction.Orthogonal() * 10 * _scale;
        DrawColoredPolygon([tip, back + side, back - side], color);
    }

    /// <summary>
    /// Where the next movement note's route move carries the square, as a
    /// dotted trail up to the note after it: what pressing the guided way does.
    /// </summary>
    private void DrawGhostPath()
    {
        RouteNote? next = null;
        RouteNote? after = null;
        foreach (RouteNote note in _notes)
        {
            if (note.Done)
                continue;
            if (next is null)
            {
                if (note.Kind == Kind.Hit)
                    continue;
                next = note;
            }
            else
            {
                after = note;
                break;
            }
        }

        if (next is null || next.TimeMs - _now > ApproachMs)
            return;

        Body body = next.Before;
        Fire(ref body, next.Kind, next.Strength, next.Direction, next.TimeMs);
        double end = after?.TimeMs ?? next.TimeMs + _experiment.BeatMs;
        Vector2 last = body.Position;
        Color dot = KindColor(next.Kind) with { A = 0.55f };
        DrawCircle(Screen(last), 4 * _scale, dot);
        for (double t = next.TimeMs; t + StepMs <= end;)
        {
            body = Advance(body, t, Math.Min(t + 40, end), out t);
            if (Wrapped(body.Position - last).Length() > 8)
            {
                DrawCircle(Screen(body.Position), 4 * _scale, dot);
                last = body.Position;
            }
        }
    }

    private void DrawSquare()
    {
        Vector2 centre = Screen(_body.Position);
        float half = Size / 2 * _scale;
        DrawRect(new Rect2(centre - new Vector2(half, half), new Vector2(half * 2, half * 2)), Palette.Primary);

        Vector2 held = HeldDirection;
        if (held != Vector2.Zero)
            DrawLine(centre, centre + held * half * 2.4f, Palette.Accent, 4);
    }

    private static string KeyLabel(Kind kind) => kind switch
    {
        Kind.Dash => "F",
        Kind.Hit => "SPACE",
        _ => "J",
    };

    private void DrawHud()
    {
        Vector2 at = _field.Position + new Vector2(16, 34);
        DrawString(_font, at, $"HITS {_hits}   MISSES {_misses}", HorizontalAlignment.Left, -1, 22, Palette.SurfaceDeep);
        float age = (float)(_now - _judgementAt);
        if (age < 600)
        {
            Color color = _judgement.StartsWith('P') ? Palette.WarningDeep : Palette.SurfaceDeep;
            DrawString(_font, Screen(_body.Position) + new Vector2(-60, -50 * _scale), _judgement,
                HorizontalAlignment.Left, -1, 20, color with { A = 1f - age / 600f });
        }
    }
}
