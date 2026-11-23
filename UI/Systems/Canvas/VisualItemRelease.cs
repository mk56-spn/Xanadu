using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// Frees an entity's <see cref="VisualItem"/> canvas item when the entity is deleted.
/// Deleting the entity alone leaves the canvas item drawn on screen, so a closed
/// overlay or panel would stay visible. Every host with an <see cref="EntityStore"/>
/// that draws UI needs one; disposing it frees whatever canvas items are left.
/// </summary>
public sealed class VisualItemRelease : IDisposable
{
    private readonly EntityStore _store;

    public VisualItemRelease(EntityStore store)
    {
        _store = store;
        store.OnEntityDelete += Free;
    }

    public void Dispose()
    {
        _store.OnEntityDelete -= Free;
        foreach (Entity entity in _store.Query<VisualItem>().Entities)
            CanvasServer.Current.Free(entity.GetComponent<VisualItem>().Value);
    }

    private static void Free(EntityDelete deleted)
    {
        if (deleted.Entity.TryGetComponent(out VisualItem visual))
            CanvasServer.Current.Free(visual.Value);
    }
}
