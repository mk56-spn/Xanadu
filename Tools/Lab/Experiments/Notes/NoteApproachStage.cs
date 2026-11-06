using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Notes;

/// <summary>The stage's rectangle, clipping the field inside it: notes fly and fall in from past its edges.</summary>
public partial class NoteApproachStage : Node2D
{
    private readonly Rect2 _area;

    public NoteApproachStage() { }

    public NoteApproachStage(Rect2 area, NoteApproachExperiment experiment)
    {
        _area = area;
        ClipChildren = ClipChildrenMode.Only;
        AddChild(new NoteApproachField(area, experiment));
    }

    public override void _Draw() => DrawRect(_area, Colors.White);
}

/// <summary>
/// An endless chart of hit notes wandering round the stage, a bar at a time,
/// with the hits judged on the field's own clock. The drawing is in
/// <c>NoteApproachField.Draw.cs</c>.
/// </summary>
public partial class NoteApproachField : Node2D
{
    /// <summary>A note's radius: the game's 72-unit note at playfield scale 1.</summary>
    private const float Radius = 36f;

    /// <summary>How early or late a key still hits a note.</summary>
    private const float HitWindowMs = 150f;

    /// <summary>Time before the first beat, so the first notes approach in full.</summary>
    private const float LeadInMs = 400f;

    /// <summary>How long a hit's burst, or a miss's fade, lasts.</summary>
    private const float BurstMs = 360f;

    private const float JudgementMs = 550f;

    /// <summary>
    /// The beats of each bar, in turn: quarters, some eighths for runs, and
    /// chords, written as a beat listed more than once.
    /// </summary>
    private static readonly float[][] Bars =
    [
        [0, 0, 1, 2, 3],
        [0, 1, 1.5f, 2, 2, 3],
        [0, 0.5f, 1, 2, 3, 3, 3],
        [0, 1, 2, 2.5f, 3, 3.5f],
        [0, 0, 1, 1, 2, 3],
        [0, 1, 1.5f, 2, 3, 3],
    ];

    /// <summary>
    /// How far apart a chord's notes sit. A press reaches 100 units round the
    /// player, so a chord has to fit inside that from its middle.
    /// </summary>
    private const float PairSpacing = 140f;

    private const float TripleRadius = 85f;

    private static readonly Color[] LaneColors =
        [Palette.Cyan, Palette.PrimarySoft, Palette.Violet, Palette.Accent, Palette.Pink];

    private static readonly Key[] HitKeys = [Key.Space, Key.Z, Key.X, Key.J, Key.K];

    private sealed class ChartNote
    {
        public double TimeMs;
        public Vector2 Position;
        public Color Color;

        /// <summary>Which way a flying note comes from, and its wave's sign and size.</summary>
        public Vector2 FlyFrom;
        public float Wave;

        /// <summary>Turns the burst's sparks so no two hits look the same.</summary>
        public float Spin;

        public double HitMs = double.NaN;
        public double MissMs = double.NaN;
        public string Judgement = "";

        public bool Hit => !double.IsNaN(HitMs);
        public bool Missed => !double.IsNaN(MissMs);
    }

    private readonly Rect2 _area;
    private readonly NoteApproachExperiment _experiment = null!;
    private readonly List<ChartNote> _notes = [];
    private readonly RandomNumberGenerator _random = new() { Seed = 7 };
    private readonly Font _font = ThemeDB.FallbackFont;

    private double _now;

    /// <summary>The bar the chart writes next, and when it starts.</summary>
    private int _bar;
    private double _barStartMs = LeadInMs;

    private Vector2 _walk;
    private float _heading;
    private int _lane;

    public NoteApproachField() { }

    public NoteApproachField(Rect2 area, NoteApproachExperiment experiment)
    {
        _area = area;
        _experiment = experiment;
        _walk = area.GetCenter();
        _heading = -0.4f;
    }

    public override void _Process(double delta)
    {
        _experiment.Poll();
        // Hitches are not chart time: a long frame just stalls the notes.
        _now += Math.Min(delta, 0.1) * 1000.0;
        WriteChart();
        Judge();
        _notes.RemoveAll(note => _now - note.TimeMs > 1200);
        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_experiment.Keys || @event is not InputEventKey { Pressed: true, Echo: false } key
            || Array.IndexOf(HitKeys, key.Keycode) < 0)
            return;

        ChartNote? best = null;
        foreach (ChartNote note in _notes)
        {
            if (note.Hit || note.Missed || Math.Abs(_now - note.TimeMs) > HitWindowMs)
                continue;
            if (best is null || note.TimeMs < best.TimeMs)
                best = note;
        }

        if (best is null)
            return;

        float offset = (float)(_now - best.TimeMs);
        best.HitMs = _now;
        float early = Math.Abs(offset);
        string grade = early <= 35 ? "PERFECT" : early <= 80 ? "GREAT" : "GOOD";
        best.Judgement = $"{grade} {offset:+0;-0;0}";
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Writes bars until the chart reaches past what the longest approach can show.</summary>
    private void WriteChart()
    {
        float beat = _experiment.BeatMs;
        while (_barStartMs < _now + _experiment.ApproachMs + beat * 4)
        {
            float[] beats = Bars[_bar % Bars.Length];
            float previous = -1;
            for (int i = 0; i < beats.Length;)
            {
                float b = beats[i];
                int count = 1;
                while (i + count < beats.Length && beats[i + count] == b)
                    count++;
                i += count;

                float gap = previous < 0 ? 1 : b - previous;
                previous = b;
                WriteChord(_barStartMs + b * beat, gap, _experiment.Chords ? count : 1);
            }

            _barStartMs += beat * 4;
            _bar++;
        }
    }

    /// <summary>
    /// One beat's notes round the next spot on the path: alone, a pair across
    /// the path or a triangle. A chord flies in together, as one formation.
    /// </summary>
    private void WriteChord(double timeMs, float gapBeats, int count)
    {
        ChartNote first = NextNote(timeMs, gapBeats);
        if (count == 1)
        {
            _notes.Add(first);
            return;
        }

        Vector2 middle = first.Position;
        for (int n = 0; n < count; n++)
        {
            Vector2 offset = count == 2
                ? Vector2.FromAngle(_heading + Mathf.Pi / 2) * PairSpacing * (n - 0.5f)
                : Vector2.FromAngle(_heading + n * Mathf.Tau / count) * TripleRadius;
            _lane = (_lane + 1 + _random.RandiRange(0, 1)) % LaneColors.Length;
            _notes.Add(new ChartNote
            {
                TimeMs = timeMs,
                Position = middle + offset,
                Color = LaneColors[_lane],
                FlyFrom = first.FlyFrom,
                Wave = first.Wave,
                Spin = _random.Randf() * Mathf.Tau,
            });
        }
    }

    /// <summary>
    /// The next spot along a wandering path: further for a longer gap, turning a
    /// little each step and back toward the middle near an edge.
    /// </summary>
    private ChartNote NextNote(double timeMs, float gapBeats)
    {
        Rect2 inner = _area.Grow(-Radius * 3.5f);
        float step = 130f + 130f * Math.Min(gapBeats, 1f);
        _heading += _random.RandfRange(-1.1f, 1.1f);
        Vector2 next = _walk + Vector2.FromAngle(_heading) * step;
        if (!inner.HasPoint(next))
        {
            _heading = (inner.GetCenter() - _walk).Angle() + _random.RandfRange(-0.6f, 0.6f);
            next = _walk + Vector2.FromAngle(_heading) * step;
        }

        _walk = next.Clamp(inner.Position, inner.End);
        _lane = (_lane + 1 + _random.RandiRange(0, 2)) % LaneColors.Length;

        // Flying notes come in from across the path, from one side or the other.
        float side = _random.Randf() < 0.5f ? -1f : 1f;
        return new ChartNote
        {
            TimeMs = timeMs,
            Position = _walk,
            Color = LaneColors[_lane],
            FlyFrom = Vector2.FromAngle(_heading + side * _random.RandfRange(1.1f, 2.0f)),
            Wave = side * _random.RandfRange(40f, 70f),
            Spin = _random.Randf() * Mathf.Tau,
        };
    }

    /// <summary>On auto every note hits on its beat; on keys a note past the window is missed.</summary>
    private void Judge()
    {
        foreach (ChartNote note in _notes)
        {
            if (note.Hit || note.Missed)
                continue;
            if (!_experiment.Keys && _now >= note.TimeMs)
                note.HitMs = note.TimeMs;
            else if (_experiment.Keys && _now - note.TimeMs > HitWindowMs)
            {
                note.MissMs = _now;
                note.Judgement = "MISS";
            }
        }
    }

    /// <summary>0 on a beat, rising to 1 just before the next.</summary>
    private float BeatPhase()
    {
        double beats = (_now - LeadInMs) / _experiment.BeatMs;
        return beats < 0 ? 1f : (float)(beats - Math.Floor(beats));
    }
}
