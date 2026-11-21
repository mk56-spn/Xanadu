using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// The UI's place on the canvas. Each frame the renderer asks it what to draw,
/// and it walks the entity tree, layer by layer, listing each drawn entity's
/// own item with its transform, layer opacity, z and clip worked out from the
/// components there and then. Nothing is mirrored into a parented item tree,
/// so moving, hiding, fading or reordering an entity is just changing it.
/// </summary>
/// <remarks>
/// An entity is drawn when it has <see cref="Rectangle"/>, <see cref="Drawable"/>,
/// <see cref="VisualItem"/> and <see cref="Position"/>. It sits in its parent's
/// space when its parent is drawn, else in its layer's: the layer above it in
/// the tree, or for an entity outside any layer, the one its
/// <see cref="Drawable.ZIndex"/> names. Disabled entities and those tagged
/// <see cref="CanvasHidden"/> hide with everything under them.
/// </remarks>
public sealed class UiCanvasMount(EntityStore store) : ICanvasMount
{
    private readonly ArchetypeQuery<Layer, VisualItem> _layers = store.Query<Layer, VisualItem>();
    private readonly ArchetypeQuery<Rectangle, Drawable, VisualItem, Position> _drawn = store.Query<Rectangle, Drawable, VisualItem, Position>();
    private readonly List<Entity> _order = new();
    private readonly Dictionary<int, List<Entity>> _loose = new();

    private readonly record struct Base(Transform2D Transform, Color Modulate, int Z, Rect2? Clip);

    public void Collect(ICanvasCollector into)
    {
        _order.Clear();
        _layers.EachEntity(new CollectLayers(store, _order));
        // Lower z first; between equals, the one made first, as their items were parented.
        _order.Sort(static (a, b) =>
        {
            int z = a.GetComponent<Layer>().ZIndex.CompareTo(b.GetComponent<Layer>().ZIndex);
            return z != 0 ? z : a.Id.CompareTo(b.Id);
        });

        foreach (List<Entity> list in _loose.Values)
            list.Clear();
        _drawn.EachEntity(new CollectLoose(store, _loose));

        foreach (Entity layer in _order)
        {
            if (layer.Tags.Has<CanvasHidden>())
                continue;

            int z = layer.GetComponent<Layer>().ZIndex;
            float opacity = layer.TryGetComponent(out LayerOpacity fade) ? fade.Value : 1f;
            var under = new Base(Transform2D.Identity, new Color(1f, 1f, 1f, opacity), z, null);
            into.Item(layer.GetComponent<VisualItem>().Value, Transform2D.Identity, under.Modulate, z, null);
            foreach (Entity child in layer.ChildEntities)
                Walk(into, child, under, under);

            if (_loose.TryGetValue(z, out List<Entity>? loose))
            {
                foreach (Entity entity in loose)
                    Walk(into, entity, under, under);
            }
        }
    }

    private static void Walk(ICanvasCollector into, Entity entity, Base parent, Base layer)
    {
        if (!entity.Enabled || entity.Tags.Has<CanvasHidden>() || entity.HasComponent<Layer>())
            return;

        if (!IsDrawn(entity))
        {
            // Its children hang under the layer, as they would without it.
            foreach (Entity child in entity.ChildEntities)
                Walk(into, child, layer, layer);
            return;
        }

        Transform2D transform = parent.Transform * UiTransform.Local(entity, entity.GetComponent<Position>().Value);
        int z = parent.Z + LocalZIndex(entity);
        Handle item = entity.GetComponent<VisualItem>().Value;
        if (entity.TryGetComponent(out UiMaterial material))
            CanvasServer.Current.SetMaterial(item, material.Material);

        // A tweened fade tints it and everything under it.
        Color modulate = parent.Modulate;
        float opacity = UiTween.Pose(entity).Opacity;
        if (opacity != 1f)
            modulate = new Color(modulate, modulate.A * Math.Clamp(opacity, 0f, 1f));

        into.Item(item, transform, modulate, z, parent.Clip);

        Rect2? clip = parent.Clip;
        if (entity.TryGetComponent(out UiContainer container) && container.Clip)
        {
            Rect2 own = Bounds(transform, entity.GetComponent<Rectangle>().Bounds);
            clip = clip is { } outer ? outer.Intersection(own) : own;
        }

        var here = new Base(transform, modulate, z, clip);
        foreach (Entity child in entity.ChildEntities)
            Walk(into, child, here, layer);
    }

    private static bool IsDrawn(Entity entity) =>
        entity.HasComponent<VisualItem>() && entity.HasComponent<Drawable>() &&
        entity.HasComponent<Rectangle>() && entity.HasComponent<Position>();

    private static Rect2 Bounds(in Transform2D t, Rect2 rect)
    {
        Vector2 a = t * rect.Position, b = t * new Vector2(rect.End.X, rect.Position.Y);
        Vector2 c = t * rect.End, d = t * new Vector2(rect.Position.X, rect.End.Y);
        Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new Rect2(min, max - min);
    }

    /// <summary>Where an entity draws among its siblings' layer, over its parent's.</summary>
    public static int LocalZIndex(Entity entity)
    {
        if (entity.HasComponent<UiBackground>())
            return -1;

        // A dropdown floats above whatever it opens over, and its options above
        // it; nested content (a panel's text stacks its own z on its parent's)
        // must not draw over the list.
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

    private readonly record struct CollectLayers(EntityStore Store, List<Entity> Into) : IEachEntity<Layer, VisualItem>
    {
        public void Execute(ref Layer layer, ref VisualItem visual, int id) => Into.Add(Store.GetEntityById(id));
    }

    /// <summary>Drawn entities with no drawn parent and no layer above them: they hang under the layer their z names.</summary>
    private readonly record struct CollectLoose(EntityStore Store, Dictionary<int, List<Entity>> Into)
        : IEachEntity<Rectangle, Drawable, VisualItem, Position>
    {
        public void Execute(ref Rectangle rect, ref Drawable drawable, ref VisualItem visual, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);
            for (Entity node = entity.Parent; !node.IsNull; node = node.Parent)
            {
                if (node.HasComponent<Layer>() || node.HasComponent<VisualItem>())
                    return;
            }

            if (!Into.TryGetValue(drawable.ZIndex, out List<Entity>? list))
                Into[drawable.ZIndex] = list = new List<Entity>();
            list.Add(entity);
        }
    }
}

/// <summary>Not drawn, nor anything under it, while it is otherwise left alone (input, layout and systems still see it).</summary>
public struct CanvasHidden : ITag;
