using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

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

    /// <summary>The least room in pixels between two drawn beat ticks.</summary>
    private const float MinBeatSpacing = 16f;

    private const float MarkerTagHeight = 18f;
    private const float MarkerTagPadding = 6f;
    private const float MarkerTagGap = 3f;

    /// <summary>A rough advance for bold caption text; the canvas cannot measure.</summary>
    private const float MarkerTagCharWidth = 7.5f;

    private static readonly Color MarkerTagBackground = Palette.WithAlpha(Palette.SurfaceDeep, 0.94f);

    private readonly EditorDrawSurface _surface;
    private readonly EditorAnalysisDrawer _analysis;
    private readonly EditorWaveformDrawer _waveform;

    public EditorTimelineDrawer(EditorDrawSurface surface)
    {
        _surface = surface;
        _analysis = new EditorAnalysisDrawer(surface);
        _waveform = new EditorWaveformDrawer(surface);
    }

    public void Draw(EditorFrame frame)
    {
        PlayfieldView view = frame.View;
        Playhead playhead = frame.Playhead;
        TimelineView timeline = frame.Timeline;
        Rect2 ruler = EditorLayout.RulerRect(view);
        _surface.Canvas.DrawRect(_surface.Target, ruler, Palette.SurfaceDeep);
        _surface.Canvas.DrawBorder(_surface.Target, ruler, PlayfieldRenderer.BorderColor, 1);

        if (playhead.DurationMs <= 0)
        {
            _surface.Canvas.DrawText(
                _surface.Target,
                ruler,
                "Load a song to enable the timeline",
                PlayfieldRenderer.DimTextColor);
            return;
        }

        (long windowStart, long windowEnd) = EditorLayout.TimelineWindow(playhead, timeline);

        _analysis.Draw(
            frame.Analysis,
            playhead,
            EditorLayout.AnalysisRect(view),
            windowStart,
            windowEnd);
        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        _waveform.Draw(ruler, frame.Preview, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        DrawStartMarkers(ruler, frame.Selection, windowStart, windowEnd);
        int subdivisions = Math.Max(1, playhead.SnapDivision);
        TempoMap tempo = playhead.Tempo;
        double firstBeat = tempo.BeatAt(windowStart);
        double lastBeat = tempo.BeatAt(windowEnd);

        // The window's average tick, for thinning; the ticks themselves follow the tempo map.
        double tickMs = (windowEnd - windowStart) / Math.Max(1e-6, (lastBeat - firstBeat) * subdivisions);

        float pixelsPerTick = ruler.Size.X * (float)tickMs / Math.Max(1f, windowEnd - windowStart);
        int skip = 1;
        while (pixelsPerTick / skip < 3f)
            skip *= 2;

        // Zoomed out, the ticks must thin out by importance or they become a solid wall.
        float pixelsPerBeat = pixelsPerTick * subdivisions;
        int beatSkip = 1;
        while (pixelsPerBeat * beatSkip < MinBeatSpacing)
            beatSkip *= 2;

        int firstTick = (int)Math.Floor(firstBeat * subdivisions);
        int lastTick = (int)Math.Ceiling(lastBeat * subdivisions);

        for (int tick = firstTick; tick <= lastTick; tick++)
        {
            int sub = tick % subdivisions;
            if (sub < 0)
                sub += subdivisions;

            bool main = sub == 0;
            bool half = sub * 2 == subdivisions;
            bool quarter = sub * 4 == subdivisions ||
                           sub * 4 == subdivisions * 3;

            if (main
                    ? FloorDiv(tick, subdivisions) % beatSkip != 0
                    : !ShowsTick(half, quarter, sub, skip, pixelsPerBeat))
            {
                continue;
            }

            double tickTime = tempo.TimeAt(tick / (double)subdivisions);
            if (tickTime < windowStart - 1 || tickTime > windowEnd + 1)
                continue;

            float x = EditorTimelineGeometry.XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Round(tickTime));
            float height = main ? 22f : half ? 15f : quarter ? 10f : 5f;
            Color color = main
                ? MainSnapColor
                : half
                    ? HalfSnapColor
                    : quarter
                        ? QuarterSnapColor
                        : PlayfieldRenderer.GridColor;

            _surface.Canvas.DrawLine(
                _surface.Target,
                new Vector2(x, ruler.End.Y - height),
                new Vector2(x, ruler.End.Y),
                color,
                main || half ? 2 : 1);
        }

        foreach ((Entity entity, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _surface.Entities, ruler, windowStart, windowEnd, timeline.Zoom))
        {
            _surface.Canvas.DrawRect(
                _surface.Target,
                bounds,
                PlayfieldRenderer.ColorFor(entity.GetComponent<Note>()));

            if (frame.Selection.Contains(entity))
            {
                _surface.Canvas.DrawBorder(
                    _surface.Target,
                    new Rect2(
                        bounds.Position - new Vector2(1.5f, 1.5f),
                        bounds.Size + new Vector2(3, 3)),
                    PlayfieldRenderer.SelectedNoteColor,
                    1.5f);
            }
        }

        float playheadX = EditorTimelineGeometry.XForTime(
            ruler,
            windowStart,
            windowEnd,
            Math.Clamp(playhead.TimeMs, windowStart, windowEnd));

        _surface.Canvas.DrawLine(
            _surface.Target,
            new Vector2(playheadX, ruler.Position.Y),
            new Vector2(playheadX, ruler.End.Y),
            Colors.White,
            2);
        _surface.Canvas.DrawRect(
            _surface.Target,
            new Rect2(playheadX - 5f, ruler.Position.Y - 4f, 10f, 10f),
            Colors.White);
    }

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

            float x0 = EditorTimelineGeometry.XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Max(start, windowStart));
            float x1 = EditorTimelineGeometry.XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Min(end, windowEnd));
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

            float x = EditorTimelineGeometry.XForTime(ruler, windowStart, windowEnd, (long)Math.Round(point.TimeMs));
            _surface.Canvas.DrawRect(
                _surface.Target,
                new Rect2(x, ruler.Position.Y, 2, ruler.Size.Y),
                PlayfieldRenderer.SelectedNoteColor);

            string label = point.BeatsPerBar == 4
                ? $"{TempoText.Bpm(point.Bpm)} BPM"
                : $"{TempoText.Bpm(point.Bpm)} BPM {point.BeatsPerBar}/4";
            DrawMarkerTag(ruler, x + 2, label, PlayfieldRenderer.SelectedNoteColor, rightOfLine: true);
        }
    }

    /// <summary>
    /// Each start point as a full-height line on the ruler with its name
    /// tagged to the left, so it does not cover a BPM label at the same time.
    /// </summary>
    private void DrawStartMarkers(Rect2 ruler, NoteSelection selection, long windowStart, long windowEnd)
    {
        foreach (Entity entity in _surface.Entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            if (start.TimeMs < windowStart || start.TimeMs > windowEnd)
                continue;

            Color color = selection.Contains(entity) ? Colors.White : PlayfieldRenderer.StartPointColor;
            float x = EditorTimelineGeometry.XForTime(ruler, windowStart, windowEnd, start.TimeMs);
            _surface.Canvas.DrawRect(
                _surface.Target,
                new Rect2(x - 1, ruler.Position.Y, 2, ruler.Size.Y),
                color);

            DrawMarkerTag(ruler, x - 1, StartLabel(start), color, rightOfLine: false);
        }
    }

    /// <summary>The start point's name, or "START" when it has none.</summary>
    public static string StartLabel(StartPoint start) =>
        string.IsNullOrWhiteSpace(start.Name) ? "START" : start.Name;

    /// <summary>
    /// A horizontal tag at the top of the ruler beside a marker line, on a
    /// solid backing so the waveform and ticks never show through its text.
    /// </summary>
    private void DrawMarkerTag(Rect2 ruler, float lineX, string label, Color color, bool rightOfLine)
    {
        float width = label.Length * MarkerTagCharWidth + MarkerTagPadding * 2;
        float left = rightOfLine ? lineX + MarkerTagGap : lineX - MarkerTagGap - width;
        left = Mathf.Clamp(left, ruler.Position.X + 2, ruler.End.X - width - 2);

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

    private static int FloorDiv(int value, int divisor) =>
        (int)Math.Floor(value / (double)divisor);

    /// <summary>Whether a tick between beats has room to be drawn at this zoom.</summary>
    private static bool ShowsTick(bool half, bool quarter, int sub, int skip, float pixelsPerBeat) =>
        half ? pixelsPerBeat >= MinBeatSpacing * 2f :
        quarter ? pixelsPerBeat >= MinBeatSpacing * 4f :
        pixelsPerBeat >= MinBeatSpacing * 4f && sub % skip == 0;
}
