using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Rendering;

namespace Xanadu.Ui.Systems.Groups;

/// <summary>Resolves container layout and anchors. Where things draw is worked out each frame by <see cref="UiCanvasMount"/>.</summary>
public sealed class InitializationSystemGroup : SystemGroup
{
    public InitializationSystemGroup() : base("Initialization")
    {
        Add(new ContainerLayoutSystem());
        Add(new UiAnchorSystem());
    }
}
