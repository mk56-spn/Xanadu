using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Rendering;

namespace Xanadu.Systems.Groups;

/// <summary>Creates layer hierarchy, resolves container layout, and initialises drawables.</summary>
public sealed class InitializationSystemGroup : SystemGroup
{
    public InitializationSystemGroup(Rid parentCanvas) : base("Initialization")
    {
        Add(new LayerParentSystem(parentCanvas));
        Add(new ContainerLayoutSystem());
        Add(new UiAnchorSystem());
        Add(new DrawableInitialiseSystem());
    }
}
