using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Rendering;

/// <summary>
/// Initialises uninitialised drawables by parenting their canvas item under a
/// container parent or the layer matching <see cref="Drawable.ZIndex"/>, then
/// applying <see cref="Position"/>. Missing layers fall back to layer 0.
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

            Entity parent = entity.Parent;
            Rid parentCanvas = layerCanvas;

            if (!parent.IsNull &&
                parent.HasComponent<UiContainer>() &&
                parent.TryGetComponent<VisualRid>(out VisualRid parentVisual))
            {
                parentCanvas = parentVisual.Value;
            }

            RenderingServer.CanvasItemSetParent(visual.Value, parentCanvas);

            RenderingServer.CanvasItemSetZIndex(
                visual.Value,
                LocalZIndex(entity));
            RenderingServer.CanvasItemSetTransform(visual.Value, new Transform2D(0, position.Value));
            buffer.RemoveTag<Uninitialised>(entity.Id);
        });
    }

    public static int LocalZIndex(Entity entity)
    {
        if (entity.HasComponent<UiBackground>())
            return -1;

        // A dropdown floats above whatever it opens over, and its options above
        // it. Sharing the ordinary local z left their order to whichever the
        // initialise pass reached first, and nested content (a panel's text
        // stacks its own z on its parent's) could still draw over the list.
        if (entity.HasComponent<DropdownOption>())
            return 4;

        if (entity.Tags.Has<DropdownListPanel>())
            return 3;

        if (entity.Tags.Has<UiOverlayItem>())
            return 6;

        if (entity.Tags.Has<UiOverlay>())
            return 5;

        if (entity.Tags.Has<UiSliderFill>())
            return 2;

        return entity.HasComponent<Text>() ||
               entity.HasComponent<ButtonVisual>() ||
               entity.Tags.Has<TakesInput>()
            ? 1
            : 0;
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
