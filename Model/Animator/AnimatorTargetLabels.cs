using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>What the animator calls a limb target in its rows and menus.</summary>
public static class AnimatorTargetLabels
{
    /// <summary>What the override is called on its bar and in the status line.</summary>
    public static string Name(RigLimbTarget target) =>
        target.Shape == RigTargetShape.Spot ? "PIN" : "ELLIPSE";
}
