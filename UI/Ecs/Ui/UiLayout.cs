using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>Helpers for participating in native ECS container layout.</summary>
public static class UiLayout
{
    /// <summary>
    /// Puts an entity's centre at <paramref name="position"/> and tags it
    /// <see cref="Moved"/>, but only when that changes it; true when it moved.
    /// </summary>
    public static bool Move(Entity entity, Vector2 position)
    {
        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return false;

        current.Value = position;
        entity.AddTag<Moved>();
        return true;
    }

    /// <summary>
    /// Shows or hides a node and its subtree: hidden nodes leave every query, the
    /// canvas and their parent's stack. A node hidden on its own stays hidden
    /// when an ancestor is shown again.
    /// </summary>
    public static void SetHidden(Entity entity, bool hidden)
    {
        if (entity.IsNull || entity.Tags.Has<UiHidden>() == hidden)
            return;

        UiTreeEvents.Ensure(entity.Store);
        if (hidden)
            entity.AddTag<UiHidden>();
        else
            entity.RemoveTag<UiHidden>();
    }

    /// <summary>Buffered variant for use inside system queries.</summary>
    public static void SetHidden(Entity entity, CommandBuffer buffer, bool hidden)
    {
        if (entity.IsNull || entity.Tags.Has<UiHidden>() == hidden)
            return;

        UiTreeEvents.Ensure(entity.Store);
        if (hidden)
            buffer.AddTag<UiHidden>(entity.Id);
        else
            buffer.RemoveTag<UiHidden>(entity.Id);
    }

    /// <summary>Fades a layer and everything drawn under it. 1 is opaque, and the default.</summary>
    public static void SetLayerOpacity(Entity layer, float opacity)
    {
        if (layer.TryGetComponent(out LayerOpacity current) && current.Value == opacity)
            return;

        layer.AddComponent(new LayerOpacity(opacity));
        layer.AddTag<LayerOpacityDirty>();
    }

    /// <summary>A layer's opacity; 1 when it was never faded.</summary>
    public static float LayerOpacityOf(Entity layer) =>
        layer.TryGetComponent(out LayerOpacity opacity) ? opacity.Value : 1f;

    /// <summary>True when this node itself was hidden, whatever its ancestors do.</summary>
    public static bool IsHidden(Entity entity) => entity.Tags.Has<UiHidden>();

    /// <summary>Disables or enables a control, dropping any interaction state it held.</summary>
    public static void SetDisabled(Entity entity, bool disabled)
    {
        if (entity.IsNull)
            return;

        if (disabled)
        {
            if (entity.Tags.Has<UiDisabled>())
                return;

            entity.AddTag<UiDisabled>();
            entity.RemoveTag<Focused>();
            entity.RemoveTag<Hovered>();
            entity.RemoveTag<Pressed>();
            entity.RemoveTag<TextFieldFocused>();
        }
        else
        {
            entity.RemoveTag<UiDisabled>();
        }
    }

    /// <summary>Buffered variant for use inside system queries.</summary>
    public static void SetDisabled(
        Entity entity,
        CommandBuffer buffer,
        bool disabled)
    {
        if (entity.IsNull)
            return;

        if (disabled)
        {
            if (entity.Tags.Has<UiDisabled>())
                return;

            buffer.AddTag<UiDisabled>(entity.Id);
            buffer.RemoveTag<Focused>(entity.Id);
            buffer.RemoveTag<Hovered>(entity.Id);
            buffer.RemoveTag<Pressed>(entity.Id);
            buffer.RemoveTag<TextFieldFocused>(entity.Id);
        }
        else
        {
            buffer.RemoveTag<UiDisabled>(entity.Id);
        }
    }

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

    /// <summary>
    /// Deletes an entity and everything parented under it. <c>DeleteEntity</c>
    /// alone leaves the children behind, still alive and still hit-testable.
    /// </summary>
    public static void DeleteSubtree(Entity entity)
    {
        if (entity.IsNull)
            return;

        var subtree = new EntityList(entity.Store);
        subtree.AddTree(entity);
        foreach (Entity member in subtree)
            member.DeleteEntity();
    }

    /// <summary>Deletes everything under an entity and keeps the entity.</summary>
    public static void DeleteChildren(Entity entity)
    {
        var children = new List<Entity>(entity.ChildCount);
        foreach (Entity child in entity.ChildEntities)
            children.Add(child);
        foreach (Entity child in children)
            DeleteSubtree(child);
    }

    /// <summary>
    /// Puts a child at an index under a parent, moving it from any other parent.
    /// Use this, not <c>Entity.InsertChild</c>: a tag added to the parent from a
    /// child-changed event in the middle of an insert silently loses the child
    /// (Friflo 4.0.0-preview.2), and <see cref="UiTreeEvents"/> does exactly that.
    /// </summary>
    public static void MoveChild(Entity parent, Entity child, int index)
    {
        if (child.Parent == parent && parent.GetChildIndex(child) == index)
            return;

        var order = new List<Entity>(parent.ChildCount + 1);
        foreach (Entity sibling in parent.ChildEntities)
        {
            if (sibling != child)
                order.Add(sibling);
        }
        order.Insert(index, child);

        for (int i = index; i < order.Count; i++)
        {
            if (order[i].Parent == parent)
                parent.RemoveChild(order[i]);
            parent.AddChild(order[i]);
        }
    }

    /// <summary>
    /// A node's laid-out box in its layer's space (design pixels for a full-stage
    /// layer). Non-UI drawing that lives in a container slot reads its room here,
    /// and follows it when it changes, rather than computing it.
    /// </summary>
    public static Rect2 Box(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 at = entity.TryGetComponent(out Position position) ? position.Value : Vector2.Zero;
        return new Rect2(UiTransform.WorldPosition(entity, at) + bounds.Position, bounds.Size);
    }

    /// <summary>True once no container above or at the node is waiting to be laid out, so <see cref="Box"/> is current.</summary>
    public static bool Settled(Entity entity)
    {
        for (Entity at = entity; !at.IsNull; at = at.Parent)
        {
            if (at.Tags.Has<LayoutDirty>())
                return false;
        }

        return true;
    }

    /// <summary>True when a container's stack places the node, so nothing else should move it.</summary>
    public static bool InFlow(Entity entity)
    {
        Entity parent = entity.Parent;
        return !parent.IsNull && parent.HasComponent<UiContainer>() && !entity.Tags.Has<LayoutIgnore>();
    }

    private static void MarkParentDirty(Entity entity)
    {
        Entity parent = entity.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            parent.AddTag<LayoutDirty>();
    }
}
