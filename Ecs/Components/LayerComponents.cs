using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Layering;

namespace Xanadu.Ecs;

/// <summary>
/// The layer a placed item draws on and its z inside that layer. Layers run
/// −200…−1 behind the player wedge and 1…200 in front of it; there is no layer
/// 0. Every editor item carries one from the moment it is made
/// (<see cref="EditorItems"/>), so setting it never has to add a component.
/// </summary>
public struct ItemLayer(int layer, int z) : IComponent, IEquatable<ItemLayer>
{
    /// <summary>Deco shapes, furthest back.</summary>
    public const int Polygons = -3;

    /// <summary>Blocks and spikes: collidable terrain behind the gameplay.</summary>
    public const int Terrain = -2;

    /// <summary>Notes, rails, mines and start points: behind the player, in front of terrain.</summary>
    public const int Gameplay = -1;

    /// <summary>Grass, in front of the blocks it grows on.</summary>
    public const int Grass = 1;

    /// <summary>Texts, readable over everything.</summary>
    public const int Texts = 2;

    public int Layer = layer;
    public int Z = z;

    /// <summary>The layer an item of this kind is placed on.</summary>
    public static int DefaultFor<T>() where T : struct, IComponent
    {
        Type kind = typeof(T);
        if (kind == typeof(LevelPolygon))
            return Polygons;
        if (kind == typeof(Block) || kind == typeof(Spike))
            return Terrain;
        if (kind == typeof(GrassPatch))
            return Grass;
        if (kind == typeof(LevelText))
            return Texts;
        return Gameplay;
    }

    /// <summary>The layer an existing item's kind is placed on.</summary>
    public static int DefaultFor(Entity entity)
    {
        if (entity.HasComponent<LevelPolygon>())
            return Polygons;
        if (entity.HasComponent<Block>() || entity.HasComponent<Spike>())
            return Terrain;
        if (entity.HasComponent<GrassPatch>())
            return Grass;
        if (entity.HasComponent<LevelText>())
            return Texts;
        return Gameplay;
    }

    /// <summary>The item's layer and z, or its kind's default layer at z 0.</summary>
    public static ItemLayer Of(Entity entity) =>
        entity.TryGetComponent(out ItemLayer layer) ? layer : new ItemLayer(DefaultFor(entity), 0);

    /// <summary>
    /// A layer that exists: clamped to the range, and <paramref name="fallback"/>
    /// for 0, which is where the player wedge sits and also what an item read
    /// from a file without layers has.
    /// </summary>
    public static int Valid(int layer, int fallback) =>
        layer == 0 ? fallback : DrawOrder.ClampLayer(layer);

    /// <summary>Sets an item's layer and z; a 0 layer means its kind's default.</summary>
    public static void Put(Entity entity, ItemLayer value)
    {
        var valid = new ItemLayer(Valid(value.Layer, DefaultFor(entity)), value.Z);
        if (entity.HasComponent<ItemLayer>())
            entity.GetComponent<ItemLayer>() = valid;
        else
            entity.AddComponent(valid);
    }

    /// <summary>
    /// Puts a newly placed item in front of everything already on its layer,
    /// so newer items draw over older ones.
    /// </summary>
    public static void OnTop(EntityStore entities, Entity item) => OnTop(entities, [item]);

    /// <summary>
    /// Puts newly placed items in front of everything else on their layers,
    /// keeping their order among themselves, as a paste does.
    /// </summary>
    public static void OnTop(EntityStore entities, IReadOnlyCollection<Entity> items)
    {
        if (items.Count == 0)
            return;

        var placed = new HashSet<int>();
        foreach (Entity item in items)
            placed.Add(item.Id);

        // The highest z already on each layer, and the lowest among the placed.
        var top = new Dictionary<int, int>();
        foreach (Entity entity in entities.Query<ItemLayer>().Entities)
        {
            if (placed.Contains(entity.Id))
                continue;
            ItemLayer layer = entity.GetComponent<ItemLayer>();
            top[layer.Layer] = top.TryGetValue(layer.Layer, out int z) ? Math.Max(z, layer.Z) : layer.Z;
        }

        var bottom = new Dictionary<int, int>();
        foreach (Entity item in items)
        {
            ItemLayer layer = Of(item);
            bottom[layer.Layer] = bottom.TryGetValue(layer.Layer, out int z) ? Math.Min(z, layer.Z) : layer.Z;
        }

        foreach (Entity item in items)
        {
            ItemLayer layer = Of(item);
            if (!top.TryGetValue(layer.Layer, out int highest))
                continue;
            Put(item, layer with { Z = layer.Z - bottom[layer.Layer] + highest + 1 });
        }
    }

    public readonly bool Equals(ItemLayer other) => Layer == other.Layer && Z == other.Z;

    public override readonly bool Equals(object? obj) => obj is ItemLayer other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Layer, Z);

    public static bool operator ==(ItemLayer a, ItemLayer b) => a.Equals(b);

    public static bool operator !=(ItemLayer a, ItemLayer b) => !a.Equals(b);
}
