using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Layout;
using Writer.Systems.Rendering;

namespace Writer.Systems.Groups;

/// <summary>Creates layer hierarchy, resolves container layout, and initialises drawables.</summary>
public sealed class InitializationSystemGroup : SystemGroup
{
    public InitializationSystemGroup(Rid parentCanvas) : base("Initialization")
    {
        Add(new LayerParentSystem(parentCanvas));
        Add(new ContainerLayoutSystem());
        Add(new DrawableInitialiseSystem());
    }
}
