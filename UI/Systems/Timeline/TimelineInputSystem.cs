using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Timeline;

namespace Xanadu.Systems.Timeline;

/// <summary>
/// Presses and drags on every timeline, read the same way in every tool: the
/// row scrollbar and the wheel over the rows, fixed handles (event flags, loop
/// ends), scrubbing the ruler, track labels, items on the rows, and a box over
/// empty row space. The tool describes its timeline as a
/// <see cref="TimelineScene"/> and is told what each gesture meant through
/// <see cref="ITimelineInput"/>; it never sees the pointer.
/// </summary>
public sealed class TimelineInputSystem(IPointerInputSource pointer, IWheelSource wheel)
    : QuerySystem<TimelineHost, TimelineView>
{
    /// <summary>How far a Shift-press on a selected item may move and still count as a click that takes it out.</summary>
    public const float ClickSlop = 3f;

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.GetComponent<TimelineHost>().Host is ITimelineInput input)
                Update(entity, input);
        }
    }

    private void Update(Entity entity, ITimelineInput input)
    {
        ITimelineHost host = entity.GetComponent<TimelineHost>().Host;
        TimelineGesture gesture = entity.GetComponent<TimelineHost>().Gesture;
        TimelineScene? scene = host.Blocked ? null : input.Scene();
        if (scene is null)
        {
            if (gesture.Active)
                End(entity, input, gesture, pointer.Read().Position);
            gesture.SurfaceHeld = false;
            return;
        }

        PointerInput press = pointer.Read();
        bool pressed = scene.Surface.IsNull ? press.Pressed : scene.Surface.Tags.Has<Pressed>();
        bool begin = pressed && !gesture.SurfaceHeld;
        gesture.SurfaceHeld = pressed;

        if (wheel.Notches != 0 && scene.Lanes is { } wheeled && scene.Panel.HasPoint(press.Position))
        {
            // Zooming is the wheel system's; what reaches here scrolls the rows.
            input.ScrollRows(wheeled.Clamp(wheeled.First - Math.Sign(wheel.TakeWheel())));

            // A box being dragged has to see the scroll this frame.
            scene = input.Scene() ?? scene;
        }

        if (begin && scene.Panel.HasPoint(press.Position))
            Begin(entity, input, gesture, scene, press);
        else if (gesture.Active && !press.Pressed)
            End(entity, input, gesture, press.Position);
        else if (gesture.Active)
            Drag(entity, input, gesture, scene, press);
    }

    private static void Begin(Entity entity, ITimelineInput input, TimelineGesture gesture, TimelineScene scene, PointerInput press)
    {
        Vector2 point = press.Position;
        gesture.Clear();
        gesture.StartPointer = point;

        if (scene.Lanes is { HasBar: true } barred && barred.BarHit.HasPoint(point))
        {
            Start(input, gesture, TimelineGestureKind.RowScroll);
            gesture.ScrollGrab = barred.Slider.Grab(point.Y, barred.Value);
            input.ScrollRows(barred.FirstFor(barred.Slider.ValueAt(point.Y, gesture.ScrollGrab)));
            return;
        }

        if (Pick(scene, point, -1) is { } handle)
        {
            Take(entity, input, gesture, scene, handle.Item, handle.Part, press.Shift);
            return;
        }

        if (scene.ScrubBand.HasPoint(point) && !(press.Shift && scene.ShiftBoxesOnScrub))
        {
            Hold(entity, scene);
            Start(input, gesture, TimelineGestureKind.Scrub);
            Scrub(input, scene, point.X, press.Shift);
            return;
        }

        if (scene.Lanes is not { } lanes)
            return;

        int lane = lanes.LaneAt(point);
        if (lane < 0)
            return;

        if (!lanes.Body.HasPoint(point))
        {
            input.LabelPressed(lane, point);
            return;
        }

        if (Pick(scene, point, lane) is { } onLane)
        {
            Take(entity, input, gesture, scene, onLane.Item, onLane.Part, press.Shift);
            return;
        }

        // Empty row space: the owner reacts to the press, and dragging on draws a box.
        List<object> existing = TimelineGesture.Selected(scene);
        Hold(entity, scene);
        Start(input, gesture, TimelineGestureKind.Box);
        input.LanePressed(lane, scene.SnappedAt(point.X, press.Shift), press.Shift);
        Vector2 content = lanes.ToContent(point);
        if (press.Shift)
            gesture.Box.Begin(content, additive: true, existing);
        else
            gesture.Box.Arm(content);
    }

    private static void Take(
        Entity entity,
        ITimelineInput input,
        TimelineGesture gesture,
        TimelineScene scene,
        TimelineItem item,
        TimelineGrabPart part,
        bool shift)
    {
        TimelinePick pick = item.Role != TimelineItemRole.Selectable ? TimelinePick.None
            : shift ? item.Selected ? TimelinePick.RemoveOnClick : TimelinePick.Add
            : item.Selected ? TimelinePick.Lead : TimelinePick.Only;

        gesture.Item = item;
        gesture.Part = part;
        gesture.PendingRemove = pick == TimelinePick.RemoveOnClick;
        Hold(entity, scene);
        Start(input, gesture, TimelineGestureKind.Item);
        input.ItemPressed(item, pick, part);
    }

    private static void Start(ITimelineInput input, TimelineGesture gesture, TimelineGestureKind kind)
    {
        gesture.Kind = kind;
        input.GestureStarted(kind);
    }

    private void Drag(Entity entity, ITimelineInput input, TimelineGesture gesture, TimelineScene scene, PointerInput press)
    {
        Vector2 point = press.Position;
        switch (gesture.Kind)
        {
            case TimelineGestureKind.RowScroll when scene.Lanes is { } lanes:
                input.ScrollRows(lanes.FirstFor(lanes.Slider.ValueAt(point.Y, gesture.ScrollGrab)));
                return;

            case TimelineGestureKind.Box when scene.Lanes is { } lanes:
                if (!gesture.Box.Update(lanes.ToContent(point), out _))
                    return;
                Publish(entity, gesture);
                input.BoxSelected(gesture.Box.Selection(Covered(scene, lanes, gesture.Box.Bounds)));
                return;

            case TimelineGestureKind.Scrub:
                // Out past the middle of the ruler the held window pans, and the scrub reads the panned window.
                if (TimelineScrub.Pan(
                        ref entity.GetComponent<TimelineView>(),
                        scene.Axis.Area,
                        point.X,
                        Tick.deltaTime,
                        (long)Math.Round(scene.DurationMs)))
                {
                    scene = input.Scene() ?? scene;
                }

                Scrub(input, scene, point.X, press.Shift);
                return;

            case TimelineGestureKind.Item:
                MoveItem(input, gesture, scene, press);
                return;
        }
    }

    /// <summary>Sends the playhead to the time under the pointer, kept on the axis when the pointer runs off it.</summary>
    private static void Scrub(ITimelineInput input, TimelineScene scene, float x, bool free) =>
        input.Scrub(scene.ScrubAt(Math.Clamp(x, scene.Axis.Left, scene.Axis.Right), free));

    /// <summary>Retimes the taken item once the press has moved past its slop.</summary>
    private static void MoveItem(ITimelineInput input, TimelineGesture gesture, TimelineScene scene, PointerInput press)
    {
        Vector2 point = press.Position;
        TimelineItem item = gesture.Item;
        if (!gesture.Moving)
        {
            if (item.Slop > 0 && point.DistanceTo(gesture.StartPointer) < item.Slop)
                return;
            gesture.Moving = true;
        }

        if (item.Shape == TimelineItemShape.Marker)
        {
            // Moved by as much time as the pointer, so where it was grabbed stays under the pointer.
            double moved = item.FromMs + scene.TimeAt(point.X) - scene.TimeAt(gesture.StartPointer.X);
            double ms = press.Shift ? moved : scene.Snap(moved);
            input.ItemMoved(item, ms, ms);
            return;
        }

        var span = new TimelineSpanDrag(gesture.Part, item.FromMs, item.ToMs, scene.TimeAt(gesture.StartPointer.X));
        (double from, double to) = span.Apply(
            scene.TimeAt(point.X),
            press.Shift ? ms => ms : scene.Snap,
            item.LowMs,
            item.HighMs,
            item.MinLengthMs);
        input.ItemMoved(item, from, to);
    }

    private static void End(Entity entity, ITimelineInput input, TimelineGesture gesture, Vector2 point)
    {
        TimelineGestureKind kind = gesture.Kind;
        if (kind == TimelineGestureKind.Item)
            input.ItemReleased(gesture.Item, gesture.PendingRemove && point.DistanceTo(gesture.StartPointer) < ClickSlop);

        if (kind == TimelineGestureKind.Box)
        {
            gesture.Box.End();
            Publish(entity, gesture);
        }

        TimelineScrub.Release(ref entity.GetComponent<TimelineView>());
        gesture.Clear();
        input.GestureEnded(kind);
    }

    /// <summary>Keeps the visible window still while the gesture moves the playhead or an item.</summary>
    private static void Hold(Entity entity, TimelineScene scene) =>
        TimelineScrub.Hold(
            ref entity.GetComponent<TimelineView>(),
            new TimeWindow((long)Math.Round(scene.Axis.StartMs), (long)Math.Round(scene.Axis.EndMs)));

    private static void Publish(Entity entity, TimelineGesture gesture)
    {
        if (entity.HasComponent<UiMarqueeBox>())
            gesture.Box.Publish(ref entity.GetComponent<UiMarqueeBox>());
    }

    /// <summary>
    /// The item a press at <paramref name="point"/> takes among the fixed items
    /// (<paramref name="lane"/> -1) or those on one lane. Markers in a group go
    /// to the nearest within reach; the first group with anything in reach wins.
    /// </summary>
    public static (TimelineItem Item, TimelineGrabPart Part)? Pick(TimelineScene scene, Vector2 point, int lane)
    {
        TimelineLanes? lanes = scene.Lanes;
        if (lane >= 0 && (lanes is not { } shown || !shown.Shows(lane) || !shown.Body.HasPoint(point)))
            return null;

        List<TimelineItem> items = scene.Items;
        var groupsDone = new HashSet<int>();
        for (int start = 0; start < items.Count; start++)
        {
            int group = items[start].Group;
            if (items[start].Lane != lane || !groupsDone.Add(group))
                continue;

            (TimelineItem, TimelineGrabPart)? best = null;
            float closest = float.MaxValue;
            for (int i = start; i < items.Count; i++)
            {
                TimelineItem item = items[i];
                if (item.Group != group || item.Lane != lane || (lane < 0 && !item.Area.HasPoint(point)))
                    continue;

                if (item.Shape == TimelineItemShape.Span)
                {
                    if (best is null &&
                        TimelineGrab.Span(scene.Axis, item.FromMs, item.ToMs, point.X, item.Reach) is { } part)
                    {
                        best = (item, part);
                        closest = -1f;
                    }
                    continue;
                }

                float distance = Math.Abs(point.X - scene.Axis.X(item.FromMs));
                if (distance <= item.Reach && distance <= closest)
                {
                    closest = distance;
                    best = (item, TimelineGrabPart.Body);
                }
            }

            if (best is not null)
                return best;
        }

        return null;
    }

    /// <summary>
    /// The selectable items whose markers touch a box in content space, among the
    /// times on screen: a lane item's marker sits in the middle of its row, a
    /// fixed one's in the middle of its area.
    /// </summary>
    public static List<object> Covered(TimelineScene scene, TimelineLanes lanes, Rect2 box)
    {
        var hits = new List<object>();
        foreach (TimelineItem item in scene.Items)
        {
            if (item.Role != TimelineItemRole.Selectable || !scene.Axis.Shows(item.FromMs))
                continue;

            float middle;
            if (item.Lane >= 0)
            {
                Rect2 row = lanes.Row(item.Lane);
                if (!box.Intersects(row, includeBorders: true))
                    continue;
                middle = row.GetCenter().Y;
            }
            else
            {
                middle = item.Area.GetCenter().Y + lanes.ScrollY;
            }

            var centre = new Vector2(scene.Axis.X(item.FromMs), middle);
            var marker = new Rect2(centre - new Vector2(item.Size, item.Size), new Vector2(item.Size * 2, item.Size * 2));
            if (box.Intersects(marker, includeBorders: true) && !hits.Contains(item.Ref))
                hits.Add(item.Ref);
        }

        return hits;
    }
}
