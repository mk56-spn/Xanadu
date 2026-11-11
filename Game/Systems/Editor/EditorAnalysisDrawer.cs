using System;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;
using Xanadu.Systems.Playfields;
using Xanadu.Timeline;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The strip just above the timeline: bars for notes per second, a line for how
/// far the player has to travel, and a red tick on every unreachable note.
/// </summary>
public sealed class EditorAnalysisDrawer(EditorDrawSurface surface)
{
    private static readonly Color DensityBackground = new(0.043f, 0.067f, 0.106f, 0.9f);
    private static readonly Color CalmColor = Palette.PrimarySoft;
    private static readonly Color BusyColor = Palette.Danger;
    private static readonly Color TravelColor = new(1f, 1f, 1f, 0.7f);
    private static readonly Color IssueColor = Palette.DangerBright;

    // A quiet chart should not look like a wall: scale against at least this much.
    private const float MinNotesScale = 8f;
    private const float MinTravelScale = 800f;
    private const float BusyNotesPerSecond = 12f;

    public void Draw(EditorAnalysis analysis, Playhead playhead, Rect2 strip, long windowStart, long windowEnd)
    {
        if (analysis.Density is null || playhead.DurationMs <= 0)
            return;

        DensityProfile density = analysis.Density;
        var window = new TimeWindow(windowStart, windowEnd);

        surface.Canvas.DrawRect(surface.Target, strip, DensityBackground);

        float notesScale = Math.Max(density.PeakNotes, MinNotesScale);
        float travelScale = Math.Max(density.PeakTravel, MinTravelScale);
        var travelLine = new System.Collections.Generic.List<Vector2>();

        int first = (int)Math.Max(0, windowStart / density.BucketMs - 1);
        int last = (int)Math.Min(density.BucketCount - 1, windowEnd / density.BucketMs + 1);
        for (int i = first; i <= last; i++)
        {
            float left = Mathf.Clamp(
                window.XForTime(strip, i * (long)density.BucketMs),
                strip.Position.X,
                strip.End.X);
            float right = Mathf.Clamp(
                window.XForTime(strip, (i + 1) * (long)density.BucketMs),
                strip.Position.X,
                strip.End.X);
            if (right <= left)
                continue;

            float notes = density.NotesPerSecond[i];
            float height = strip.Size.Y * Math.Min(1f, notes / notesScale);
            Color color = CalmColor.Lerp(BusyColor, Math.Min(1f, notes / BusyNotesPerSecond));
            surface.Canvas.DrawRect(
                surface.Target,
                new Rect2(left, strip.End.Y - height, right - left, height),
                new Color(color, 0.85f));

            travelLine.Add(new Vector2(
                (left + right) / 2,
                strip.End.Y - strip.Size.Y * Math.Min(1f, density.TravelPixelsPerSecond[i] / travelScale)));
        }

        if (travelLine.Count > 1)
            surface.Canvas.DrawPolyline(surface.Target, travelLine.ToArray(), TravelColor, 1.5f);

        DrawIssueTicks(analysis, strip, window, windowStart, windowEnd);
        DrawLabels(analysis, strip, density);
    }

    private void DrawIssueTicks(
        EditorAnalysis analysis,
        Rect2 strip,
        TimeWindow window,
        long windowStart,
        long windowEnd)
    {
        if (analysis.Issues is null)
            return;

        foreach (PlayabilityIssue issue in analysis.Issues)
        {
            if (issue.TimeMs < windowStart || issue.TimeMs > windowEnd)
                continue;

            float x = window.XForTime(strip, issue.TimeMs);
            surface.Canvas.DrawRect(
                surface.Target,
                new Rect2(x - 1, strip.Position.Y, 2, strip.Size.Y),
                IssueColor);
            surface.Canvas.DrawPolygon(
                surface.Target,
                [new(x - 4, strip.Position.Y), new(x + 4, strip.Position.Y), new(x, strip.Position.Y + 6)],
                IssueColor);
        }
    }

    private void DrawLabels(EditorAnalysis analysis, Rect2 strip, DensityProfile density)
    {
        int problems = analysis.Issues?.Length ?? 0;
        if (problems > 0)
        {
            surface.Canvas.DrawTextStyledLeft(
                surface.Target,
                strip,
                problems == 1 ? "1 unreachable note" : $"{problems} unreachable notes",
                IssueColor,
                11);
        }

        surface.Canvas.DrawTextStyledRight(
            surface.Target,
            strip,
            $"peak {density.PeakNotes:0.#} notes/s",
            PlayfieldRenderer.DimTextColor,
            11);
    }
}
