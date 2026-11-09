using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Frees an entity's <see cref="VisualRid"/> canvas item when the entity is deleted.
/// Deleting the entity alone leaves the canvas item drawn on screen, so a closed
/// overlay or panel would stay visible. Every host with an <see cref="EntityStore"/>
/// that draws UI needs one; disposing it frees whatever canvas items are left.
/// </summary>
public sealed class VisualRidRelease : IDisposable
{
    private readonly EntityStore _store;

    public VisualRidRelease(EntityStore store)
    {
        _store = store;
        store.OnEntityDelete += Free;
    }

    public void Dispose()
    {
        _store.OnEntityDelete -= Free;
        foreach (Entity entity in _store.Query<VisualRid>().ToEntityList())
            CanvasServer.Current.Free(entity.GetComponent<VisualRid>().Value);
    }

    private static void Free(EntityDelete deleted)
    {
        if (deleted.Entity.TryGetComponent(out VisualRid visual))
            CanvasServer.Current.Free(visual.Value);
    }
}
