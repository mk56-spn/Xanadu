using Godot;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>The one mapping from song time to an x on the timeline, shared by its layers.</summary>
public static class EditorTimelineGeometry
{
    public static float XForTime(Rect2 ruler, long windowStart, long windowEnd, long timeMs) =>
        new TimeWindow(windowStart, windowEnd).XForTime(ruler, timeMs);
}
