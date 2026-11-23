using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>
/// Everything the editor, the history, the clipboard and the level document
/// need to know about one kind of placed item, in one place, so a kind
/// brought back is one class here, its component, its drawing and its file
/// record. Notes are the only kind for now.
/// </summary>
/// <remarks>
/// Every member has the answer most kinds give, so a kind overrides only what
/// it does differently. <see cref="ItemKind{T,TDoc}"/> does the parts that only
/// depend on the component and document types.
/// </remarks>
public abstract class ItemKind
{
    /// <summary>A name for messages and tests.</summary>
    public abstract string Name { get; }

    public abstract bool Has(Entity entity);

    /// <summary>Every item of this kind, as a list it is safe to change the store under.</summary>
    public abstract EntityList All(EntityStore entities);

    public void DeleteAll(EntityStore entities)
    {
        foreach (Entity entity in All(entities))
            entity.DeleteEntity();
    }

    // --- where it is --------------------------------------------------------

    /// <summary>Its centre on the playfield, the point groups and rotation move.</summary>
    public abstract Vector2 Position(Entity entity);

    public abstract void SetPosition(Entity entity, Vector2 position);

    /// <summary>Its time in the song, for the kinds that have one.</summary>
    public virtual long? Time(Entity entity) => null;

    public virtual bool SetTime(Entity entity, long timeMs) => false;

    /// <summary>The upright box it covers as it is turned; a point for the kinds with no shape.</summary>
    public virtual Rect2 Hull(Entity entity) => new(Position(entity), Vector2.Zero);

    // --- picking ------------------------------------------------------------

    /// <summary>
    /// Whether a playfield point (already moved back by the item's groups)
    /// picks it at the playhead.
    /// </summary>
    public abstract bool Hit(Entity entity, Vector2 point, long playheadMs);

    /// <summary>Whether a marquee, on screen, takes it.</summary>
    public abstract bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs);

    // --- copies -------------------------------------------------------------

    /// <summary>
    /// A copy of the item that undo and the clipboard keep: its own value,
    /// cloned where it shares anything by reference.
    /// </summary>
    public ItemCopy Copy(Entity entity) => new(this, Capture(entity));

    /// <summary>Makes the item a copy describes.</summary>
    public Entity Restore(EntityStore entities, in ItemCopy copy) => Create(entities, copy.Value);

    /// <summary>The item's own value, detached from the entity.</summary>
    protected abstract object Capture(Entity entity);

    /// <summary>Makes an item from a value <see cref="Capture"/> gave.</summary>
    protected abstract Entity Create(EntityStore entities, object value);

    /// <summary>Orders two values of this kind in a snapshot; equal values must compare 0.</summary>
    public abstract int Compare(object a, object b);

    /// <summary>Whether two values of this kind are the same item.</summary>
    public abstract bool Same(object a, object b);

    /// <summary>
    /// A clipboard value moved for a paste: timed kinds to <paramref name="timeMs"/>,
    /// geometry by <paramref name="offset"/>. Null when it can't go there.
    /// </summary>
    public abstract object? Pasted(object value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities);

    /// <summary>A copied value's time, for the kinds pasted at the playhead; null for geometry.</summary>
    public virtual long? TimeOf(object value) => null;

    // --- the level document -------------------------------------------------

    /// <summary>Writes every item of this kind into the document's list, in a stable order.</summary>
    public abstract void CaptureInto(EntityStore entities, LevelDocument level);

    /// <summary>Replaces every item of this kind with the document's.</summary>
    public abstract void ApplyFrom(EntityStore entities, LevelDocument level);
}

/// <summary>
/// One item as undo and the clipboard keep it: its kind and its own value.
/// Two copies are equal when their kind says their values are the same.
/// </summary>
public readonly struct ItemCopy(ItemKind kind, object value) : IEquatable<ItemCopy>
{
    public ItemKind Kind { get; } = kind;

    public object Value { get; } = value;

    public ItemCopy WithValue(object value) => new(Kind, value);

    public bool Equals(ItemCopy other) =>
        ReferenceEquals(Kind, other.Kind) && Kind.Same(Value, other.Value);

    public override bool Equals(object? obj) => obj is ItemCopy other && Equals(other);

    public override int GetHashCode() => Kind.GetHashCode();
}
