using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Steps each session's attack cycle along and sounds its metronome; it runs
/// before playback so an attack fires on the frame of its click. A cycle just
/// started sounds its first beat here too.
/// </summary>
public sealed class AnimatorAttackCycleSystem(IAnimatorClick? click = null)
    : QuerySystem<AnimatorDocument, AnimatorAttackCycle>
{
    private bool _wasActive;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.ToEntityList())
        {
            bool started = session.GetComponent<AnimatorAttackCycle>().Active && !_wasActive;
            AnimatorBeat beat = AnimatorAttackCycleCommands.Tick(session, delta * AnimatorFlowCommands.Speed(session));
            _wasActive = session.GetComponent<AnimatorAttackCycle>().Active;
            if (started && _wasActive)
                beat = AnimatorBeat.Hit;
            if (beat != AnimatorBeat.None)
                click?.Click(beat == AnimatorBeat.Hit);
        }
    }
}
