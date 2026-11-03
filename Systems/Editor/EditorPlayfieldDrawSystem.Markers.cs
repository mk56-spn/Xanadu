using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The BPM and start point markers on the timeline: a full-height line and a tag naming it.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    private const float MarkerTagHeight = 18f;
    private const float MarkerTagPadding = 6f;
    private const float MarkerTagGap = 3f;

    /// <summary>A rough advance for bold caption text; the canvas cannot measure.</summary>
    private const float MarkerTagCharWidth = 7.5f;

    private static readonly Color MarkerTagBackground = Palette.WithAlpha(Palette.SurfaceDeep, 0.94f);

    private void DrawBpmMarker(
        Rect2 ruler,
        Playhead playhead,
        long windowStart,
        long windowEnd)
    {
        if (playhead.OffsetMs < windowStart ||
            playhead.OffsetMs > windowEnd)
        {
            return;
        }

        float x = XForTime(
            ruler,
            windowStart,
            windowEnd,
            playhead.OffsetMs);
        _canvas.DrawRect(
            _editor,
            new Rect2(x, ruler.Position.Y, 2, ruler.Size.Y),
            PlayfieldRenderer.SelectedNoteColor);

        DrawMarkerTag(ruler, x + 2, $"{playhead.Bpm} BPM", PlayfieldRenderer.SelectedNoteColor, rightOfLine: true);
    }

    /// <summary>
    /// Each start point as a full-height line on the ruler with its name
    /// tagged to the left, so it does not cover a BPM label at the same time.
    /// </summary>
    private void DrawStartMarkers(
        Rect2 ruler,
        NoteSelection selection,
        long windowStart,
        long windowEnd)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            if (start.TimeMs < windowStart || start.TimeMs > windowEnd)
                continue;

            Color color = selection.Contains(entity)
                ? Colors.White
                : PlayfieldRenderer.StartPointColor;
            float x = XForTime(ruler, windowStart, windowEnd, start.TimeMs);
            _canvas.DrawRect(
                _editor,
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
        _canvas.DrawRect(_editor, tag, MarkerTagBackground);
        _canvas.DrawBorder(_editor, tag, Palette.WithAlpha(color, 0.55f), 1);
        _canvas.DrawTextWeighted(
            _editor,
            tag,
            label,
            color,
            UiType.Caption,
            TextAlign.Center,
            bold: true);
    }
}
