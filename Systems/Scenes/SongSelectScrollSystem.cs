using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scenes;

namespace Xanadu.Systems.Scenes;

/// <summary>Eases the song-select list toward the selected level each frame.</summary>
public sealed class SongSelectScrollSystem : QuerySystem<SongSelectState>
{
    private readonly List<Entity> _lists = new();

    protected override void OnUpdate()
    {
        // Laying out rows tags entities, which a running query forbids, so
        // collect the lists first and move them once iteration has finished.
        _lists.Clear();
        Query.ForEachEntity((ref _, entity) => _lists.Add(entity));

        float delta = Tick.deltaTime;
        foreach (Entity list in _lists)
        {
            ref SongSelectState state = ref list.GetComponent<SongSelectState>();
            SongSelectScene.AdvanceScroll(ref state, delta);
        }
    }
}
