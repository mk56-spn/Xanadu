using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Systems.Timeline;

/// <summary>
/// Lets a pinned timeline window go once the playhead moves out of it, by
/// playing past its end or by a seek, so the view goes back to following the
/// playhead. A playhead that stays put, or moves within the window, keeps it.
/// </summary>
public sealed class TimelinePinSystem : QuerySystem<TimelineHost, TimelineView>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TimelineHost timeline, ref TimelineView view, Entity _) =>
        {
            if (!view.Pinned)
                return;

            long now = (long)System.Math.Round(timeline.Host.NowMs);
            if (now == view.PinNowMs)
                return;

            TimeWindow window = TimeWindow.Starting(
                view.PinStartMs,
                (long)System.Math.Round(timeline.Host.DurationMs),
                view.Zoom);
            if (window.Contains(now))
                view.PinNowMs = now;
            else
                view.Pinned = false;
        });
    }
}
