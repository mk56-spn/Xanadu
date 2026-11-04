using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Timeline;

namespace Xanadu.Systems.Timeline;

/// <summary>
/// Zooms a timeline: the wheel with Ctrl anywhere over it, or alone over a ruler
/// the tool offers for it. The zoom is about the pointer: the time under it stays
/// under it, by pinning the window until the playhead moves out of it. Every
/// other notch stays for the tool's own wheel.
/// </summary>
public sealed class TimelineWheelSystem(IPointerInputSource pointer, IWheelSource wheel)
    : QuerySystem<TimelineHost, TimelineView>
{
    protected override void OnUpdate()
    {
        if (wheel.Notches == 0)
            return;

        Vector2 position = pointer.Read().Position;
        foreach (Entity entity in Query.ToEntityList())
        {
            ITimelineHost host = entity.GetComponent<TimelineHost>().Host;
            if (host.Blocked || !host.TryGetArea(out Rect2 panel, out Rect2 ruler) ||
                !panel.HasPoint(position) || !(wheel.Control || ruler.HasPoint(position)))
            {
                continue;
            }

            ref TimelineView view = ref entity.GetComponent<TimelineView>();
            ZoomAbout(ref view, host, ruler.Size.X > 0 ? ruler : panel, position.X, wheel.TakeWheel());
            return;
        }
    }

    /// <summary>
    /// Steps the zoom by <paramref name="notches"/>, keeping the time under
    /// <paramref name="x"/> still; <paramref name="axis"/> is where time runs.
    /// </summary>
    public static void ZoomAbout(ref TimelineView view, ITimelineHost host, Rect2 axis, float x, int notches)
    {
        long duration = (long)Math.Round(host.DurationMs);
        long now = (long)Math.Round(host.NowMs);
        TimeWindow before = TimeWindow.Of(view, now, duration);
        float zoom = TimelineZoom.Step(view.Zoom, notches);

        view.PinStartMs = before.StartKeeping(axis, x, duration, zoom);
        view.PinNowMs = now;
        view.Pinned = true;
        view.Zoom = zoom;
        view.AutoZoomed = true;
    }
}
