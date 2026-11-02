using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Holds planted feet in place after the frame's drags and playback have set the pose, before it is drawn.</summary>
public sealed class AnimatorPlantSystem : QuerySystem<AnimatorPlants, AnimatorDocument>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            AnimatorPlantCommands.Hold(session);
    }
}
