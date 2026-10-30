using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Keeps the UI tree's derived state in step with the native tree. Parents hear
/// about children coming and going, and <see cref="UiHidden"/> becomes the native
/// <see cref="Disabled"/> tag across the subtree, which every query already skips.
/// Handlers run on structural changes, which never happen inside a query loop.
/// </summary>
public static class UiTreeEvents
{
    private static readonly ConditionalWeakTable<EntityStore, object> Attached = new();

    /// <summary>Attaches the handlers to a store, once.</summary>
    public static void Ensure(EntityStore store)
    {
        if (Attached.TryGetValue(store, out _))
            return;

        Attached.Add(store, new object());
        store.OnTagsChanged += OnTagsChanged;
        store.OnChildEntitiesChanged += OnChildEntitiesChanged;
    }

    private static void OnTagsChanged(TagsChanged changed)
    {
        Entity entity = changed.Entity;
        if (changed.ChangedTags.Has<UiHidden>())
            Sync(entity);

        if (!changed.ChangedTags.Has<Disabled>())
            return;

        if (entity.HasComponent<VisualRid>())
            entity.AddTag<VisibilityDirty>();
        MarkLayoutDirty(entity.Parent);
    }

    private static void OnChildEntitiesChanged(ChildEntitiesChanged changed)
    {
        MarkLayoutDirty(changed.Entity);

        if (changed.Action == ChildEntitiesChangedAction.Add)
            Sync(changed.Store.GetEntityById(changed.ChildId));
    }

    /// <summary>A node is enabled unless it, or an ancestor, is hidden.</summary>
    private static void Sync(Entity entity)
    {
        Entity parent = entity.Parent;
        bool enabled = !entity.Tags.Has<UiHidden>() && (parent.IsNull || parent.Enabled);
        if (entity.Enabled == enabled)
            return;

        entity.Enabled = enabled;
        foreach (Entity child in entity.ChildEntities)
            Sync(child);
    }

    private static void MarkLayoutDirty(Entity container)
    {
        if (!container.IsNull && container.HasComponent<UiContainer>())
            container.AddTag<LayoutDirty>();
    }
}
