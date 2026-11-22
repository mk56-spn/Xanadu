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
/// The editor's timeline: a shaded panel with its beat bands and bar numbers,
/// the song's waveform, the BPM and start markers, the tick grid, every note as
/// a dot in its colour (a dash as a bar, an entrance as a diamond, an orb as a
/// gold span to its tail) and the playhead, a line with a head and a glow.
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
        // A shade along the top, so the panel reads as sunk into the footer.
        _surface.Canvas.DrawGradientRect(_surface.Target, new Rect2(ruler.Position, new Vector2(ruler.Size.X, ruler.Size.Y * 0.35f)),
            new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), horizontal: false);
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
        DrawBarNumbers(ruler, playhead, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        var painter = new TimelinePainter(_surface.Canvas, _surface.Target);
        TimelineAxis axis = Axis(ruler, windowStart, windowEnd);
        painter.RulerTicks(axis, ruler, TempoTimelineTicks.For(axis, playhead.Tempo), RulerLook);

        foreach ((Entity entity, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _surface.Entities, ruler, windowStart, windowEnd, timeline.Zoom))
            DrawNoteSlot(entity.GetComponent<Note>(), bounds, axis, ruler, frame.Selection.Contains(entity));

        DrawPlayhead(painter, axis, ruler, Math.Clamp((long)Math.Round(playhead.ShownMs), windowStart, windowEnd));
    }

    /// <summary>
    /// A note on the timeline, in its colour on a dark rim: a dot for a tap, a
    /// bar for a dash, a diamond for the entrance, and for an orb a dot with a
    /// gold span reaching to its tail. A selected one gets the selection ring.
    /// </summary>
    private void DrawNoteSlot(in Note note, Rect2 bounds, TimelineAxis axis, Rect2 ruler, bool selected)
    {
        IRectCanvas canvas = _surface.Canvas;
        Handle target = _surface.Target;
        Vector2 centre = bounds.GetCenter();
        float radius = bounds.Size.X / 2f;
        Color colour = PlayfieldColors.ColorFor(note);
        Color rim = new(0f, 0f, 0f, 0.55f);

        if (note.IsOrb && note.EndMs > note.TimeMs)
        {
            float end = axis.ClampedX(Math.Min(note.EndMs, axis.EndMs));
            var span = new Rect2(centre.X, centre.Y - radius * 0.45f, Math.Max(0f, end - centre.X), radius * 0.9f);
            canvas.DrawRect(target, span, Palette.WithAlpha(colour, 0.45f));
            if (note.EndMs <= axis.EndMs)
                canvas.DrawCircle(target, new Vector2(end, centre.Y), radius * 0.6f, colour);
        }

        if (note.IsEntrance)
        {
            float r = radius * 1.25f;
            Span<Vector2> rimShape = [centre + new Vector2(0f, -r - 1.5f), centre + new Vector2(r + 1.5f, 0f), centre + new Vector2(0f, r + 1.5f), centre + new Vector2(-r - 1.5f, 0f)];
            Span<Vector2> shape = [centre + new Vector2(0f, -r), centre + new Vector2(r, 0f), centre + new Vector2(0f, r), centre + new Vector2(-r, 0f)];
            canvas.DrawPolygon(target, rimShape, rim);
            canvas.DrawPolygon(target, shape, new Color(0.75f, 0.88f, 1f));
        }
        else if (note.IsDash)
        {
            var bar = new Rect2(centre - new Vector2(radius * 1.6f, radius * 0.45f), new Vector2(radius * 3.2f, radius * 0.9f));
            canvas.DrawRect(target, bar.Grow(1.5f), rim);
            canvas.DrawRect(target, bar, colour);
        }
        else
        {
            canvas.DrawCircle(target, centre, radius + 1.5f, rim);
            canvas.DrawCircle(target, centre, radius, colour);
            canvas.DrawCircle(target, centre - new Vector2(radius * 0.25f, radius * 0.3f), radius * 0.35f, new Color(1f, 1f, 1f, 0.35f));
        }

        if (selected)
            canvas.DrawCircleOutline(target, centre, radius + 4f, PlayfieldColors.SelectedNoteColor, 1.5f);
    }

    /// <summary>The playhead: a soft glow, the line, and a head at the top.</summary>
    private void DrawPlayhead(TimelinePainter painter, TimelineAxis axis, Rect2 ruler, long timeMs)
    {
        float x = axis.ClampedX(timeMs);
        _surface.Canvas.DrawRect(_surface.Target, new Rect2(x - 4f, ruler.Position.Y, 8f, ruler.Size.Y), new Color(1f, 1f, 1f, 0.08f));
        painter.Playhead(axis, timeMs, ruler.Position.Y, ruler.End.Y, Colors.White);
        Span<Vector2> head = [new Vector2(x - 6f, ruler.Position.Y), new Vector2(x + 6f, ruler.Position.Y), new Vector2(x, ruler.Position.Y + 7f)];
        _surface.Canvas.DrawPolygon(_surface.Target, head, Colors.White);
    }

    /// <summary>
    /// The bar number at the top of each bar line, counted from the song's
    /// start by the tempo map, so a change of metre keeps the count right.
    /// Only drawn when bars are wide enough to read them.
    /// </summary>
    private void DrawBarNumbers(Rect2 ruler, Playhead playhead, long windowStart, long windowEnd)
    {
        TempoMap tempo = playhead.Tempo;
        TimelineAxis axis = Axis(ruler, windowStart, windowEnd);
        int bar = 0;
        float lastX = float.NegativeInfinity;
        foreach (TempoTick tick in tempo.Ticks(tempo.TimeAt(0), windowEnd, 1))
        {
            if (!tick.OnBar)
                continue;
            bar++;
            if (tick.TimeMs < windowStart)
                continue;

            float x = axis.ClampedX((long)tick.TimeMs);
            if (x - lastX < 28f)
                continue;
            lastX = x;
            _surface.Canvas.DrawLine(_surface.Target, new Vector2(x, ruler.Position.Y + 1f), new Vector2(x, ruler.End.Y - 1f), new Color(1f, 1f, 1f, 0.12f), 1f);
            _surface.Canvas.DrawTextWeighted(_surface.Target, new Rect2(x + 3f, ruler.Position.Y + 2f, 40f, 14f), bar.ToString(),
                Palette.WithAlpha(Palette.TextMuted, 0.8f), UiType.Caption, TextAlign.Left, bold: false);
        }
    }

    /// <summary>The ruler's snap ticks: beats tallest, then halves, quarters and the finer lines.</summary>
    private static readonly TimelineRulerLook RulerLook = new(
        new(16f, MainSnapColor, 2f),
        new(11f, HalfSnapColor, 2f),
        new(7f, QuarterSnapColor),
        new(4f, PlayfieldColors.GridColor));

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
