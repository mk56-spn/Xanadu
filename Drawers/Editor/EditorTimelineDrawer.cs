using Xanadu.Ui.Systems.Canvas;
using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Timeline;
using Xanadu.Model.Playfields;
using Xanadu.Model.Editor;

namespace Xanadu.Drawers.Editor;

/// <summary>
/// The editor's timeline: the ruler, its beat bands, the song's waveform, the
/// BPM and start markers, the tick grid, the note slots and the playhead.
/// </summary>
public sealed class EditorTimelineDrawer
{
    // Softened so the grid guides the eye without outshouting the notes on it.
    public static readonly Color MainSnapColor = Palette.WithAlpha(Colors.White, 0.6f);
    public static readonly Color HalfSnapColor = Palette.WithAlpha(Palette.Danger, 0.55f);
    public static readonly Color QuarterSnapColor = Palette.WithAlpha(Palette.PrimarySoft, 0.55f);

    private const float MarkerTagHeight = 18f;
    private const float MarkerTagPadding = 6f;
    private const float MarkerTagGap = 3f;

    /// <summary>A rough advance for bold caption text; the canvas cannot measure.</summary>
    private const float MarkerTagCharWidth = 7.5f;

    private static readonly Color MarkerTagBackground = Palette.WithAlpha(Palette.SurfaceDeep, 0.94f);

    private readonly EditorDrawSurface _surface;
    private readonly EditorWaveformDrawer _waveform;

    public EditorTimelineDrawer(EditorDrawSurface surface)
    {
        _surface = surface;
        _waveform = new EditorWaveformDrawer(surface);
    }

    public void Draw(EditorFrame frame)
    {
        PlayfieldView view = frame.View;
        Playhead playhead = frame.Playhead;
        TimelineView timeline = frame.Timeline;
        Rect2 ruler = EditorLayout.RulerRect(view);
        _surface.Canvas.DrawRect(_surface.Target, ruler, Palette.SurfaceDeep);
        _surface.Canvas.DrawBorder(_surface.Target, ruler, PlayfieldColors.BorderColor, 1);

        if (playhead.DurationMs <= 0)
        {
            _surface.Canvas.DrawText(
                _surface.Target,
                ruler,
                "Load a song to enable the timeline",
                PlayfieldColors.DimTextColor);
            return;
        }

        (long windowStart, long windowEnd) = EditorLayout.TimelineWindow(playhead, timeline);

        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        _waveform.Draw(ruler, frame.Preview, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        var painter = new TimelinePainter(_surface.Canvas, _surface.Target);
        TimelineAxis axis = Axis(ruler, windowStart, windowEnd);
        painter.RulerTicks(axis, ruler, TempoTimelineTicks.For(axis, playhead.Tempo), RulerLook);

        DrawCurveSpans(ruler, frame.Selection, windowStart, windowEnd);
        foreach ((Entity entity, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _surface.Entities, ruler, windowStart, windowEnd, timeline.Zoom))
        {
            _surface.Canvas.DrawRect(
                _surface.Target,
                bounds,
                PlayfieldColors.ColorFor(entity.GetComponent<Note>()));

            if (frame.Selection.Contains(entity))
            {
                _surface.Canvas.DrawBorder(
                    _surface.Target,
                    new Rect2(
                        bounds.Position - new Vector2(1.5f, 1.5f),
                        bounds.Size + new Vector2(3, 3)),
                    PlayfieldColors.SelectedNoteColor,
                    1.5f);
            }
        }

        painter.Playhead(axis, Math.Clamp(playhead.TimeMs, windowStart, windowEnd), ruler.Position.Y, ruler.End.Y, Colors.White);
    }

    /// <summary>
    /// Each route note as a band from its head to its end, along the ruler's
    /// foot, with a tick at each of its turns.
    /// </summary>
    /// <summary>A hold's band at the ruler's foot, from its head to its tail, in its key's colour.</summary>
    private void DrawHoldSpan(Rect2 ruler, TimeWindow window, float top, float height, Note note, bool selected)
    {
        if (note.EndMs < window.Start || note.TimeMs > window.End)
            return;

        float from = window.XForTime(ruler, Math.Max(note.TimeMs, window.Start));
        float to = window.XForTime(ruler, Math.Min(note.EndMs, window.End));
        var band = new Rect2(from, top, Math.Max(2f, to - from), height);
        Color fill = PlayfieldColors.ColorFor(note);
        _surface.Canvas.DrawRect(_surface.Target, band, new Color(fill.R, fill.G, fill.B, 0.55f));
        if (selected)
            _surface.Canvas.DrawBorder(_surface.Target, band.Grow(1.5f), PlayfieldColors.SelectedNoteColor, 1.5f);
    }

    private void DrawCurveSpans(Rect2 ruler, NoteSelection selection, long windowStart, long windowEnd)
    {
        var window = new TimeWindow(windowStart, windowEnd);
        const float height = 8f;
        float top = ruler.End.Y - height - 4f;
        foreach (var (notes, chunk) in _surface.Entities.Cached<Note>().Chunks)
        {
            for (int index = 0; index < notes.Length; index++)
            {
                Note curve = notes.Span[index];
                if (curve.IsHold)
                {
                    DrawHoldSpan(ruler, window, top, height, curve, selection.Contains(chunk.EntityAt(index)));
                    continue;
                }

                if (curve.Route is not { } route)
                    continue;
                Entity entity = chunk.EntityAt(index);
                RoutePath path = RoutePath.Of(curve);
                if (path.EndMs < windowStart || path.StartMs > windowEnd)
                    continue;

                float from = window.XForTime(ruler, Math.Max(path.StartMs, windowStart));
                float to = window.XForTime(ruler, Math.Min(path.EndMs, windowEnd));
                var band = new Rect2(from, top, Math.Max(2f, to - from), height);
                Color fill = PlayfieldColors.CurveColor;
                _surface.Canvas.DrawRect(_surface.Target, band, new Color(fill.R, fill.G, fill.B, 0.55f));
                for (int i = 1; i <= route.Turns; i++)
                {
                    long at = path.TimeAt(i);
                    if (at < windowStart || at > windowEnd)
                        continue;
                    float x = window.XForTime(ruler, at);
                    _surface.Canvas.DrawLine(_surface.Target, new Vector2(x, top - 3f), new Vector2(x, top + height + 3f), fill, 2f);
                }

                if (selection.Contains(entity))
                    _surface.Canvas.DrawBorder(_surface.Target, band.Grow(1.5f), PlayfieldColors.SelectedNoteColor, 1.5f);
            }
        }
    }

    /// <summary>The ruler's snap ticks: beats tallest, then halves, quarters and the finer lines.</summary>
    private static readonly TimelineRulerLook RulerLook = new(
        new(22f, MainSnapColor, 2f),
        new(15f, HalfSnapColor, 2f),
        new(10f, QuarterSnapColor),
        new(5f, PlayfieldColors.GridColor));

    private static TimelineAxis Axis(Rect2 ruler, long windowStart, long windowEnd) =>
        new(ruler, windowStart, windowEnd);

    /// <summary>A faint band per beat, brighter on the first beat of each bar (bars from the tempo map).</summary>
    private void DrawBeatBands(Rect2 ruler, Playhead playhead, long windowStart, long windowEnd)
    {
        TempoMap tempo = playhead.Tempo;
        double firstBeat = Math.Floor(tempo.BeatAt(windowStart));
        foreach (TempoTick tick in tempo.Ticks(tempo.TimeAt(firstBeat), windowEnd, 1))
        {
            double start = tick.TimeMs;
            double end = tempo.TimeAt(tick.Beat + 1);
            if (end < windowStart || start > windowEnd)
                continue;

            float x0 = Axis(ruler, windowStart, windowEnd).ClampedX((long)Math.Max(start, windowStart));
            float x1 = Axis(ruler, windowStart, windowEnd).ClampedX((long)Math.Min(end, windowEnd));
            bool accent = tick.OnBar;
            _surface.Canvas.DrawRect(
                _surface.Target,
                new Rect2(x0, ruler.Position.Y + 1, Math.Max(1f, x1 - x0 - 1), ruler.Size.Y - 2),
                new Color(1f, 1f, 1f, accent ? 0.028f : 0.012f));
        }
    }

    /// <summary>
    /// A marker at the offset and at every BPM change, tagged with the tempo
    /// (and the bar length when it isn't four). Read-only here: songs are
    /// timed on the song timing screen.
    /// </summary>
    private void DrawBpmMarker(Rect2 ruler, Playhead playhead, long windowStart, long windowEnd)
    {
        foreach (TempoPoint point in playhead.Tempo.Points)
        {
            if (point.TimeMs < windowStart || point.TimeMs > windowEnd)
                continue;

            float x = Axis(ruler, windowStart, windowEnd).ClampedX((long)Math.Round(point.TimeMs));
            _surface.Canvas.DrawRect(
                _surface.Target,
                new Rect2(x, ruler.Position.Y, 2, ruler.Size.Y),
                PlayfieldColors.SelectedNoteColor);

            string label = point.BeatsPerBar == 4
                ? $"{TempoText.Bpm(point.Bpm)} BPM"
                : $"{TempoText.Bpm(point.Bpm)} BPM {point.BeatsPerBar}/4";
            DrawMarkerTag(ruler, x + 2, label, PlayfieldColors.SelectedNoteColor, rightOfLine: true);
        }
    }

    /// <summary>
    /// A horizontal tag at the top of the ruler beside a marker line, on a
    /// solid backing so the waveform and ticks never show through its text.
    /// </summary>
    private void DrawMarkerTag(Rect2 ruler, float lineX, string label, Color color, bool rightOfLine)
    {
        float width = label.Length * MarkerTagCharWidth + MarkerTagPadding * 2;
        float left = rightOfLine ? lineX + MarkerTagGap : lineX - MarkerTagGap - width;
        left = Math.Clamp(left, ruler.Position.X + 2, ruler.End.X - width - 2);

        var tag = new Rect2(left, ruler.Position.Y + 3, width, MarkerTagHeight);
        _surface.Canvas.DrawRect(_surface.Target, tag, MarkerTagBackground);
        _surface.Canvas.DrawBorder(_surface.Target, tag, Palette.WithAlpha(color, 0.55f), 1);
        _surface.Canvas.DrawTextWeighted(
            _surface.Target,
            tag,
            label,
            color,
            UiType.Caption,
            TextAlign.Center,
            bold: true);
    }
}
