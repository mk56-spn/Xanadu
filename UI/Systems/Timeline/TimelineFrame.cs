using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Systems.Timeline;

/// <summary>
/// A timeline as its owner describes it this frame: the clock, where it is on
/// screen, whether something modal is up, its markers and its scene. The owner
/// writes it whenever <see cref="TimelineFrameDue"/> is emitted on the entity,
/// once at the start of the timeline group and again after a scroll; the
/// shared timeline only reads it. It is derived output, never written back,
/// so the owner's own state stays the one source of truth. Times are in milliseconds.
/// </summary>
public struct TimelineFrame() : IComponent
{
    public double NowMs;
    public double DurationMs;

    /// <summary>One snap step, and where the step grid starts.</summary>
    public double StepMs = 1;
    public double StepOriginMs;

    /// <summary>Something modal is up, so the timeline leaves the pointer and wheel alone.</summary>
    public bool Blocked;

    /// <summary>The whole timeline and its ruler on screen; false while it is hidden.</summary>
    public bool Shown;
    public Rect2 Panel;
    public Rect2 Ruler;

    /// <summary>The times of everything on the timeline, in any order; refilled by the owner.</summary>
    public readonly List<double> Markers = [];

    /// <summary>The timeline to draw and hit-test this frame, or null when it is hidden.</summary>
    public TimelineScene? Scene;

    /// <summary>The press or drag under way on this timeline; <see cref="TimelineInputSystem"/> keeps it.</summary>
    public readonly TimelineGesture Gesture = new();

    /// <summary>Asks <paramref name="timeline"/>'s owner to describe it now, and reads what it wrote.</summary>
    public static ref TimelineFrame Refresh(Entity timeline)
    {
        timeline.EmitSignal(new TimelineFrameDue());
        return ref timeline.GetComponent<TimelineFrame>();
    }
}

// Signals the shared timeline emits on the timeline entity. The owner hears the
// ones it acts on with AddSignalHandler; the rest go unheard.

/// <summary>Write <see cref="TimelineFrame"/> now.</summary>
public readonly record struct TimelineFrameDue;

/// <summary>Move the playhead to <paramref name="Ms"/>, already kept within the song.</summary>
public readonly record struct TimelineSeek(double Ms);

/// <summary>Play if stopped, stop if playing.</summary>
public readonly record struct TimelineTogglePlay;

/// <summary>A jump found no marker that way: below zero is earlier, above is later.</summary>
public readonly record struct TimelineNoMarker(int Direction);

/// <summary>A gesture began; the owner can mark itself busy.</summary>
public readonly record struct TimelineGestureStarted(TimelineGestureKind Kind);

/// <summary>The gesture is over, whatever it was.</summary>
public readonly record struct TimelineGestureEnded(TimelineGestureKind Kind);

/// <summary>Show rows from <paramref name="First"/> on.</summary>
public readonly record struct TimelineRowsScrolled(int First);

/// <summary>The playhead goes to a time, already snapped.</summary>
public readonly record struct TimelineScrubbed(double Ms);

/// <summary>A press on a lane's label.</summary>
public readonly record struct TimelineLabelPressed(int Lane, Vector2 Point);

/// <summary>A press on empty lane space, at a snapped time; a box may follow, adding to the selection when <paramref name="Additive"/>.</summary>
public readonly record struct TimelineLanePressed(int Lane, double Ms, bool Additive);

/// <summary>A press took an item, and changed the selection as <paramref name="Pick"/> says.</summary>
public readonly record struct TimelineItemPressed(TimelineItem Item, TimelinePick Pick, TimelineGrabPart Part);

/// <summary>
/// The taken item has been dragged: a marker to <paramref name="FromMs"/> (both
/// times equal), a span to its new ends. Each is where it was plus how far the
/// pointer has moved in time, snapped already; a selected marker carries the
/// rest of the selection by the same amount.
/// </summary>
public readonly record struct TimelineItemMoved(TimelineItem Item, double FromMs, double ToMs);

/// <summary>The press on an item let go; <paramref name="Remove"/> when a Shift-click takes it out.</summary>
public readonly record struct TimelineItemReleased(TimelineItem Item, bool Remove);

/// <summary>A box made this the selection.</summary>
public readonly record struct TimelineBoxSelected(IReadOnlyList<object> Selection);

/// <summary>
/// First in the timeline group: has every timeline's owner write its
/// <see cref="TimelineFrame"/>, so the systems after it read this frame's.
/// </summary>
public sealed class TimelineFrameSystem : Friflo.Engine.ECS.Systems.QuerySystem<TimelineFrame>
{
    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
            entity.EmitSignal(new TimelineFrameDue());
    }
}
