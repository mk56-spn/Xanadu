using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator;
using Xanadu.Model.Animator;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Moves time on: sequences, the attack cycle, clip playback and foot planting.</summary>
public sealed class AnimatorPlaybackSystemGroup : SystemGroup
{
    public AnimatorPlaybackSystemGroup(IAnimatorClick? click) : base("Animator playback")
    {
        Add(new AnimatorSequenceSystem());
        Add(new AnimatorAttackCycleSystem(click));
        Add(new AnimatorPlaybackSystem());
        Add(new AnimatorPlantSystem());
    }
}
