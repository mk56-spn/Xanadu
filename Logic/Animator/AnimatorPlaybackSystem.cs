using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;

namespace Xanadu.Logic.Animator;

/// <summary>Advances the runtime of each animator session, slowed while SLOW is on.</summary>
public sealed class AnimatorPlaybackSystem : QuerySystem<AnimatorDocument>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.ToEntityList())
            session.GetComponent<AnimatorDocument>().Playback.Update(delta * AnimatorFlowCommands.Speed(session));
    }
}
