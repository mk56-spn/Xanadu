using System;
using Xanadu.Ecs;

namespace Xanadu.Timeline;

/// <summary>
/// Dragging on a timeline the same way in every tool: the visible window is
/// held still while the playhead or an item is dragged, so the view does not
/// slide under the pointer, and pushing the pointer past the middle part of
/// the timeline pans the held window that way, faster the further out it is.
/// </summary>
public static class TimelineScrub
{
    /// <summary>The share of each half, from the middle, where the window holds still.</summary>
    public const float DeadZone = 0.55f;

    /// <summary>How many windows a second the pan reaches at the very edge.</summary>
    public const float PanRate = 0.45f;

    /// <summary>Holds the window still at <paramref name="window"/> until <see cref="Release"/>.</summary>
    public static void Hold(ref TimelineView view, TimeWindow window)
    {
        view.Scrubbing = true;
        view.ScrubStartMs = window.Start;
        view.ScrubEndMs = window.End;
    }

    public static void Release(ref TimelineView view) => view.Scrubbing = false;

    /// <summary>
    /// Pans a held window when the pointer is out past the dead zone, kept
    /// inside the timeline; false when it did not move.
    /// </summary>
    public static bool Pan(ref TimelineView view, Rect2 area, float x, float deltaSeconds, long durationMs)
    {
        if (!view.Scrubbing || durationMs <= 0 || area.Size.X <= 0)
            return false;

        float centre = area.Position.X + area.Size.X / 2;
        float normalized = (x - centre) / (area.Size.X / 2);
        float overshoot = Math.Max(0, Math.Abs(normalized) - DeadZone) / (1 - DeadZone);
        if (overshoot <= 0)
            return false;

        long span = Math.Max(1, view.ScrubEndMs - view.ScrubStartMs);
        long shift = (long)(span * PanRate * overshoot * overshoot * deltaSeconds * Math.Sign(normalized));
        long start = Math.Clamp(view.ScrubStartMs + shift, 0, Math.Max(0, durationMs - span));
        if (start == view.ScrubStartMs)
            return false;

        view.ScrubStartMs = start;
        view.ScrubEndMs = start + span;
        return true;
    }
}
