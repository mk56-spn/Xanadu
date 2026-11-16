using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Rendering;

namespace Xanadu.Ui.Systems.Groups;

/// <summary>Creates layer hierarchy, resolves container layout, and initialises drawables.</summary>
public sealed class InitializationSystemGroup : SystemGroup
{
    public InitializationSystemGroup(Handle parentCanvas) : base("Initialization")
    {
        Add(new LayerParentSystem(parentCanvas));
        Add(new LayerOpacitySystem());
        Add(new ContainerLayoutSystem());
        Add(new UiAnchorSystem());
        Add(new DrawableInitialiseSystem());
    }
}
