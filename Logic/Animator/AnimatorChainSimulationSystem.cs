using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;

namespace Xanadu.Logic.Animator;

/// <summary>
/// Swings each session's Verlet chains and cloths. They hang from the bones as
/// the preview shows them, so dragging a bone, scrubbing or playing a clip all
/// set them swinging.
/// </summary>
public sealed class AnimatorChainSimulationSystem : QuerySystem<AnimatorDocument, AnimatorChains>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.Entities)
        {
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            ref AnimatorChains chains = ref session.GetComponent<AnimatorChains>();

            // A loaded or new document starts its chains hanging, not mid-swing.
            if (!ReferenceEquals(chains.Package, document.Package))
            {
                chains.Package = document.Package;
                chains.Simulation.Reset();
            }

            ref AnimatorCloths cloths = ref session.GetComponent<AnimatorCloths>();
            if (!ReferenceEquals(cloths.Package, document.Package))
            {
                cloths.Package = document.Package;
                cloths.Simulation.Reset();
            }

            RigPose pose = AnimatorGeometry.PreviewPose(document, AnimatorModes.Of(session));
            Transform2D[] world = RigRig.WorldBones(document.Package, pose);
            // Slowed with the bones, so hair and cloth keep up with them in slow motion.
            float step = delta * AnimatorFlowCommands.Speed(session);
            chains.Simulation.Update(document.Package, world, step);
            cloths.Simulation.Update(document.Package, world, step);
        }
    }
}
