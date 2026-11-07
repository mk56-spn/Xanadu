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
    /// <summary>Both Splitline rail lanes with their holds, at the back of the backmost rail note's layer.</summary>
    RailLanes,
    Polygon,
    Block,
    Spike,
    Grass,
    Flare,
    Text,

    /// <summary>A boundary that refreshes the back buffer where the draw order reaches it.</summary>
    ScreenRead,

    /// <summary>The guide lines between notes in hit order, just under the backmost note.</summary>
    NoteGuides,
    Note,
    RailNote,
    Mine,
    StartPoint,
    NotePop,
    DashLinkFade,
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
    /// <param name="railLanes">Whether the Splitline rail lanes draw at all this frame.</param>
    public static void Gather(
        EntityStore entities,
        List<PlayfieldDrawEntry> into,
        bool startPoints,
        bool railLanes)
    {
        into.Clear();
        foreach (ItemKind kind in ItemKinds.All)
        {
            if (kind.DrawKind != PlayfieldDrawKind.StartPoint || startPoints)
                kind.AddDrawEntries(entities, into);
        }

        AddNotes(entities, into);
        AddEffects<NotePop>(entities, into, PlayfieldDrawKind.NotePop);
        AddEffects<DashLinkFade>(entities, into, PlayfieldDrawKind.DashLinkFade);
        AddEffects<DashTrail>(entities, into, PlayfieldDrawKind.DashTrail);
        AddEffects<HitTrail>(entities, into, PlayfieldDrawKind.HitTrail);
        into.Sort();

        // The two entries that stand for several things go under the backmost of them.
        int firstNote = into.FindIndex(e => e.Kind == PlayfieldDrawKind.Note);
        if (firstNote >= 0)
        {
            PlayfieldDrawEntry note = into[firstNote];
            into.Insert(firstNote, note with { Kind = PlayfieldDrawKind.NoteGuides, Entity = default });
        }

        if (railLanes)
        {
            int firstRail = into.FindIndex(e => e.Kind == PlayfieldDrawKind.RailNote);
            int layer = firstRail >= 0 ? into[firstRail].Layer : ItemLayer.Gameplay;
            var lanes = new PlayfieldDrawEntry(layer, int.MinValue, PlayfieldDrawKind.RailLanes, default);
            int at = into.FindIndex(e => e.CompareTo(lanes) > 0);
            into.Insert(at < 0 ? into.Count : at, lanes);
        }
    }

    /// <summary>Notes, and rail notes apart from them; channel ends are chart data and draw nothing.</summary>
    private static void AddNotes(EntityStore entities, List<PlayfieldDrawEntry> into)
    {
        foreach (var (notes, chunk) in entities.Query<Note>().Chunks)
        {
            Span<Note> span = notes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                NoteKind kind = span[i].Kind;
                if (NoteRules.IsChannelEnd(kind))
                    continue;

                Entity entity = chunk.EntityAt(i);
                ItemLayer layer = ItemLayer.Of(entity);
                PlayfieldDrawKind drawKind = NoteRules.IsRail(kind) ? PlayfieldDrawKind.RailNote : PlayfieldDrawKind.Note;
                into.Add(new PlayfieldDrawEntry(layer.Layer, layer.Z, drawKind, entity));
            }
        }
    }

    private static void AddEffects<T>(EntityStore entities, List<PlayfieldDrawEntry> into, PlayfieldDrawKind kind)
        where T : struct, IComponent
    {
        foreach (var (_, chunk) in entities.Query<T>().Chunks)
        {
            foreach (Entity entity in chunk)
            {
                EffectLayer layer = EffectLayer.Of(entity);
                into.Add(new PlayfieldDrawEntry(layer.Layer, layer.Z, kind, entity));
            }
        }
    }
}
