using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace Writer.Systems;

/// <summary>
/// Initialises uninitialised drawables by parenting their canvas item to the
/// layer matching <see cref="Drawable.ZIndex"/> and applying <see cref="Position"/>.
/// Missing layers fall back to layer 0 and log a console error.
/// </summary>
public sealed class DrawableInitialiseSystem : QuerySystem<Rectangle, Drawable, VisualRid, Position>
{
    private const int DefaultLayer = 0;

    private readonly Dictionary<int, ArchetypeQuery<VisualRid, Layer>> _layerQueries = new();

    public DrawableInitialiseSystem()
    {
        Filter.AllTags(Tags.Get<Uninitialised>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref _, ref drawable, ref visual, ref position, entity) =>
        {
            int requested = drawable.ZIndex;
            if (!TryGetLayerCanvas(entity.Store, requested, out Rid layerCanvas))
            {
                GD.PushError(
                    $"Drawable entity {entity.Id} references missing layer {requested}; falling back to layer {DefaultLayer}.");

                if (!TryGetLayerCanvas(entity.Store, DefaultLayer, out layerCanvas))
                {
                    GD.PushError(
                        $"Drawable entity {entity.Id} cannot fall back because layer {DefaultLayer} does not exist.");
                    return;
                }

                drawable.ZIndex = DefaultLayer;
            }

            RenderingServer.CanvasItemSetParent(visual.Value, layerCanvas);
            RenderingServer.CanvasItemSetTransform(visual.Value, new Transform2D(0, position.Value));
            buffer.RemoveTag<Uninitialised>(entity.Id);
        });
    }

    private bool TryGetLayerCanvas(EntityStore store, int zIndex, out Rid canvas)
    {
        Rid result = default;
        bool found = false;

        LayerQuery(store, zIndex).ForEachEntity((ref visual, ref _, _) =>
        {
            result = visual.Value;
            found = true;
        });

        canvas = result;
        return found;
    }

    private ArchetypeQuery<VisualRid, Layer> LayerQuery(EntityStore store, int zIndex)
    {
        if (_layerQueries.TryGetValue(zIndex, out ArchetypeQuery<VisualRid, Layer>? query))
            return query;

        query = store.Query<VisualRid, Layer>().HasValue<Layer, int>(zIndex);
        _layerQueries.Add(zIndex, query);
        return query;
    }
}
