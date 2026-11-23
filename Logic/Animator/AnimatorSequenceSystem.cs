using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;

namespace Xanadu.Logic.Animator;

/// <summary>Steps each session's clip chain along; it runs before playback so a hand-over starts on the same frame.</summary>
public sealed class AnimatorSequenceSystem : QuerySystem<AnimatorDocument, AnimatorSequence>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.ToEntityList())
            AnimatorSequenceCommands.Tick(session, delta * AnimatorFlowCommands.Speed(session));
    }
}
