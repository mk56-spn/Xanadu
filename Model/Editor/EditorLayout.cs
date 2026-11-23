using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Editor;

/// <summary>Shared editor header, footer, transport, playfield, and timeline geometry.</summary>
public static class EditorLayout
{
    public const float Margin = 16;

    /// <summary>The shell's header. The footer is one panel: the transport row, then the timeline row.</summary>
    public const float HeaderHeight = EditorShellMetrics.HeaderHeight;
    public const float FooterHeight = EditorFooterHeight.Level;
    /// <summary>The transport row: 38 px buttons with 6 px clear above and below.</summary>
    public const float TransportHeight = 50;

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
            Math.Max(1, right - left),
            TimelineHeight);
    }

    public const float GroupKeysHeight = 26;

    /// <summary>The picked group's key strip, between the transport and the timeline, on the timeline's time axis.</summary>
    public static Rect2 GroupKeysRect(PlayfieldView view)
    {
        Rect2 ruler = RulerRect(view);
        return new Rect2(
            ruler.Position.X,
            ruler.Position.Y - GroupKeysHeight - 2,
            ruler.Size.X,
            GroupKeysHeight);
    }

    public const float GroupFieldRowHeight = 18;

    /// <summary>
    /// Row <paramref name="row"/> of the picked group's field rows (one per keyed
    /// field, <see cref="Xanadu.Model.Levels.GroupKeys.FieldRows"/>): stacked up
    /// from the footer's top edge over the playfield, on the timeline's time axis,
    /// the first row lowest. The field's name sits left of it, over the clock's column.
    /// </summary>
    public static Rect2 GroupFieldRowRect(PlayfieldView view, int row)
    {
        Rect2 ruler = RulerRect(view);
        float bottom = view.ScreenSize.Y - FooterHeight - 4;
        return new Rect2(ruler.Position.X, bottom - (row + 1) * GroupFieldRowHeight, ruler.Size.X, GroupFieldRowHeight - 2);
    }

    /// <summary>The panel behind <paramref name="rows"/> field rows, label column included; empty for none.</summary>
    public static Rect2 GroupFieldPanelRect(PlayfieldView view, int rows)
    {
        if (rows <= 0)
            return new Rect2();
        Rect2 top = GroupFieldRowRect(view, rows - 1);
        Rect2 bottom = GroupFieldRowRect(view, 0);
        return new Rect2(Margin, top.Position.Y - 3, bottom.End.X - Margin, bottom.End.Y - top.Position.Y + 6);
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
        TimeWindow window = TimeWindow.Of(view, (long)Math.Round(playhead.ShownMs), playhead.DurationMs);
        return (window.Start, window.End);
    }

    /// <summary>Visible time window for a timeline zoom level, following the playhead.</summary>
    public static (long Start, long End) TimelineWindow(
        Playhead playhead,
        float zoom)
    {
        TimeWindow window = TimeWindow.Following(
            (long)Math.Round(playhead.ShownMs), playhead.DurationMs, zoom);
        return (window.Start, window.End);
    }

    public static Rect2 PlayfieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));
}
