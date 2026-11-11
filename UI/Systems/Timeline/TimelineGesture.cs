using System.Collections.Generic;
using Xanadu.Systems.Ui;

namespace Xanadu.Timeline;

/// <summary>
/// One timeline's press or drag in progress, kept on its
/// <see cref="Xanadu.Systems.Timeline.TimelineHost"/> rather than as a component
/// of its own (the Animator is close to Friflo's component cap).
/// </summary>
public sealed class TimelineGesture
{
    /// <summary>The surface (or, without one, the pointer) was pressed last frame, so a press only begins once.</summary>
    public bool SurfaceHeld;

    public TimelineGestureKind Kind;

    public Vector2 StartPointer;

    /// <summary>The item taken, as it was when the press landed.</summary>
    public TimelineItem Item;

    public TimelineGrabPart Part;

    /// <summary>A Shift-press on a selected item, which takes it out unless it becomes a drag.</summary>
    public bool PendingRemove;

    /// <summary>The press moved past the item's slop, so it retimes.</summary>
    public bool Moving;

    /// <summary>Where on the scrollbar's thumb the pointer took hold.</summary>
    public float ScrollGrab;

    public readonly UiMarquee<object> Box = new();

    public bool Active => Kind != TimelineGestureKind.None;

    public void Clear()
    {
        Kind = TimelineGestureKind.None;
        Item = default;
        Part = TimelineGrabPart.Body;
        PendingRemove = false;
        Moving = false;
        Box.Cancel();
    }

    /// <summary>What is selected, for a box that adds to it: the scene's own list, or its selected items.</summary>
    public static List<object> Selected(TimelineScene scene)
    {
        if (scene.Selection is { } given)
            return new List<object>(given);

        var selected = new List<object>();
        foreach (TimelineItem item in scene.Items)
        {
            if (item.Role == TimelineItemRole.Selectable && item.Selected)
                selected.Add(item.Ref);
        }

        return selected;
    }
}
