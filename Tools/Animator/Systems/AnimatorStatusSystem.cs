using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Shows the session status message in the header text entity.</summary>
public sealed class AnimatorStatusSystem : QuerySystem<AnimatorStatus, AnimatorUi>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity session in Query.ToEntityList())
        {
            ShellStatus.Show(
                session.GetComponent<AnimatorUi>().StatusLabels,
                session.GetComponent<AnimatorStatus>().Message,
                buffer);
        }
    }
}
