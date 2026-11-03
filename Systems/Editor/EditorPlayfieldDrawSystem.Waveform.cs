using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;
using Xanadu.Theme;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>The song on the timeline: its bass, middle and highs overlaid around a centre line.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    private const float WaveformColumnWidth = 2f;
    private const float WaveformMargin = 4f;

    /// <summary>Bass is drawn full height; the higher bands shorter, so all three read at once.</summary>
    private const float WaveformMidScale = 0.62f;
    private const float WaveformHighScale = 0.34f;

    private void DrawWaveform(
        Rect2 ruler,
        WaveformPreview preview,
        long windowStart,
        long windowEnd)
    {
        if (preview.Data is not { DurationMs: > 0 } data)
            return;

        float centerY = ruler.Position.Y + ruler.Size.Y / 2;
        float maxHeight = ruler.Size.Y / 2f - WaveformMargin;
        double msPerColumn = (windowEnd - windowStart) * WaveformColumnWidth / (double)ruler.Size.X;

        // Columns sit on fixed multiples of time, so the shape holds still as the window scrolls past it.
        long firstColumn = (long)Math.Floor(windowStart / msPerColumn);
        long lastColumn = (long)Math.Ceiling(windowEnd / msPerColumn);
        var window = new TimeWindow(windowStart, windowEnd);
        Span<(float Level, Color Color)> bands = stackalloc (float, Color)[3];

        for (long column = firstColumn; column < lastColumn; column++)
        {
            double start = column * msPerColumn;
            double end = start + msPerColumn;
            float x0 = window.XForTime(ruler, start);
            float x1 = window.XForTime(ruler, end);
            if (x1 <= x0)
                continue;

            // Drawn ahead of its clock, so a hit shows where a chart would place it.
            double audioStart = start + ChartTiming.AudioLagMs;
            double audioEnd = end + ChartTiming.AudioLagMs;
            bands[0] = (data.Average(WaveformBand.Low, audioStart, audioEnd), Palette.WaveLow);
            bands[1] = (data.Average(WaveformBand.Mid, audioStart, audioEnd) * WaveformMidScale, Palette.WaveMid);
            bands[2] = (data.Average(WaveformBand.High, audioStart, audioEnd) * WaveformHighScale, Palette.WaveHigh);

            // Tallest first, so a shorter band is never hidden behind a taller one.
            if (bands[0].Level < bands[1].Level)
                (bands[0], bands[1]) = (bands[1], bands[0]);
            if (bands[1].Level < bands[2].Level)
                (bands[1], bands[2]) = (bands[2], bands[1]);
            if (bands[0].Level < bands[1].Level)
                (bands[0], bands[1]) = (bands[1], bands[0]);

            foreach ((float level, Color color) in bands)
            {
                float half = level * maxHeight;
                if (half < 0.5f)
                    continue;

                _canvas.DrawRect(
                    _editor,
                    new Rect2(x0, centerY - half, x1 - x0, half * 2f),
                    color);
            }
        }
    }
}
