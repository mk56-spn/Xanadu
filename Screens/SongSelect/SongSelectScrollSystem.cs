using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Screens.SongSelect;

/// <summary>Eases the song-select list toward the selected level each frame, unless the pointer is dragging it.</summary>
public sealed class SongSelectScrollSystem : QuerySystem<SongSelectState>
{
    private readonly List<Entity> _lists = new();

    protected override void OnUpdate()
    {
        // Laying out rows tags entities, which a running query forbids, so
        // collect the lists first and move them once iteration has finished.
        _lists.Clear();
        Query.EachEntity(new CollectLists(Query.Store, _lists));

        float delta = Tick.deltaTime;
        foreach (Entity list in _lists)
        {
            ref SongSelectState state = ref list.GetComponent<SongSelectState>();
            if (!list.Tags.Has<SongSelectDragging>())
                SongSelectScene.AdvanceScroll(ref state, delta);
            SongSelectScene.RefreshScoreThumb(ref state);
        }
    }

    private readonly record struct CollectLists(EntityStore Store, List<Entity> Lists) : IEachEntity<SongSelectState>
    {
        public void Execute(ref SongSelectState _, int id) => Lists.Add(Store.GetEntityById(id));
    }
}
