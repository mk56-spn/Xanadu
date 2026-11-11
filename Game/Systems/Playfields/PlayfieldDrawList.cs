using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Layering;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// What one entry of the playfield's draw list is. The order here also breaks
/// ties between entries on the same layer and z, so a level where everything
/// sits at z 0 keeps the old stacking by kind; effects come after every item,
/// which puts each one just above the item it came from.
/// </summary>
public enum PlayfieldDrawKind : byte
{
    Polygon,
    Block,
    Spike,
    FrictionBar,
    Grass,
    Flare,
    Text,

    /// <summary>A boundary that refreshes the back buffer where the draw order reaches it.</summary>
    ScreenRead,

    /// <summary>The guide lines between notes in hit order, just under the backmost note.</summary>
    NoteGuides,
    Note,
    Mine,
    StartPoint,
    NotePop,
    DashTrail,
    HitTrail,
}

/// <summary>One thing to draw, where it sits: layer, then z, then kind, then entity id (list order).</summary>
public readonly record struct PlayfieldDrawEntry(int Layer, int Z, PlayfieldDrawKind Kind, Entity Entity)
    : IComparable<PlayfieldDrawEntry>
{
    /// <summary>Back to front.</summary>
    public int CompareTo(PlayfieldDrawEntry other)
    {
        int slot = new DrawSlot(Layer, Z, (int)Kind).CompareTo(new DrawSlot(other.Layer, other.Z, (int)other.Kind));
        return slot != 0 ? slot : Entity.Id.CompareTo(other.Entity.Id);
    }
}

/// <summary>
/// Gathers every item and effect on the playfield into one list sorted back to
/// front (docs/render-architecture.md §3). The player wedge isn't in it: it
/// draws between the last negative layer and the first positive one.
/// </summary>
public static class PlayfieldDrawList
{
    /// <param name="startPoints">Whether start points draw; only the editor shows them.</param>
    public static void Gather(
        EntityStore entities,
        List<PlayfieldDrawEntry> into,
        bool startPoints)
    {
        into.Clear();
        foreach (ItemKind kind in ItemKinds.All)
        {
            if (kind.DrawKind != PlayfieldDrawKind.StartPoint || startPoints)
                kind.AddDrawEntries(entities, into);
        }

        AddNotes(entities, into);
        AddEffects<NotePop>(entities, into, PlayfieldDrawKind.NotePop);
        AddEffects<DashTrail>(entities, into, PlayfieldDrawKind.DashTrail);
        AddEffects<HitTrail>(entities, into, PlayfieldDrawKind.HitTrail);
        into.Sort();

        // The guides stand for every note, so they go under the backmost one.
        int firstNote = into.FindIndex(e => e.Kind == PlayfieldDrawKind.Note);
        if (firstNote >= 0)
        {
            PlayfieldDrawEntry note = into[firstNote];
            into.Insert(firstNote, note with { Kind = PlayfieldDrawKind.NoteGuides, Entity = default });
        }
    }

    /// <summary>Every note, on its own layer.</summary>
    private static void AddNotes(EntityStore entities, List<PlayfieldDrawEntry> into)
    {
        foreach (var (notes, chunk) in entities.Cached<Note>().Chunks)
        {
            Span<Note> span = notes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                Entity entity = chunk.EntityAt(i);
                ItemLayer layer = ItemLayer.Of(entity);
                into.Add(new PlayfieldDrawEntry(layer.Layer, layer.Z, PlayfieldDrawKind.Note, entity));
            }
        }
    }

    private static void AddEffects<T>(EntityStore entities, List<PlayfieldDrawEntry> into, PlayfieldDrawKind kind)
        where T : struct, IComponent
    {
        foreach (var (_, chunk) in entities.Cached<T>().Chunks)
        {
            foreach (Entity entity in chunk)
            {
                EffectLayer layer = EffectLayer.Of(entity);
                into.Add(new PlayfieldDrawEntry(layer.Layer, layer.Z, kind, entity));
            }
        }
    }
}
