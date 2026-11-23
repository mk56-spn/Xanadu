using Friflo.Engine.ECS;

namespace Xanadu.Core;

/// <summary>Marks an entity whose visual state changed and needs a redraw.</summary>
public struct Dirty : ITag { }

/// <summary>Marks a layer that still needs its canvas item parented.</summary>
public struct Uninitialised : ITag { }

/// <summary>Marks a drawable that participates in pointer hit testing.</summary>
public struct TakesInput : ITag { }

/// <summary>Marks a button currently under the pointer.</summary>
public struct Hovered : ITag { }

/// <summary>Marks the drawable currently holding the pointer button.</summary>
public struct Pressed : ITag { }

/// <summary>Marks a container whose child layout needs recomputing.</summary>
public struct LayoutDirty : ITag { }

/// <summary>A layer whose <c>Opacity</c> has not reached its canvas item yet.</summary>
public struct LayerOpacityDirty : ITag { }

/// <summary>Excludes a child from its container's stack while keeping it alive.</summary>
public struct LayoutIgnore : ITag { }

/// <summary>
/// Authored: this node and everything under it are not shown. Set it through
/// <c>UiLayout.SetHidden</c>; <c>UiTreeEvents</c> keeps the native
/// <see cref="Disabled"/> tag on the subtree, which takes it out of every query.
/// </summary>
public struct UiHidden : ITag { }

/// <summary>
/// Marks a debug view and everything under it, so that what the debug views report
/// leaves out the views themselves. Tag the root; children inherit it.
/// </summary>
public struct DebugUi : ITag { }

/// <summary>A greyed-out control: still drawn and laid out, but it takes no input or focus.</summary>
public struct UiDisabled : ITag { }

/// <summary>Marks a canvas item whose visibility must follow its entity's enabled state.</summary>
public struct VisibilityDirty : ITag { }

/// <summary>Marks a drawable whose canvas transform needs updating.</summary>
public struct Moved : ITag { }

/// <summary>
/// A dialog, menu, popup or panel that is showing. It sits on the thing's own
/// root entity; whoever draws it shows or hides it by this tag.
/// </summary>
public struct Open : ITag { }

/// <summary>Reading and setting <see cref="Open"/> on an entity that may be null.</summary>
public static class OpenTag
{
    public static bool IsOpen(this Entity entity) => !entity.IsNull && entity.Tags.Has<Open>();

    public static void SetOpen(this Entity entity, bool open) => entity.SetTag<Open>(open);

    public static void ToggleOpen(this Entity entity) => entity.SetOpen(!entity.IsOpen());
}

/// <summary>
/// Turning tags on and off from a bool, doing nothing when they already match.
/// Adding or removing a tag moves the entity to another archetype: a <c>ref</c>
/// to one of its components taken before the change no longer points at it.
/// </summary>
public static class TagSwitch
{
    public static void SetTag<T>(this Entity entity, bool on)
        where T : struct, ITag
    {
        if (entity.IsNull || entity.Tags.Has<T>() == on)
            return;
        if (on)
            entity.AddTag<T>();
        else
            entity.RemoveTag<T>();
    }

    public static void SetTags(this Entity entity, in Tags tags, bool on)
    {
        if (entity.IsNull || entity.Tags.HasAll(tags) == on)
            return;
        if (on)
            entity.AddTags(tags);
        else
            entity.RemoveTags(tags);
    }
}
