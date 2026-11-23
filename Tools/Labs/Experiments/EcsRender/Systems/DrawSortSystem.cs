using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender.Systems;

/// <summary>Puts the frame in draw order; nothing to do when the extract systems happened to find it that way.</summary>
internal sealed class DrawSortSystem(DrawList list) : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        if (!list.InOrder)
            list.Keys.AsSpan(0, list.Count).Sort();
    }
}
