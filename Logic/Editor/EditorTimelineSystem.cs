using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor;

/// <summary>Chooses the default timeline zoom once the song length is known.</summary>
public sealed class EditorTimelineSystem : QuerySystem<Playhead, TimelineView>
{
    protected override void OnUpdate() => Query.Each(new AutoZoom());

    private readonly record struct AutoZoom : IEach<Playhead, TimelineView>
    {
        public void Execute(ref Playhead playhead, ref TimelineView timeline)
        {
            if (timeline.AutoZoomed || playhead.DurationMs <= 0)
                return;

            timeline.Zoom = TimelineZoom.ClosestForDuration(
                playhead.DurationMs);
            timeline.AutoZoomed = true;
        }
    }
}
