using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Timeline;
using Xanadu.Ui.Systems.Input;
using Rect2 = Xanadu.Core.Maths.Rect2;

namespace Xanadu.Ui.Systems.Timeline;

/// <summary>
/// Zooms a timeline: the wheel with Ctrl anywhere over it, or alone over a ruler
/// the tool offers for it. The zoom is about the pointer: the time under it stays
/// under it, by pinning the window until the playhead moves out of it. Every
/// other notch stays for the tool's own wheel.
/// </summary>
public sealed class TimelineWheelSystem(IPointerInputSource pointer, IWheelSource wheel)
    : QuerySystem<TimelineFrame, TimelineView>
{
    protected override void OnUpdate()
    {
        if (wheel.Notches == 0)
            return;

        Vector2 position = pointer.Read().Position;
        foreach (Entity entity in Query.Entities)
        {
            TimelineFrame frame = entity.GetComponent<TimelineFrame>();
            Rect2 panel = frame.Panel;
            Rect2 ruler = frame.Ruler;
            if (frame.Blocked || !frame.Shown ||
                !panel.HasPoint(position) || !(wheel.Control || ruler.HasPoint(position)))
            {
                continue;
            }

            ref TimelineView view = ref entity.GetComponent<TimelineView>();
            ZoomAbout(ref view, frame, ruler.Size.X > 0 ? ruler : panel, position.X, wheel.TakeWheel());
            return;
        }
    }

    /// <summary>
    /// Steps the zoom by <paramref name="notches"/>, keeping the time under
    /// <paramref name="x"/> still; <paramref name="axis"/> is where time runs.
    /// </summary>
    public static void ZoomAbout(ref TimelineView view, in TimelineFrame frame, Rect2 axis, float x, int notches)
    {
        long duration = (long)Math.Round(frame.DurationMs);
        long now = (long)Math.Round(frame.NowMs);
        TimeWindow before = TimeWindow.Of(view, now, duration);
        float zoom = TimelineZoom.Step(view.Zoom, notches);

        view.PinStartMs = before.StartKeeping(axis, x, duration, zoom);
        view.PinNowMs = now;
        view.Pinned = true;
        view.Zoom = zoom;
        view.AutoZoomed = true;
    }
}
