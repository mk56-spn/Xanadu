using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor;

/// <summary>
/// The editor's song timeline: chooses the default zoom once the song length is
/// known, and has an <see cref="EditorTimeline"/> describe and answer the shared
/// timeline for each editor the first time it sees one.
/// </summary>
public sealed class EditorTimelineSystem(EditorHistory? history = null) : QuerySystem<Playhead, TimelineView>
{
    /// <summary>Editors already given a timeline, by id and revision: a rebuilt scene may reuse an id.</summary>
    private readonly HashSet<(int Id, int Revision)> _listening = [];

    protected override void OnUpdate()
    {
        Query.Each(new AutoZoom());

        foreach (Entity editor in Query.ToEntityList())
        {
            if (editor.HasComponent<PlayfieldView>() && _listening.Add((editor.Id, editor.Revision)))
                new EditorTimeline(editor, history).Listen();
        }
    }

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
