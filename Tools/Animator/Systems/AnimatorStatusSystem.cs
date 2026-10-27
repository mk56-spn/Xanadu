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
            Entity label = session.GetComponent<AnimatorUi>().Status;
            string message = session.GetComponent<AnimatorStatus>().Message ?? "";
            if (label.IsNull || !label.HasComponent<Text>())
                continue;

            ref Text text = ref label.GetComponent<Text>();
            if (text.Value == message)
                continue;

            text.Value = message;
            buffer.AddTag<Dirty>(label.Id);
        }
    }
}
