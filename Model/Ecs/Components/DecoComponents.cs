using Friflo.Engine.ECS;
using Xanadu.Core.Items;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A placed Xif that only looks: grass, a flare, anything the level puts on
/// the field for show. What it is comes from its Xif's modules (geometry,
/// shader, instancer), never from a per-kind component
/// (<see cref="Playfields.Decos"/> has the stock ones). Play never notices it.
/// </summary>
public struct Deco(Vector2 position, Xif xif) : IComponent
{
    /// <summary>The Xif's origin on the field, playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>What is drawn there, in its own space around <see cref="Position"/>.</summary>
    public Xif Xif = xif;
}

/// <summary>
/// The level's groups, on the level's own entity, and what they lay over each
/// item this frame (by item id), worked out once a frame by whoever draws.
/// </summary>
public struct LevelGroups(System.Collections.Generic.List<Xanadu.Model.Levels.GroupDocument> groups) : IComponent
{
    public System.Collections.Generic.List<Xanadu.Model.Levels.GroupDocument> Groups = groups;

    public System.Collections.Generic.Dictionary<string, Xanadu.Model.Levels.GroupLayer> Layers = [];
}
