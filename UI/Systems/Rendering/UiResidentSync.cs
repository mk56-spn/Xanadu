using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// Keeps the UI's drawn entities in a host <see cref="IResidentTree"/>
/// (Docs/host.md §5.3), so the host places and orders them on the GPU and
/// a quiet frame walks no tree.
/// </summary>
/// <remarks>
/// Two speeds. <b>Structure</b> (which entities draw, their parents, z and
/// order) changes only through Friflo's own events: tags, components added or
/// removed, children, entities made or deleted; any of them marks a restructure,
/// the one full walk, done the next frame. <b>Values</b> (positions, poses, tween
/// fades, layer fades, container clips) are written through <c>ref</c> by
/// layout and tweens, which raises no event, so every frame a flat scan over
/// the component chunks compares them with what was last sent and sends only
/// the differences.
/// </remarks>
internal sealed class UiResidentSync
{
    private static readonly HashSet<Type> Structural =
    [
        typeof(Layer), typeof(VisualItem), typeof(Drawable), typeof(Rectangle), typeof(Position), typeof(Text),
        typeof(ButtonVisual), typeof(DropdownOption), typeof(UiContainer), typeof(UiRotation), typeof(UiPose),
        typeof(UiTweens), typeof(Opacity),
    ];

    /// <summary>Components whose value (not just presence) is structure: the z of layers and loose entities.</summary>
    private static readonly HashSet<Type> StructuralValues = [typeof(Layer), typeof(Drawable)];

    private static readonly Tags StructuralTags = MakeTags();

    private readonly EntityStore _store;
    private readonly ArchetypeQuery<Layer, VisualItem> _layers;
    private readonly ArchetypeQuery<Rectangle, Drawable, VisualItem, Position> _drawn;
    private readonly ArchetypeQuery<Rectangle, Drawable, VisualItem, Position> _drawnAll;
    private readonly ArchetypeQuery<Rectangle, Drawable, VisualItem, Position> _plain;
    private readonly ArchetypeQuery<Rectangle, Drawable, VisualItem, Position> _posed;
    private readonly ArchetypeQuery<UiContainer, Rectangle, VisualItem> _containers;
    private readonly ArchetypeQuery<UiMaterial, VisualItem> _materials;

    private IResidentTree? _tree;
    private bool _restructure = true;
    private int _lastDrawnCount = -1, _lastLayerCount = -1;

    /// <summary>Each entity's slot, by entity id; -1 for none.</summary>
    private int[] _slotOf = [];

    /// <summary>By slot: the entity, the item it was made for, the node last sent, and the restructure it was last seen in.</summary>
    private Entity[] _entityOf = [];
    private Handle[] _itemOf = [];
    private ResidentNode[] _sent = [];
    private int[] _seen = [];
    private int _stamp;

    /// <summary>The origin last sent for each entity, by entity id (NaN for none), so the position scan reads two flat arrays.</summary>
    private Vector2[] _originOf = [];

    private readonly List<Entity> _layerOrder = [];
    private readonly Dictionary<int, List<Entity>> _loose = [];
    private readonly List<int> _order = [];
    private readonly List<int> _z = [];

    public UiResidentSync(EntityStore store)
    {
        _store = store;
        _layers = store.Query<Layer, VisualItem>();
        _drawn = store.Query<Rectangle, Drawable, VisualItem, Position>();
        _drawnAll = store.Query<Rectangle, Drawable, VisualItem, Position>().WithDisabled();
        // Layers are placed by the walk, never by their position.
        _plain = store.Query<Rectangle, Drawable, VisualItem, Position>()
            .WithoutAnyComponents(ComponentTypes.Get<UiRotation, UiPose, UiTweens, Layer>());
        _posed = store.Query<Rectangle, Drawable, VisualItem, Position>()
            .AnyComponents(ComponentTypes.Get<UiRotation, UiPose, UiTweens>())
            .WithoutAnyComponents(ComponentTypes.Get<Layer>());
        _containers = store.Query<UiContainer, Rectangle, VisualItem>();
        _materials = store.Query<UiMaterial, VisualItem>();

        store.OnTagsChanged += changed =>
        {
            if (changed.ChangedTags.HasAny(StructuralTags))
                _restructure = true;
        };
        store.OnComponentAdded += Component;
        store.OnComponentRemoved += Component;
        store.OnChildEntitiesChanged += _ => _restructure = true;
        store.OnEntityCreate += _ => _restructure = true;
        store.OnEntityDelete += _ => _restructure = true;
    }

    private void Component(ComponentChanged changed)
    {
        Type type = changed.ComponentType.Type;
        if (changed.Action == ComponentChangedAction.Update ? StructuralValues.Contains(type) : Structural.Contains(type))
            _restructure = true;
    }

    /// <summary>Brings <paramref name="tree"/> up to date for this frame.</summary>
    public void Sync(IResidentTree tree, ICanvasCollector timing)
    {
        if (_tree != tree)
        {
            _tree = tree;
            _restructure = true;
        }

        // A safety net for changes that slip past the events (batches, command buffers).
        int drawn = _drawnAll.Count, layers = _layers.Count;
        if (drawn != _lastDrawnCount || layers != _lastLayerCount)
        {
            _lastDrawnCount = drawn;
            _lastLayerCount = layers;
            _restructure = true;
        }

        if (_restructure)
        {
            timing.Begin(MountStep.Restructure);
            _restructure = false;
            Restructure(tree);
            return;
        }

        timing.Begin(MountStep.Scan);
        Scan(tree);
    }

    // ---- Structure ----------------------------------------------------------

    private void Restructure(IResidentTree tree)
    {
        _stamp++;
        _order.Clear();
        _z.Clear();
        _layerOrder.Clear();
        _layers.EachEntity(new CollectLayers(_store, _layerOrder));
        _layerOrder.Sort(static (a, b) =>
        {
            int z = a.GetComponent<Layer>().ZIndex.CompareTo(b.GetComponent<Layer>().ZIndex);
            return z != 0 ? z : a.Id.CompareTo(b.Id);
        });

        foreach (List<Entity> list in _loose.Values)
            list.Clear();
        _drawn.EachEntity(new CollectLoose(_store, _loose));

        var placed = new HashSet<int>();
        foreach (Entity layer in _layerOrder)
        {
            if (layer.Tags.Has<CanvasHidden>())
                continue;

            int z = layer.GetComponent<Layer>().ZIndex;
            int slot = Place(tree, layer, LayerNode(layer), z);
            foreach (Entity child in layer.ChildEntities)
                Walk(tree, child, slot, z, slot, z);

            // Loose entities hang under the first layer of their z.
            if (placed.Add(z) && _loose.TryGetValue(z, out List<Entity>? loose))
            {
                foreach (Entity entity in loose)
                    Walk(tree, entity, slot, z, slot, z);
            }
        }

        for (int slot = 0; slot < _seen.Length; slot++)
        {
            if (_seen[slot] == 0 || _seen[slot] == _stamp)
                continue;

            Drop(tree, slot);
        }

        tree.SetOrder(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_order),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_z));
    }

    private void Walk(IResidentTree tree, Entity entity, int parent, int parentZ, int layer, int layerZ)
    {
        if (!entity.Enabled || entity.Tags.Has<CanvasHidden>() || entity.HasComponent<Layer>())
            return;

        if (!IsDrawn(entity))
        {
            // Its children hang under the layer, as they would without it.
            foreach (Entity child in entity.ChildEntities)
                Walk(tree, child, layer, layerZ, layer, layerZ);
            return;
        }

        int z = parentZ + UiCanvasMount.LocalZIndex(entity);
        int slot = Place(tree, entity, EntityNode(entity, parent), z);
        foreach (Entity child in entity.ChildEntities)
            Walk(tree, child, slot, z, layer, layerZ);
    }

    /// <summary>The entity's slot (made, or remade when its item changed), its node sent if it changed, and its place in the order.</summary>
    private int Place(IResidentTree tree, Entity entity, in ResidentNode node, int z)
    {
        Handle item = entity.GetComponent<VisualItem>().Value;
        int slot = SlotOf(entity);
        if (slot >= 0 && _itemOf[slot] != item)
        {
            Drop(tree, slot);
            slot = -1;
        }

        if (slot < 0)
        {
            slot = tree.Add(item);
            Grow(slot);
            Grow(ref _slotOf, entity.Id + 1, -1);
            _slotOf[entity.Id] = slot;
            _entityOf[slot] = entity;
            _itemOf[slot] = item;
            _sent[slot] = node;
            tree.Set(slot, node);
        }
        else
        {
            Send(tree, slot, node);
        }

        Grow(ref _originOf, entity.Id + 1, new Vector2(float.NaN));
        _originOf[entity.Id] = node.Local.Origin;

        _seen[slot] = _stamp;
        _order.Add(slot);
        _z.Add(z);
        return slot;
    }

    private void Drop(IResidentTree tree, int slot)
    {
        Entity entity = _entityOf[slot];
        if (entity.Id < _slotOf.Length && _slotOf[entity.Id] == slot)
            _slotOf[entity.Id] = -1;
        if (entity.Id < _originOf.Length)
            _originOf[entity.Id] = new Vector2(float.NaN);
        _entityOf[slot] = default;
        _itemOf[slot] = default;
        _seen[slot] = 0;
        tree.Remove(slot);
    }

    private int SlotOf(Entity entity) => entity.Id < _slotOf.Length ? _slotOf[entity.Id] : -1;

    private void Grow(int slot)
    {
        int size = slot + 1;
        Grow(ref _entityOf, size, default);
        Grow(ref _itemOf, size, default);
        Grow(ref _sent, size, default);
        Grow(ref _seen, size, 0);
    }

    private static void Grow<T>(ref T[] array, int size, T fill)
    {
        if (array.Length >= size)
            return;

        int old = array.Length;
        Array.Resize(ref array, Math.Max(size, old * 2));
        array.AsSpan(old).Fill(fill);
    }

    private void Send(IResidentTree tree, int slot, in ResidentNode node)
    {
        if (_sent[slot].Same(node))
            return;

        _sent[slot] = node;
        tree.Set(slot, node);
    }

    private static ResidentNode LayerNode(Entity layer) => new()
    {
        Parent = ResidentTree.NoParent,
        Local = Transform2D.Identity,
        Opacity = layer.TryGetComponent(out Opacity fade) ? fade.Value : 1f,
    };

    private static ResidentNode EntityNode(Entity entity, int parent)
    {
        bool clips = entity.TryGetComponent(out UiContainer container) && container.Clip;
        return new ResidentNode
        {
            Parent = parent,
            Local = UiTransform.Local(entity, entity.GetComponent<Position>().Value),
            Opacity = Math.Clamp(UiTween.Pose(entity).Opacity, 0f, 1f),
            Clips = clips,
            Clip = clips ? entity.GetComponent<Rectangle>().Bounds : default,
        };
    }

    private static bool IsDrawn(Entity entity) =>
        entity.HasComponent<VisualItem>() && entity.HasComponent<Drawable>() &&
        entity.HasComponent<Rectangle>() && entity.HasComponent<Position>();

    private static Tags MakeTags()
    {
        var tags = new Tags();
        tags.Add<Disabled>();
        tags.Add<CanvasHidden>();
        tags.Add<UiBackground>();
        tags.Add<DropdownListPanel>();
        tags.Add<UiOverlayItem>();
        tags.Add<UiOverlay>();
        tags.Add<UiSliderFill>();
        tags.Add<UiRaised>();
        tags.Add<TakesInput>();
        return tags;
    }

    // ---- Values -------------------------------------------------------------

    /// <summary>The per-frame values, compared with what was sent; only differences go to the host.</summary>
    private void Scan(IResidentTree tree)
    {
        ScanPositions(tree);
        _posed.EachEntity(new ScanPosed(this, tree));
        _containers.EachEntity(new ScanClips(this, tree));
        _layers.EachEntity(new ScanLayers(this, tree));
        _materials.Each(new ScanMaterials());
    }

    /// <summary>
    /// Entities placed by their position alone: a moved one sends its new place.
    /// The hot loop of a quiet frame, so it walks the position chunks against
    /// the flat array of last-sent origins and touches a slot only on a change.
    /// </summary>
    private void ScanPositions(IResidentTree tree)
    {
        foreach (var (_, _, _, positions, entities) in _plain.Chunks)
        {
            Span<Position> at = positions.Span;
            ReadOnlySpan<int> ids = entities.Ids;
            for (int i = 0; i < at.Length; i++)
            {
                int id = ids[i];
                Vector2 now = at[i].Value;
                if (id >= _originOf.Length || _originOf[id] == now)
                    continue;

                int slot = _slotOf[id];
                if (slot < 0)
                    continue;

                ResidentNode node = _sent[slot];
                node.Local = new Transform2D(0, now);
                _sent[slot] = node;
                _originOf[id] = now;
                tree.Set(slot, node);
            }
        }
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
                Into[drawable.ZIndex] = list = [];
            list.Add(entity);
        }
    }

    /// <summary>Turned, posed or tweened entities: their whole local place and fade, worked out again.</summary>
    private readonly record struct ScanPosed(UiResidentSync Sync, IResidentTree Tree)
        : IEachEntity<Rectangle, Drawable, VisualItem, Position>
    {
        public void Execute(ref Rectangle rect, ref Drawable drawable, ref VisualItem visual, ref Position position, int id)
        {
            int slot = id < Sync._slotOf.Length ? Sync._slotOf[id] : -1;
            if (slot < 0)
                return;

            Entity entity = Sync._store.GetEntityById(id);
            ResidentNode node = Sync._sent[slot];
            node.Local = UiTransform.Local(entity, position.Value);
            node.Opacity = Math.Clamp(UiTween.Pose(entity).Opacity, 0f, 1f);
            Sync.Send(Tree, slot, node);
        }
    }

    private readonly record struct ScanClips(UiResidentSync Sync, IResidentTree Tree) : IEachEntity<UiContainer, Rectangle, VisualItem>
    {
        public void Execute(ref UiContainer container, ref Rectangle rect, ref VisualItem visual, int id)
        {
            int slot = id < Sync._slotOf.Length ? Sync._slotOf[id] : -1;
            if (slot < 0)
                return;

            ResidentNode node = Sync._sent[slot];
            node.Clips = container.Clip;
            node.Clip = container.Clip ? rect.Bounds : default;
            Sync.Send(Tree, slot, node);
        }
    }

    private readonly record struct ScanLayers(UiResidentSync Sync, IResidentTree Tree) : IEachEntity<Layer, VisualItem>
    {
        public void Execute(ref Layer layer, ref VisualItem visual, int id)
        {
            int slot = id < Sync._slotOf.Length ? Sync._slotOf[id] : -1;
            if (slot >= 0)
                Sync.Send(Tree, slot, LayerNode(Sync._store.GetEntityById(id)));
        }
    }

    private readonly struct ScanMaterials : IEach<UiMaterial, VisualItem>
    {
        public void Execute(ref UiMaterial material, ref VisualItem visual) =>
            CanvasServer.Current.SetMaterial(visual.Value, material.Material);
    }
}
