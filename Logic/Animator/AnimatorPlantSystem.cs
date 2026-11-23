using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;

namespace Xanadu.Logic.Animator;

/// <summary>Holds planted feet in place after the frame's drags and playback have set the pose, before it is drawn.</summary>
public sealed class AnimatorPlantSystem : QuerySystem<AnimatorPlants, AnimatorDocument>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            AnimatorPlantCommands.Hold(session);
    }
}
