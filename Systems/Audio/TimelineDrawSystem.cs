using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Levels;

namespace Writer.Systems.Audio;

/// <summary>Draws the track timeline, playhead, and drift history from TrackProgress.</summary>
public sealed class TimelineDrawSystem : QuerySystem<TrackProgress>
{
    private const float PanelX = 60;
    private const float PanelY = 60;
    private const float PanelWidth = 1160;
    private const float PanelHeight = 520;
    private const float BarX = 100;
    private const float BarY = 430;
    private const float BarWidth = 1080;
    private const float BarHeight = 10;
    private const float GraphTop = 220;
    private const float GraphBottom = 390;
    private const long DriftWindowMs = 30_000;
    private const long DriftCheckIntervalMs = 100;

    private static readonly Color PanelColor = new(0.02f, 0.03f, 0.06f, 0.9f);
    private static readonly Color PanelBorder = new(0.45f, 0.6f, 0.85f, 0.8f);
    private static readonly Color TitleColor = new(1f, 0.85f, 0.3f);
    private static readonly Color TextColor = new(0.82f, 0.88f, 1f);
    private static readonly Color TickColor = new(0.4f, 0.5f, 0.65f);
    private static readonly Color BarBackground = new(0.1f, 0.13f, 0.2f);
    private static readonly Color BarFill = new(0.25f, 0.8f, 0.5f);
    private static readonly Color PlayheadColor = Colors.Yellow;
    private static readonly Color DriftLineColor = new(1f, 0.25f, 0.2f, 0.95f);
    private static readonly Color DriftAverageColor = new(0.3f, 0.9f, 1f, 0.95f);
    private static readonly Color DriftZeroColor = new(0.35f, 0.42f, 0.55f, 0.9f);

    private readonly IRectCanvas _canvas;
    private readonly Rid _timeline;
    private readonly List<DriftSample> _drift = new();

    private long _lastTrackMs = -1;
    private long _lastSampleAudioMs = -1;
    private long _lastSampleTrackMs = -DriftCheckIntervalMs;
    private long _driftSum;
    private int _driftCount;

    public TimelineDrawSystem(IRectCanvas canvas, Rid timeline)
    {
        _canvas = canvas;
        _timeline = timeline;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_timeline);

        bool hasTrack = false;

        Query.ForEachEntity((ref progress, _) =>
        {
            hasTrack = true;
            AddDriftSample(progress);
            DrawTimeline(progress);
        });

        if (hasTrack)
            return;

        _drift.Clear();
        _driftSum = 0;
        _driftCount = 0;
        _lastTrackMs = -1;
        _lastSampleAudioMs = -1;
        _lastSampleTrackMs = -DriftCheckIntervalMs;
    }

    private void AddDriftSample(TrackProgress progress)
    {
        if (!progress.Started)
            return;

        if (progress.NowMs < _lastTrackMs)
        {
            _drift.Clear();
            _driftSum = 0;
            _driftCount = 0;
            _lastSampleAudioMs = -1;
            _lastSampleTrackMs = -DriftCheckIntervalMs;
        }

        // Check on a fixed cadence instead of every render frame. The audio
        // cursor is held between mixer chunks, so per-frame sampling makes a
        // false sawtooth even when the real offset is stable.
        if (progress.NowMs - _lastSampleTrackMs < DriftCheckIntervalMs)
            return;

        if (progress.AudioMs == _lastSampleAudioMs)
            return;

        _lastSampleAudioMs = progress.AudioMs;
        _lastSampleTrackMs = progress.NowMs;
        _lastTrackMs = progress.NowMs;

        long drift = progress.NowMs - progress.AudioMs;
        _driftSum += drift;
        _driftCount++;

        _drift.Add(new DriftSample(
            progress.NowMs,
            drift,
            _driftSum / (double)_driftCount));

        if (_drift.Count > 4096)
            _drift.RemoveAt(0);
    }

    private void DrawTimeline(TrackProgress progress)
    {
        var panel = new Rect2(PanelX, PanelY, PanelWidth, PanelHeight);
        _canvas.DrawRect(_timeline, panel, PanelColor);
        DrawBorder(panel, PanelBorder, 1);

        float x = PanelX + 30;
        float y = PanelY + 24;

        DrawText("MINIAUDIO / TIMER_PLAY TRACK TEST", x, y, TitleColor);
        y += 24;

        DrawText(
            $"Track: {Path.GetFileName(progress.AudioPath)}    BPM {progress.Bpm:0}    " +
            $"Speed {progress.SpeedPercent}%",
            x,
            y,
            TextColor);
        y += 22;

        DrawText(
            $"Timer {FormatMs(progress.NowMs)} / {FormatMs(progress.DurationMs)}    " +
            $"Audio {FormatMs(progress.AudioMs)}    Drift {progress.NowMs - progress.AudioMs:+0;-0;0} ms",
            x,
            y,
            TextColor);
        y += 22;

        DrawText("Timeline is driven by TIMER_PLAY, not the audio clock.", x, y, TickColor);
        y += 22;
        DrawText("Press L to load another song (MP3/WAV/OGG/FLAC).", x, y, TickColor);

        DrawDriftGraph(progress);

        float duration = Math.Max(1, progress.DurationMs);
        float normalized = Math.Clamp(progress.NowMs / duration, 0f, 1f);

        DrawTimelineTicks(progress.Bpm, progress.DurationMs);

        _canvas.DrawRect(
            _timeline,
            new Rect2(BarX, BarY, BarWidth, BarHeight),
            BarBackground);

        _canvas.DrawRect(
            _timeline,
            new Rect2(BarX, BarY, BarWidth * normalized, BarHeight),
            BarFill);

        DrawBorder(
            new Rect2(BarX, BarY, BarWidth, BarHeight),
            PanelBorder,
            1);

        float playheadX = BarX + BarWidth * normalized;
        _canvas.DrawRect(
            _timeline,
            new Rect2(playheadX - 2, BarY - 28, 4, BarHeight + 56),
            PlayheadColor);

        DrawText(
            $"{(int)(normalized * 100)}%",
            playheadX - 18,
            BarY + 38,
            PlayheadColor);
    }

    private void DrawDriftGraph(TrackProgress progress)
    {
        var graph = new Rect2(BarX, GraphTop, BarWidth, GraphBottom - GraphTop);
        _canvas.DrawRect(_timeline, graph, BarBackground);
        DrawBorder(graph, PanelBorder, 1);

        float centerY = (GraphTop + GraphBottom) / 2;
        float halfHeight = (GraphBottom - GraphTop) / 2 - 8;
        long windowEnd = Math.Max(DriftWindowMs, progress.NowMs);
        long windowStart = Math.Max(0, windowEnd - DriftWindowMs);
        float windowMs = Math.Max(1, windowEnd - windowStart);
        float maxAbs = 10;

        foreach (DriftSample sample in _drift)
        {
            if (sample.TimeMs >= windowStart)
                maxAbs = Math.Max(maxAbs, Math.Abs(sample.DriftMs));
        }

        float scale = Math.Max(20, MathF.Ceiling(maxAbs * 1.15f / 10f) * 10f);

        DrawText(
            $"Drift over last {DriftWindowMs / 1000}s (ms) - checked every " +
            $"{DriftCheckIntervalMs}ms at audio cursor update",
            BarX,
            GraphTop - 24,
            TitleColor);
        DrawText($"+{scale:0}", BarX + 8, GraphTop + 4, TickColor);
        DrawText("0", BarX + 8, centerY - 20, DriftZeroColor);
        DrawText($"-{scale:0}", BarX + 8, GraphBottom - 24, TickColor);

        double average = _driftCount > 0 ? _driftSum / (double)_driftCount : 0;
        DrawText(
            $"avg {average:+0;-0;0} ms",
            BarX + BarWidth - 130,
            GraphTop + 4,
            DriftAverageColor);

        _canvas.DrawLine(
            _timeline,
            new Vector2(BarX, centerY),
            new Vector2(BarX + BarWidth, centerY),
            DriftZeroColor,
            1);

        Vector2? previous = null;

        foreach (DriftSample sample in _drift)
        {
            if (sample.TimeMs < windowStart)
                continue;

            float sampleX = BarX +
                            (float)((sample.TimeMs - windowStart) / windowMs) * BarWidth;
            float sampleY = centerY -
                            Math.Clamp(sample.DriftMs / scale, -1f, 1f) * halfHeight;

            var current = new Vector2(sampleX, sampleY);
            if (previous is { } previousPoint)
            {
                var corner = new Vector2(current.X, previousPoint.Y);
                _canvas.DrawLine(_timeline, previousPoint, corner, DriftLineColor, 2);
                _canvas.DrawLine(_timeline, corner, current, DriftLineColor, 2);
            }

            previous = current;
        }

        Vector2? averagePrevious = null;

        foreach (DriftSample sample in _drift)
        {
            if (sample.TimeMs < windowStart)
                continue;

            float sampleX = BarX +
                            (float)((sample.TimeMs - windowStart) / windowMs) * BarWidth;
            float sampleY = centerY -
                            Math.Clamp((float)sample.AverageMs / scale, -1f, 1f) * halfHeight;

            var current = new Vector2(sampleX, sampleY);
            if (averagePrevious is { } previousAverage)
                _canvas.DrawLine(_timeline, previousAverage, current, DriftAverageColor, 2);

            averagePrevious = current;
        }

        if (previous is { } latest)
        {
            _canvas.DrawRect(
                _timeline,
                new Rect2(latest.X - 2, latest.Y - 2, 4, 4),
                DriftLineColor);

            DrawText(
                $"{progress.NowMs - progress.AudioMs:+0;-0;0} ms",
                Math.Min(latest.X + 6, BarX + BarWidth - 70),
                Math.Max(GraphTop + 4, latest.Y - 20),
                DriftLineColor);
        }
    }

    private void DrawTimelineTicks(float bpm, long durationMs)
    {
        if (bpm <= 0 || durationMs <= 0)
            return;

        double beatMs = TimingGrid.BeatMs((int)bpm);
        double beatCount = durationMs / beatMs;
        int step = Math.Max(1, (int)Math.Ceiling(beatCount / 120.0));
        if (step > 1)
            step = (step + 3) / 4 * 4;

        int beat = 0;

        for (double time = 0; time <= durationMs; time += beatMs)
        {
            if (beat % step != 0)
            {
                beat++;
                continue;
            }

            float x = BarX + (float)(time / durationMs) * BarWidth;
            bool measure = beat % 4 == 0;
            float height = measure ? 24 : 12;

            _canvas.DrawRect(
                _timeline,
                new Rect2(x, BarY - height, measure ? 2 : 1, height),
                TickColor);

            beat++;
        }
    }

    private void DrawText(string text, float x, float y, Color color) =>
        _canvas.DrawTextLeft(
            _timeline,
            new Rect2(x, y, PanelWidth - 60, 20),
            text,
            color);

    private void DrawBorder(Rect2 bounds, Color color, float width)
    {
        _canvas.DrawRect(_timeline,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_timeline,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_timeline,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        _canvas.DrawRect(_timeline,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
    }

    private static string FormatMs(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }

    private readonly record struct DriftSample(long TimeMs, long DriftMs, double AverageMs);
}
