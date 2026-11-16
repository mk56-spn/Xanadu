using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>Starts the frame's draw list empty.</summary>
internal sealed class DrawBeginSystem(DrawList list) : BaseSystem
{
    protected override void OnUpdateGroup() => list.Begin();
}
