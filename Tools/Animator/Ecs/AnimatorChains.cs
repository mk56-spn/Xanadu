using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The live Verlet simulation of the package's chains, so the preview shows
/// them swinging. The package it was built for is kept so a new document
/// starts its chains hanging instead of inheriting the old one's motion.
/// </summary>
public struct AnimatorChains : IComponent
{
    public XafChainSimulation Simulation;
    public XafPackage? Package;

    /// <summary>Which bone the selected chain is waiting to be given by the next click, if any.</summary>
    public AnimatorChainPick Picking;
}

/// <summary>What the next bone clicked is for.</summary>
public enum AnimatorChainPick
{
    None,

    /// <summary>The bone the chain hangs from.</summary>
    Anchor,

    /// <summary>The bone the chain's collider follows.</summary>
    Collider,
}
