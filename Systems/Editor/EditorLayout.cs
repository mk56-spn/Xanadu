using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>Shared editor header, footer, transport, playfield, and timeline geometry.</summary>
public static class EditorLayout
{
    public const float Margin = 16;

    /// <summary>The shell's header. The footer is one panel: the transport row, then the timeline row.</summary>
    public const float HeaderHeight = EditorShellMetrics.HeaderHeight;
    public const float FooterHeight = EditorFooterHeight.Level;
    public const float TransportHeight = 42;

    public const float TimelineHeight = 96;

    /// <summary>The clock left of the timeline.</summary>
    public const float TimeWidth = 248;
    public const float TimeHeight = 36;

    /// <summary>Clear space under the timeline.</summary>
    public const float TimelineBottomPad = 10;

    /// <summary>The lens and the two zoom buttons right of the timeline.</summary>
    public const float ZoomWidth = 106;

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
            view.ScreenSize.Y - FooterHeight,
            view.ScreenSize.X,
            TransportHeight);

    /// <summary>The timeline fills the footer's width between the clock and the zoom controls.</summary>
    public static Rect2 RulerRect(PlayfieldView view)
    {
        float left = Margin + TimeWidth + Margin;
        float right = view.ScreenSize.X - Margin - ZoomWidth;
        return new Rect2(
            left,
            view.ScreenSize.Y - TimelineBottomPad - TimelineHeight,
            Mathf.Max(1, right - left),
            TimelineHeight);
    }

    public const float AnalysisHeight = 18;

    /// <summary>The density strip, between the transport and the timeline.</summary>
    public static Rect2 AnalysisRect(PlayfieldView view)
    {
        Rect2 ruler = RulerRect(view);
        return new Rect2(
            ruler.Position.X,
            ruler.Position.Y - AnalysisHeight - 2,
            ruler.Size.X,
            AnalysisHeight);
    }

    /// <summary>Centre of the lens icon that labels the timeline zoom controls.</summary>
    public static Vector2 ZoomLensCenter(PlayfieldView view)
    {
        Rect2 ruler = RulerRect(view);
        return new Vector2(ruler.End.X + 24, ruler.GetCenter().Y);
    }

    /// <summary>The timeline's visible window: held while scrubbing, pinned by a wheel zoom, or following the playhead.</summary>
    public static (long Start, long End) TimelineWindow(Playhead playhead, TimelineView view)
    {
        TimeWindow window = TimeWindow.Of(view, playhead.TimeMs, playhead.DurationMs);
        return (window.Start, window.End);
    }

    /// <summary>Visible time window for a timeline zoom level, following the playhead.</summary>
    public static (long Start, long End) TimelineWindow(
        Playhead playhead,
        float zoom)
    {
        TimeWindow window = TimeWindow.Following(
            playhead.TimeMs, playhead.DurationMs, zoom);
        return (window.Start, window.End);
    }

    public static Rect2 PlayfieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));
}
