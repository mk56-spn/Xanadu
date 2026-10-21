using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Chooses the default timeline zoom once the song length is known.</summary>
public sealed class EditorTimelineSystem : QuerySystem<Playhead, EditorTimeline>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref playhead, ref timeline, _) =>
        {
            if (timeline.AutoZoomed || playhead.DurationMs <= 0)
                return;

            timeline.Zoom = EditorTimelineZoom.ClosestForDuration(
                playhead.DurationMs);
            timeline.AutoZoomed = true;
        });
    }
}
