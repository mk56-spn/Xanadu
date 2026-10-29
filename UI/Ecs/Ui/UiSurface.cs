using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// The state every <see cref="UiNode"/> shares: the store, the layer it draws
/// on, and the low-level builder that creates the entities. A node holds one
/// reference to its surface, so children can be created without threading the
/// store through every call site.
/// </summary>
/// <remarks>
/// The surface owns one <see cref="UiBuilder"/> used only to create entities;
/// it is not safe to build from two threads at once, which matches the frame
/// building this replaces.
/// </remarks>
public sealed class UiSurface
{
    internal UiSurface(EntityStore store, UiBuilder builder, Entity layer)
    {
        Store = store;
        Builder = builder;
        Layer = layer;
    }

    /// <summary>The store the tree lives in.</summary>
    public EntityStore Store { get; }

    /// <summary>The layer entity the tree hangs from.</summary>
    public Entity Layer { get; }

    /// <summary>The layer as a node, for building the tree's outermost children.</summary>
    public UiNode Root => new(this, Layer);

    internal UiBuilder Builder { get; }
}
