using System.Linq;
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Bench;

/// <summary>
/// The bench's map run: when the song has an editor map with route notes (each run straight ahead, no turns taken), the
/// bench plays that map instead of making notes up from the beat. The body is
/// airborne by default. While the song is on a curve, the curve's point at the
/// song time moves along the track and the body chases it directly
/// (<see cref="CurveAttractor"/>), never riding the line. Off a curve it glides on, slowly losing speed.
/// The camera follows the body; the map's notes show near their time but are
/// not played yet.
/// </summary>
public sealed partial class BenchSystem
{
    /// <summary>Stage pixels per playfield unit.</summary>
    private const float MapScale = 1f;

    /// <summary>Off a curve, how quickly the glide loses speed (per second, exponential).</summary>
    private const float GlideDrag = 0.6f;

    /// <summary>How quickly the camera catches the body (per second, exponential).</summary>
    private const float CameraRate = 5f;

    /// <summary>The body's lean at full speed sideways.</summary>
    private const float LeanSpeed = 1200f;

    /// <summary>How long before its start a curve shows, and after its end, ms.</summary>
    private const long CurveShowsBeforeMs = 2000;
    private const long CurveShowsAfterMs = 600;

    /// <summary>The map the current song plays, or null for the beat run.</summary>
    private BenchMap? _map;

    /// <summary>Every level in the library, read once when the bench opens.</summary>
    private List<LevelEntry>? _levels;

    private Vector2 _body;
    private Vector2 _bodyVelocity;

    /// <summary>The point on the current curve nearest the body, the one the line pulls toward.</summary>
    private Vector2 _point;
    private Vector2 _camera;
    private double _lastMapMs = double.NaN;
    private Vector2? _surfaceAt;

    /// <summary>The curve each line of the pool is showing, by index into the map's curves; -1 for none.</summary>
    private int[] _curveShown = [];

    /// <summary>The level for a song with the most curve notes, or null when none of its levels has one.</summary>
    private BenchMap? MapFor(string songId)
    {
        _levels ??= LevelStore.List();
        LevelEntry? best = null;
        foreach (LevelEntry entry in _levels)
        {
            int count = CurveCount(entry.Document);
            if (entry.IsCatalogOnly || entry.Document.SongId != songId || count == 0)
                continue;
            if (best is null || count > CurveCount(best.Document))
                best = entry;
        }

        return best is null ? null : BenchMap.From(best.Document);
    }

    private static int CurveCount(LevelDocument level) =>
        level.Notes.Count(note => note.Route is { Count: >= NoteRoute.MinTimes });

    /// <summary>Puts the body at the first curve's start, still, with the camera on it.</summary>
    private void ResetMapRun()
    {
        _body = _map is { Curves.Count: > 0 } map ? map.Curves[0].At(map.Curves[0].StartMs).Position : Vector2.Zero;
        _bodyVelocity = Vector2.Zero;
        _camera = _body;
        _lastMapMs = double.NaN;
        _tilt = 0;
        Array.Fill(_curveShown, -2);
    }

    private void RunMap(BenchView view, BenchMap map, double timeMs, float delta)
    {
        if (!double.IsNaN(timeMs))
        {
            // The song looped: start the run over.
            if (!double.IsNaN(_lastMapMs) && timeMs < _lastMapMs - 500)
                ResetMapRun();
            _lastMapMs = timeMs;
            MoveBody(map, timeMs, delta);
        }

        WriteBody(view, map, timeMs);

        _camera += (_body - _camera) * (1f - MathF.Exp(-CameraRate * delta));
        PutSurface(view, away: true);
        ShowBody(view, delta);
        ShowCurves(view, map, timeMs);
        ShowMapNotes(view, map, timeMs);
    }

    private void MoveBody(BenchMap map, double timeMs, float delta)
    {
        if (map.Curves.Count == 0)
            return;

        // Before the first curve the body waits where it starts.
        if (timeMs < map.Curves[0].StartMs)
        {
            ResetBodyTo(map.Curves[0].At(map.Curves[0].StartMs).Position);
            return;
        }

        RoutePath? on = map.On(timeMs);
        bool held = false;
        if (on is not null)
            _bodyVelocity += CurveAttractor.Accelerate(on, timeMs, _body, _bodyVelocity, out _point, out held) * delta;
        if (!held)
        {
            _point = _body;
            _bodyVelocity *= MathF.Exp(-GlideDrag * delta);
        }

        _body += _bodyVelocity * delta;
    }

    /// <summary>Puts where the body is, and where the curve wants it, on the player box for watchers.</summary>
    private void WriteBody(BenchView view, BenchMap map, double timeMs)
    {
        if (view.Player.IsNull)
            return;

        RoutePath? on = double.IsNaN(timeMs) ? null : map.On(timeMs);
        view.Player.AddComponent(new BenchBody
        {
            Position = _body,
            Target = on is null ? _body : _point,
            OnCurve = on is not null,
            SongMs = timeMs,
        });
    }

    private void ResetBodyTo(Vector2 at)
    {
        _body = at;
        _bodyVelocity = Vector2.Zero;
    }

    private Vector2 ToStage(Vector2 point) => UiStage.Full.Center + (point - _camera) * MapScale;

    /// <summary>The box where the camera shows the body, leaning into its sideways speed.</summary>
    private void ShowBody(BenchView view, float delta)
    {
        if (view.Player.IsNull || !view.Player.TryGetComponent(out Position position))
            return;

        float lean = Math.Clamp(_bodyVelocity.X / LeanSpeed, -1f, 1f) * RunTilt;
        _tilt += (lean - _tilt) * (1f - MathF.Exp(-9f * delta));
        position.Value = ToStage(_body);
        view.Player.AddComponent(position);
        view.Player.AddComponent(new UiRotation(_tilt));
        view.Player.AddTag<Moved>();
        ShowActions(view, position.Value);
    }

    /// <summary>Moves the beat run's floor out of sight for a map, and back for the beat run.</summary>
    private void PutSurface(BenchView view, bool away)
    {
        if (view.Surface.IsNull || !view.Surface.TryGetComponent(out Position position))
            return;

        _surfaceAt ??= position.Value;
        Vector2 at = away ? new Vector2(-10000, -10000) : _surfaceAt.Value;
        if (position.Value == at)
            return;

        position.Value = at;
        view.Surface.AddComponent(position);
        view.Surface.AddTag<Moved>();
    }

    /// <summary>
    /// Gives each pool line a curve near the playhead and moves it with the
    /// camera: upcoming and current curves in violet, ones the body has left dimmed.
    /// </summary>
    private void ShowCurves(BenchView view, BenchMap map, double timeMs)
    {
        if (view.Curves is not { Length: > 0 } pool)
            return;
        if (_curveShown.Length != pool.Length)
        {
            _curveShown = new int[pool.Length];
            Array.Fill(_curveShown, -2);
        }

        double now = double.IsNaN(timeMs) ? 0 : timeMs;
        int used = 0;
        for (int i = 0; i < map.Curves.Count && used < pool.Length; i++)
        {
            RoutePath path = map.Curves[i];
            if (now < path.StartMs - CurveShowsBeforeMs || now > path.EndMs + CurveShowsAfterMs)
                continue;

            Color stroke = now > path.EndMs ? Palette.Muted : PlayfieldColors.CurveColor;
            PlaceCurve(pool[used], used, i, path, stroke);
            used++;
        }

        for (; used < pool.Length; used++)
            HideCurve(pool[used], used);
    }

    private void PlaceCurve(Entity line, int slot, int curve, RoutePath path, Color stroke)
    {
        if (line.IsNull || !line.TryGetComponent(out Position position))
            return;

        ref UiShape shape = ref line.GetComponent<UiShape>();
        bool dirty = false;
        if (_curveShown[slot] != curve)
        {
            _curveShown[slot] = curve;
            Vector2 origin = path.Points[0];
            var points = new Vector2[path.Points.Count];
            Vector2 min = Vector2.Zero;
            Vector2 max = Vector2.Zero;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = (path.Points[i] - origin) * MapScale;
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            shape.Points = points;
            if (line.HasComponent<Rectangle>())
                line.GetComponent<Rectangle>().Bounds = new Rect2(min, max - min);
            dirty = true;
        }

        if (shape.Stroke != stroke)
        {
            shape.Stroke = stroke;
            dirty = true;
        }

        if (dirty)
            line.AddTag<Dirty>();

        position.Value = ToStage(path.Points[0]);
        line.AddComponent(position);
        line.AddTag<Moved>();
    }

    private void HideCurve(Entity line, int slot)
    {
        if (_curveShown[slot] == -1 || line.IsNull || !line.TryGetComponent(out Position position))
            return;

        _curveShown[slot] = -1;
        position.Value = new Vector2(-10000, -10000);
        line.AddComponent(position);
        line.AddTag<Moved>();
    }

    /// <summary>The map's notes, shown where they sit as they come up; they aren't played yet.</summary>
    private void ShowMapNotes(BenchView view, BenchMap map, double timeMs)
    {
        if (view.Notes is null)
            return;

        int used = 0;
        if (!double.IsNaN(timeMs))
        {
            long now = (long)timeMs;
            foreach (Note note in map.Notes)
            {
                if (used >= view.Notes.Length)
                    break;
                if (NoteFade.Alpha(note, now) <= 0f)
                    continue;
                PlaceNote(view.Notes[used++], ToStage(note.Position), PlayfieldColors.ColorFor(note));
            }
        }

        for (; used < view.Notes.Length; used++)
            PlaceNote(view.Notes[used], new Vector2(-1000, -1000), Palette.TextMuted);
    }
}

/// <summary>A level as the bench plays it: its curve notes laid out by start time, and its notes.</summary>
public sealed class BenchMap
{
    private BenchMap(string name, List<RoutePath> curves, List<Note> notes)
    {
        Name = name;
        Curves = curves;
        Notes = notes;
    }

    public string Name { get; }

    public IReadOnlyList<RoutePath> Curves { get; }

    public IReadOnlyList<Note> Notes { get; }

    public static BenchMap From(LevelDocument level)
    {
        var curves = new List<RoutePath>();
        foreach (NoteDocument note in level.Notes)
        {
            if (note.BuildRoute() is { } route)
                curves.Add(RoutePath.Of(new Note(note.TimeMs, new Vector2(note.X, note.Y), route: route)));
        }

        curves.Sort(static (a, b) => a.StartMs.CompareTo(b.StartMs));

        var notes = new List<Note>();
        foreach (NoteDocument note in level.Notes)
            notes.Add(note.ToNote());
        notes.Sort(static (a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return new BenchMap(level.Name, curves, notes);
    }

    /// <summary>The curve the song is on: of those covering the time, the one that started last.</summary>
    public RoutePath? On(double timeMs)
    {
        RoutePath? on = null;
        foreach (RoutePath curve in Curves)
        {
            if (curve.StartMs > timeMs)
                break;
            if (curve.Covers(timeMs))
                on = curve;
        }

        return on;
    }
}
