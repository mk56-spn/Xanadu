using System;
using Godot;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Shared editor header, footer, transport, playfield, and timeline geometry.</summary>
public static class EditorLayout
{
    public const float Margin = 16;
    public const float HeaderHeight = 72;
    public const float FooterHeight = 88;
    public const float TransportHeight = 42;
    public const float Gap = 10;

    public const float TimelineHeight = 42;
    public const float TimelineWidthRatio = 0.66f;
    public const float MaxTimelineWidth = 1200;

    public static Rect2 HeaderRect(PlayfieldView view) =>
        new(0, 0, view.ScreenSize.X, HeaderHeight);

    public static Rect2 FooterRect(PlayfieldView view) =>
        new(
            0,
            view.ScreenSize.Y - FooterHeight,
            view.ScreenSize.X,
            FooterHeight);

    public static Rect2 TransportRect(PlayfieldView view) =>
        new(
            0,
            view.ScreenSize.Y - FooterHeight - Gap - TransportHeight,
            view.ScreenSize.X,
            TransportHeight);

    /// <summary>Timeline centered inside the footer.</summary>
    public static Rect2 RulerRect(PlayfieldView view)
    {
        Rect2 footer = FooterRect(view);
        float width = Mathf.Min(
            view.ScreenSize.X * TimelineWidthRatio,
            MaxTimelineWidth);

        return new Rect2(
            (view.ScreenSize.X - width) / 2,
            footer.Position.Y + (FooterHeight - TimelineHeight) / 2,
            width,
            TimelineHeight);
    }

    /// <summary>Visible time window for a timeline zoom level, following the playhead.</summary>
    public static (long Start, long End) TimelineWindow(
        Playhead playhead,
        float zoom)
    {
        long duration = Math.Max(1, playhead.DurationMs);
        long window = Math.Max(1, (long)(duration / Math.Max(1f, zoom)));
        long start = Math.Clamp(
            playhead.TimeMs - window / 2,
            0,
            Math.Max(0, duration - window));

        return (start, Math.Min(duration, start + window));
    }

    public static Rect2 PlayfieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));
}
