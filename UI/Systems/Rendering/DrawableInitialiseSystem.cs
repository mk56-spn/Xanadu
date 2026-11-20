using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// Initialises uninitialised drawables by parenting their canvas item under a
/// container parent, the layer above it in the tree or, for an entity outside
/// any layer's tree, the layer matching <see cref="Drawable.ZIndex"/>, then
/// applying <see cref="Position"/>. Missing layers fall back to layer 0.
/// </summary>
public sealed class DrawableInitialiseSystem : QuerySystem<Rectangle, Drawable, VisualRid, Position>
{
    private const int DefaultLayer = UiLayers.Base;

    private readonly Dictionary<int, ArchetypeQuery<VisualRid, Layer>> _layerQueries = new();

    public DrawableInitialiseSystem()
    {
        Filter.AllTags(Tags.Get<Uninitialised>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new Initialise(this, Query.Store, CommandBuffer));
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

        if (entity.Tags.Has<UiSliderFill>() || entity.Tags.Has<UiRaised>())
            return 2;

        return entity.HasComponent<Text>() ||
               entity.HasComponent<ButtonVisual>() ||
               entity.Tags.Has<TakesInput>()
            ? 1
            : 0;
    }

    /// <summary>
    /// The layer this entity hangs under in the tree. Two layers can share a z
    /// index, and fading one must not reach what belongs to the other.
    /// </summary>
    private static bool TryGetOwnLayerCanvas(Entity entity, out Handle canvas)
    {
        for (Entity node = entity.Parent; !node.IsNull; node = node.Parent)
        {
            if (node.HasComponent<Layer>() && node.TryGetComponent(out VisualRid visual))
            {
                canvas = visual.Value;
                return true;
            }
        }

        canvas = default;
        return false;
    }

    private bool TryGetLayerCanvas(EntityStore store, int zIndex, out Handle canvas)
    {
        LayerCanvas layer = LayerQuery(store, zIndex).Each(new LayerCanvas());
        canvas = layer.Canvas;
        return layer.Found;
    }

    private ArchetypeQuery<VisualRid, Layer> LayerQuery(EntityStore store, int zIndex)
    {
        if (_layerQueries.TryGetValue(zIndex, out ArchetypeQuery<VisualRid, Layer>? query))
            return query;

        query = store.Query<VisualRid, Layer>().HasValue<Layer, int>(zIndex);
        _layerQueries.Add(zIndex, query);
        return query;
    }

    private readonly record struct Initialise(DrawableInitialiseSystem System, EntityStore Store, CommandBuffer Buffer) : IEachEntity<Rectangle, Drawable, VisualRid, Position>
    {
        public void Execute(ref Rectangle _, ref Drawable drawable, ref VisualRid visual, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            int requested = drawable.ZIndex;
            if (!TryGetOwnLayerCanvas(entity, out Handle layerCanvas) &&
                !System.TryGetLayerCanvas(entity.Store, requested, out layerCanvas))
            {
                GameLog.Error(
                    $"Drawable entity {id} references missing layer {requested}; falling back to layer {DefaultLayer}.");

                if (!System.TryGetLayerCanvas(entity.Store, DefaultLayer, out layerCanvas))
                {
                    GameLog.Error(
                        $"Drawable entity {id} cannot fall back because layer {DefaultLayer} does not exist.");
                    return;
                }

                drawable.ZIndex = DefaultLayer;
            }

            Entity parent = entity.Parent;
            Handle parentCanvas = layerCanvas;

            // Any drawn parent holds its children's canvas items, so a child is placed,
            // turned and faded in its parent's space, as input already reads it
            // (UiTransform.WorldPosition). A layer's own item is the layer canvas.
            if (!parent.IsNull && parent.TryGetComponent<VisualRid>(out VisualRid parentVisual))
                parentCanvas = parentVisual.Value;

            CanvasServer.Current.SetParent(visual.Value, parentCanvas);

            CanvasServer.Current.SetZIndex(
                visual.Value,
                LocalZIndex(entity));
            CanvasServer.Current.SetTransform(visual.Value, UiTransform.Local(entity, position.Value));
            if (entity.TryGetComponent(out UiMaterial material))
                CanvasServer.Current.SetMaterial(visual.Value, material.Material);

            Buffer.RemoveTag<Uninitialised>(id);
        }
    }

    private struct LayerCanvas : IEach<VisualRid, Layer>
    {
        public Handle Canvas;
        public bool Found;

        public void Execute(ref VisualRid visual, ref Layer _)
        {
            Canvas = visual.Value;
            Found = true;
        }
    }
}
