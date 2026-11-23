using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Timeline;

/// <summary>What an item on a timeline is drawn as: one time, or a stretch between two.</summary>
public enum TimelineItemShape
{
    Marker,
    Span,
}

/// <summary>How a press treats an item.</summary>
public enum TimelineItemRole
{
    /// <summary>
    /// Part of the timeline's selection: a click picks it, Shift adds it or
    /// takes it out, a box covers it, and a drag moves the whole selection.
    /// </summary>
    Selectable,

    /// <summary>Moved on its own: a press takes it and a drag retimes it (an event flag, a loop end, a span).</summary>
    Handle,
}

/// <summary>
/// One thing on a timeline, handed over fresh each frame; nothing on the UI side
/// keeps it. <see cref="Ref"/> is the owner's own name for it, given back in
/// every callback, and compared with <see cref="object.Equals(object)"/>.
/// </summary>
/// <param name="Ref">The owner's handle: a key, an event index, a span.</param>
/// <param name="Group">Markers in one group compete for a press by distance; an earlier group wins outright.</param>
/// <param name="Lane">The row it sits on, or -1 for a fixed item placed by <paramref name="Area"/>.</param>
/// <param name="Area">For a fixed item, the screen rectangle that can take it; a box touches its marker at the area's middle height.</param>
/// <param name="Reach">Pixels either side of a marker that still take it; a span's grip on its ends.</param>
/// <param name="Size">Half the marker's drawn size, which a box has to touch.</param>
/// <param name="Slop">How far a press must move before it retimes; under it the press is only a pick.</param>
/// <param name="LowMs">A span cannot be moved before this.</param>
/// <param name="HighMs">Or after this.</param>
/// <param name="MinLengthMs">Nor made shorter than this.</param>
public readonly record struct TimelineItem(
    object Ref,
    int Group,
    TimelineItemShape Shape,
    double FromMs,
    double ToMs,
    int Lane = -1,
    Rect2 Area = default,
    float Reach = 6f,
    float Size = 6f,
    TimelineItemRole Role = TimelineItemRole.Selectable,
    bool Selected = false,
    float Slop = 0f,
    double LowMs = 0,
    double HighMs = double.MaxValue,
    double MinLengthMs = 0)
{
    public static TimelineItem Marker(object reference, int group, double ms, int lane = -1, Rect2 area = default) =>
        new(reference, group, TimelineItemShape.Marker, ms, ms, lane, area);

    public static TimelineItem Span(object reference, int group, double fromMs, double toMs, int lane = -1, Rect2 area = default) =>
        new(reference, group, TimelineItemShape.Span, fromMs, toMs, lane, area, Role: TimelineItemRole.Handle);
}

/// <summary>
/// The rows of a timeline, scrolled as a list: a label column, the body where
/// items sit, and a scrollbar beside them when there are more rows than fit.
/// Items on a lane are laid out in content space, as if nothing were scrolled,
/// so a box keeps its rows while they scroll under it.
/// </summary>
/// <param name="Body">The visible rows' item area on screen.</param>
/// <param name="Labels">The label column beside the body.</param>
/// <param name="RowHeight">The height of one row.</param>
/// <param name="Count">How many rows there are.</param>
/// <param name="First">The first row shown.</param>
/// <param name="Visible">How many rows fit.</param>
/// <param name="Bar">The scrollbar's drawn track.</param>
/// <param name="BarHit">Where a press takes the scrollbar.</param>
/// <param name="MinThumb">The shortest the thumb is drawn.</param>
public readonly record struct TimelineLanes(
    Rect2 Body,
    Rect2 Labels,
    float RowHeight,
    int Count,
    int First,
    int Visible,
    Rect2 Bar,
    Rect2 BarHit,
    float MinThumb)
{
    public bool HasBar => Count > Visible;

    public ScrollSlider Slider => new(Bar.Position.Y, Bar.End.Y, Count > 0 ? (float)Visible / Count : 1f, MinThumb);

    /// <summary>How far down the rows are scrolled, from 0 to 1.</summary>
    public float Value => HasBar ? (float)First / (Count - Visible) : 0f;

    public int FirstFor(float value) => HasBar ? (int)Math.Round(value * (Count - Visible)) : 0;

    public int Clamp(int first) => Math.Clamp(first, 0, Math.Max(0, Count - Visible));

    /// <summary>How far the content is scrolled up.</summary>
    public float ScrollY => First * RowHeight;

    /// <summary>A lane's row in content space.</summary>
    public Rect2 Row(int lane) =>
        new(Body.Position.X, Body.Position.Y + lane * RowHeight, Body.Size.X, RowHeight);

    /// <summary>A screen point as if the rows were not scrolled.</summary>
    public Vector2 ToContent(Vector2 screen) => screen + new Vector2(0, ScrollY);

    /// <summary>The lane under a screen point in the labels or the body, or -1.</summary>
    public int LaneAt(Vector2 point)
    {
        bool inside = point.X >= Labels.Position.X && point.X <= Body.End.X &&
                      point.Y >= Body.Position.Y && point.Y < Body.Position.Y + Visible * RowHeight;
        if (!inside)
            return -1;

        int lane = First + (int)((point.Y - Body.Position.Y) / RowHeight);
        return lane < Count ? lane : -1;
    }

    public bool Shows(int lane) => lane >= First && lane < First + Visible && lane < Count;
}

/// <summary>
/// A scrollbar along time: its thumb is the visible window within the whole
/// timeline, and dragging it moves the window without moving the playhead.
/// </summary>
/// <param name="Bar">The drawn track, as wide as the time axis.</param>
/// <param name="Hit">Where a press takes it.</param>
/// <param name="MinThumb">The shortest the thumb is drawn.</param>
public readonly record struct TimelineTimeBar(Rect2 Bar, Rect2 Hit, float MinThumb)
{
    /// <summary>Whether part of the timeline is out of sight, so the bar shows.</summary>
    public static bool Shows(TimelineAxis axis, double durationMs) => durationMs > 0 && axis.EndMs - axis.StartMs < durationMs - 0.5;

    public ScrollSlider Slider(TimelineAxis axis, double durationMs) => new(
        Bar.Position.X,
        Bar.End.X,
        durationMs > 0 ? (float)Math.Min(1.0, (axis.EndMs - axis.StartMs) / durationMs) : 1f,
        MinThumb);

    /// <summary>How far along the window is, from 0 at the start to 1 at the end.</summary>
    public static float Value(TimelineAxis axis, double durationMs)
    {
        double room = durationMs - (axis.EndMs - axis.StartMs);
        return room > 0 ? (float)Math.Clamp(axis.StartMs / room, 0.0, 1.0) : 0f;
    }

    /// <summary>Where the window starts for a value.</summary>
    public static long StartFor(TimelineAxis axis, double durationMs, float value) =>
        (long)Math.Round(Math.Max(0.0, durationMs - (axis.EndMs - axis.StartMs)) * value);
}

/// <summary>
/// A timeline as its owner sees it this frame: where it is, how time maps to
/// x, how a time snaps, its rows and everything on it. The UI reads presses
/// and drags against this and tells the owner what they meant.
/// </summary>
public sealed class TimelineScene
{
    /// <summary>
    /// The UI element whose press starts a gesture, so whatever covers it wins.
    /// A timeline drawn straight onto a canvas has none, and takes any press in
    /// <see cref="Panel"/>.
    /// </summary>
    public Entity Surface;

    /// <summary>The whole timeline; a press outside it is not the timeline's.</summary>
    public Rect2 Panel;

    /// <summary>Time across the screen.</summary>
    public TimelineAxis Axis;

    /// <summary>Where a press scrubs the playhead.</summary>
    public Rect2 ScrubBand;

    /// <summary>How long the timeline is, so a held window pans no further.</summary>
    public double DurationMs;

    /// <summary>The owner's grid; Shift leaves a time free.</summary>
    public Func<double, double> Snap = ms => ms;

    /// <summary>Whether scrubbing lands on the grid too, or follows the pointer exactly.</summary>
    public bool SnapScrub = true;

    /// <summary>
    /// Shift on the scrub band drags a box instead of scrubbing, for a timeline
    /// whose only row is its ruler.
    /// </summary>
    public bool ShiftBoxesOnScrub;

    /// <summary>
    /// Everything selected, including what has no item this frame; a Shift box
    /// keeps it. Left null, the selected items are used.
    /// </summary>
    public IReadOnlyList<object>? Selection;

    public TimelineLanes? Lanes;

    /// <summary>The scrollbar along time under the rows, or null for none; it shows only while zoomed in.</summary>
    public TimelineTimeBar? TimeBar;

    public readonly List<TimelineItem> Items = [];

    public double TimeAt(float x) => Axis.TimeAt(x);

    /// <summary>The time under <paramref name="x"/>, snapped unless <paramref name="free"/>.</summary>
    public double SnappedAt(float x, bool free) => free ? TimeAt(x) : Snap(TimeAt(x));

    /// <summary>Where the playhead goes for a scrub at <paramref name="x"/>.</summary>
    public double ScrubAt(float x, bool free) => SnappedAt(x, free || !SnapScrub);
}

/// <summary>How a press changed the selection, for the owner to apply.</summary>
public enum TimelinePick
{
    /// <summary>Not a selectable item; nothing to change.</summary>
    None,

    /// <summary>Select only this one.</summary>
    Only,

    /// <summary>Already selected: keep the selection and make this one lead it.</summary>
    Lead,

    /// <summary>Shift on an unselected one: add it.</summary>
    Add,

    /// <summary>Shift on a selected one: it comes out on release unless the press became a drag.</summary>
    RemoveOnClick,
}

/// <summary>What a gesture on the timeline is doing.</summary>
public enum TimelineGestureKind
{
    None,
    Scrub,
    RowScroll,

    /// <summary>Dragging the scrollbar along time.</summary>
    TimeScroll,
    Item,
    Box,
}
