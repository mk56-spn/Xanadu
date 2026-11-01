using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Timeline;

namespace Xanadu.Systems.Timeline;

/// <summary>
/// Zooms a timeline: the wheel with Ctrl anywhere over it, or alone over a ruler
/// the tool offers for it. Every other notch stays for the tool's own wheel.
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
            view.Zoom = TimelineZoom.Step(view.Zoom, wheel.TakeWheel());
            view.AutoZoomed = true;
            return;
        }
    }
}
