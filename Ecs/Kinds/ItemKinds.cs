using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>
/// Every kind of placed item. Adding a kind is adding it here: everything that
/// treats items alike — selection, moving, resizing, turning, picking, undo,
/// copy and paste, drawing, saving — loops over <see cref="All"/>.
/// </summary>
public static class ItemKinds
{
    public static readonly NoteItemKind Notes = new();
    public static readonly MineItemKind Mines = new();
    public static readonly StartPointItemKind StartPoints = new();
    public static readonly BlockItemKind Blocks = new();
    public static readonly SpikeItemKind Spikes = new();
    public static readonly TextItemKind Texts = new();
    public static readonly GrassItemKind Grass = new();
    public static readonly FlareItemKind Flares = new();
    public static readonly PolygonItemKind Polygons = new();
    public static readonly ScreenReadItemKind ScreenReads = new();

    /// <summary>
    /// Every kind, in the order items are made when a level loads or an undo
    /// lands; later items draw over earlier ones on the same layer and z.
    /// </summary>
    public static readonly ItemKind[] All =
    {
        Notes, Mines, StartPoints, Blocks, Spikes, Texts, Grass, Flares, Polygons, ScreenReads,
    };

    /// <summary>
    /// The order a click looks for an item in: timed marks first, then what
    /// sits over terrain, then terrain, then polygons and regions, which sit
    /// under everything.
    /// </summary>
    public static readonly ItemKind[] PickOrder =
    {
        Notes, Mines, StartPoints, Texts, Grass, Flares, Spikes, Blocks, Polygons, ScreenReads,
    };

    private static readonly Dictionary<Type, ItemKind> ByComponent = new()
    {
        [typeof(Note)] = Notes,
        [typeof(Mine)] = Mines,
        [typeof(StartPoint)] = StartPoints,
        [typeof(Block)] = Blocks,
        [typeof(Spike)] = Spikes,
        [typeof(LevelText)] = Texts,
        [typeof(GrassPatch)] = Grass,
        [typeof(LevelFlare)] = Flares,
        [typeof(LevelPolygon)] = Polygons,
        [typeof(ScreenReadRegion)] = ScreenReads,
    };

    private static readonly Dictionary<PlayfieldDrawKind, ItemKind> ByDrawKind = BuildDrawKinds();

    /// <summary>The kind an entity is, or null when it is not a placed item.</summary>
    public static ItemKind? Of(Entity entity)
    {
        if (entity.IsNull)
            return null;

        foreach (ItemKind kind in All)
        {
            if (kind.Has(entity))
                return kind;
        }

        return null;
    }

    /// <summary>The kind whose component is <typeparamref name="T"/>, or null.</summary>
    public static ItemKind? For<T>() where T : struct, IComponent =>
        ByComponent.TryGetValue(typeof(T), out ItemKind? kind) ? kind : null;

    /// <summary>The kind that draws a draw-list entry kind, or null for entries that are not one kind's items.</summary>
    public static ItemKind? Drawing(PlayfieldDrawKind drawKind) =>
        ByDrawKind.TryGetValue(drawKind, out ItemKind? kind) ? kind : null;

    private static Dictionary<PlayfieldDrawKind, ItemKind> BuildDrawKinds()
    {
        var kinds = new Dictionary<PlayfieldDrawKind, ItemKind>();
        foreach (ItemKind kind in All)
        {
            if (kind.DrawKind is { } drawKind)
                kinds[drawKind] = kind;
        }

        return kinds;
    }
}
