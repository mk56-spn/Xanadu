using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Ecs;

/// <summary>Helpers for participating in native ECS container layout.</summary>
public static class UiLayout
{
    /// <summary>Off-screen position used by panels that are not visible.</summary>
    public static readonly Vector2 Hidden = new(-10_000, -10_000);

    /// <summary>Includes or excludes an entity from its parent's stack.</summary>
    public static void SetIgnored(Entity entity, bool ignored)
    {
        if (ignored)
            entity.AddTag<LayoutIgnore>();
        else
            entity.RemoveTag<LayoutIgnore>();

        MarkParentDirty(entity);
    }

    /// <summary>Buffered variant for use inside system queries.</summary>
    public static void SetIgnored(
        Entity entity,
        CommandBuffer buffer,
        bool ignored)
    {
        if (ignored)
            buffer.AddTag<LayoutIgnore>(entity.Id);
        else
            buffer.RemoveTag<LayoutIgnore>(entity.Id);
    }

    /// <summary>Hides an entity and removes it from its parent's layout.</summary>
    public static void Hide(Entity entity, CommandBuffer buffer)
    {
        if (entity.IsNull)
            return;

        if (entity.HasComponent<Position>())
        {
            ref Position position = ref entity.GetComponent<Position>();
            if (position.Value != Hidden)
            {
                position.Value = Hidden;
                buffer.AddTag<Moved>(entity.Id);
            }
        }

        SetIgnored(entity, buffer, true);
        MarkParentDirty(entity, buffer);
    }

    /// <summary>
    /// Deletes an entity and everything parented under it. <c>DeleteEntity</c>
    /// alone leaves the children behind, still alive and still hit-testable.
    /// </summary>
    public static void DeleteSubtree(Entity entity)
    {
        var children = new List<Entity>();
        foreach (Entity child in entity.ChildEntities)
            children.Add(child);
        foreach (Entity child in children)
            DeleteSubtree(child);
        entity.DeleteEntity();
    }

    private static void MarkParentDirty(Entity entity)
    {
        Entity parent = entity.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            parent.AddTag<LayoutDirty>();
    }

    private static void MarkParentDirty(Entity entity, CommandBuffer buffer)
    {
        Entity parent = entity.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            buffer.AddTag<LayoutDirty>(parent.Id);
    }
}
