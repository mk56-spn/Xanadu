using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>
/// Everything the editor, the history, the clipboard, the draw list and the
/// level document need to know about one kind of placed item, in one place.
/// Code that used to ask <c>HasComponent&lt;Block&gt;() … HasComponent&lt;GrassPatch&gt;()</c>
/// asks <see cref="ItemKinds.Of"/> instead, so a new kind is one class here,
/// its component, its drawing and its file record.
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

    // --- defaults -----------------------------------------------------------

    /// <summary>The layer a new item of this kind goes on.</summary>
    public virtual int DefaultLayer => ItemLayer.Gameplay;

    /// <summary>The blend a new item of this kind starts with.</summary>
    public virtual BlendMode DefaultBlend => BlendMode.Mix;

    /// <summary>Whether the colour field's self modulate applies to it.</summary>
    public virtual bool Tintable => true;

    // --- where it is --------------------------------------------------------

    /// <summary>Its centre on the playfield, the point groups and rotation move.</summary>
    public abstract Vector2 Position(Entity entity);

    public abstract void SetPosition(Entity entity, Vector2 position);

    /// <summary>Its time in the song, for the kinds that have one.</summary>
    public virtual long? Time(Entity entity) => null;

    public virtual bool SetTime(Entity entity, long timeMs) => false;

    /// <summary>
    /// The box it is moved, resized and grid-snapped by, unturned, and its
    /// turn, for the kinds placed as boxes; false for the rest.
    /// </summary>
    public virtual bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        box = default;
        degrees = 0f;
        return false;
    }

    /// <summary>Gives a boxed item a new unturned box, leaving the rest of it alone.</summary>
    public virtual void SetBox(Entity entity, Rect2 box) { }

    /// <summary>Where a dragged box's top-left corner lands: on the grid when snapping, inside the field.</summary>
    public virtual Vector2 SettleDrag(Vector2 corner, Vector2 size, Playfield playfield, bool snap) =>
        Editor.GridPlacement.Settle(corner, size, playfield, snap);

    // --- turning ------------------------------------------------------------

    /// <summary>Whether a single one of it turns on the spot rather than about the selection.</summary>
    public virtual bool TurnsOnTheSpot => false;

    /// <summary>The upright box it covers as it is turned; a point for the kinds with no shape.</summary>
    public virtual Rect2 Hull(Entity entity) => new(Position(entity), Vector2.Zero);

    /// <summary>The point it turns about.</summary>
    public virtual Vector2 TurnCenter(Entity entity) => Position(entity);

    /// <summary>How far it is turned, clockwise, in degrees.</summary>
    public virtual float Degrees(Entity entity) => 0f;

    /// <summary>Its size before it is turned, which a quarter turn swaps.</summary>
    public virtual Vector2 Size(Entity entity) => Vector2.Zero;

    /// <summary>Puts a freely turning item's centre and turn; false for the kinds that turn otherwise.</summary>
    public virtual bool TryTurnFreely(Entity entity, Vector2 center, float degrees) => false;

    // --- picking ------------------------------------------------------------

    /// <summary>
    /// Whether a playfield point (already moved back by the item's groups)
    /// picks it at the playhead.
    /// </summary>
    public abstract bool Hit(Entity entity, Vector2 point, long playheadMs);

    /// <summary>Whether a marquee, on screen, takes it.</summary>
    public abstract bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs);

    // --- drawing ------------------------------------------------------------

    /// <summary>Its entry kind in the draw list, or null for a kind that adds its own entries.</summary>
    public virtual PlayfieldDrawKind? DrawKind => null;

    /// <summary>Adds every item of this kind to the draw list, walking the store's chunks so a frame allocates nothing.</summary>
    public abstract void AddDrawEntries(EntityStore entities, List<PlayfieldDrawEntry> into);

    /// <summary>Draws one item, in editor and play.</summary>
    public virtual void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) { }

    // --- copies -------------------------------------------------------------

    /// <summary>
    /// A copy of the item that undo and the clipboard keep: its own value
    /// (cloned where it shares anything by reference) and its header.
    /// </summary>
    public ItemCopy Copy(Entity entity) => new(this, Capture(entity), ItemHeader.Of(entity));

    /// <summary>
    /// Makes the item a copy describes, header and all. A system mid-update
    /// passes its <paramref name="buffer"/>, which the groups then go through.
    /// </summary>
    public Entity Restore(EntityStore entities, in ItemCopy copy, CommandBuffer? buffer = null)
    {
        Entity created = Create(entities, copy.Value, copy.Header);
        copy.Header.ApplyTo(created, Tintable, buffer);
        return created;
    }

    /// <summary>The item's own value, detached from the entity.</summary>
    protected abstract object Capture(Entity entity);

    /// <summary>Makes an item from a value <see cref="Capture"/> gave.</summary>
    protected abstract Entity Create(EntityStore entities, object value, in ItemHeader header);

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
    public abstract void CaptureInto(EntityStore entities, IChartItems chart);

    /// <summary>Replaces every item of this kind with the document's.</summary>
    public abstract void ApplyFrom(EntityStore entities, IChartItems chart);

    /// <summary>The document's items of this kind, for checks that run over every item.</summary>
    public abstract IEnumerable<ItemDocument> Documents(IChartItems chart);
}

/// <summary>What every item carries beside its own value: groups, shader, colour, layer, blend and shader source.</summary>
public readonly record struct ItemHeader(
    uint Groups,
    int Shader,
    uint Modulate,
    ItemLayer Layer,
    BlendMode Blend,
    int ShaderSource)
{
    public static ItemHeader Of(Entity entity) => new(
        entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u,
        ItemShader.Of(entity),
        SelfModulate.Packed(entity),
        ItemLayer.Of(entity),
        ItemBlend.Of(entity),
        ItemShaderSource.Of(entity));

    /// <summary>The header a document's item states.</summary>
    public static ItemHeader Of(ItemDocument document) => new(
        ItemGroups.FromList(document.Groups),
        document.Shader,
        document.SelfModulate,
        new ItemLayer(document.Layer, document.Z),
        document.Blend,
        document.ShaderSource);

    /// <summary>Gives a new item this header; the shader went in when it was made.</summary>
    public void ApplyTo(Entity item, bool tintable, CommandBuffer? buffer = null)
    {
        if (Groups != 0 && buffer is not null)
            buffer.AddComponent(item.Id, new ItemGroups(Groups));
        else if (Groups != 0)
            item.AddComponent(new ItemGroups(Groups));
        ItemLayer.Put(item, Layer);
        ItemBlend.Put(item, Blend);
        ItemShaderSource.Put(item, ShaderSource);
        if (tintable)
            SelfModulate.Put(item, Modulate);
    }

    /// <summary>Writes this header into a document's item.</summary>
    public void WriteTo(ItemDocument document)
    {
        document.Groups = ItemGroups.ToList(Groups);
        document.Shader = Shader;
        document.SelfModulate = Modulate;
        document.Layer = Layer.Layer;
        document.Z = Layer.Z;
        document.Blend = Blend;
        document.ShaderSource = ShaderSource;
    }
}

/// <summary>
/// One item as undo and the clipboard keep it: its kind, its own value and
/// its header. Two copies are equal when their kinds say their values are
/// the same and their headers match.
/// </summary>
public readonly struct ItemCopy(ItemKind kind, object value, ItemHeader header) : IEquatable<ItemCopy>
{
    public ItemKind Kind { get; } = kind;

    public object Value { get; } = value;

    public ItemHeader Header { get; } = header;

    public ItemCopy WithValue(object value) => new(Kind, value, Header);

    public bool Equals(ItemCopy other) =>
        ReferenceEquals(Kind, other.Kind) && Header == other.Header && Kind.Same(Value, other.Value);

    public override bool Equals(object? obj) => obj is ItemCopy other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Header);
}
