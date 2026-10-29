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

/// <summary>
/// The live simulation of the package's cloths, rebuilt with a new document
/// as the chains are, and what clicks in the preview are for while a cloth
/// inspector button waits for them.
/// </summary>
public struct AnimatorCloths : IComponent
{
    public XafClothSimulation Simulation;
    public XafPackage? Package;
    public AnimatorClothPick Picking;
}

/// <summary>What clicks in the preview do for the selected cloth.</summary>
public enum AnimatorClothPick
{
    None,

    /// <summary>The next bone clicked becomes the one the cloth is laid out from.</summary>
    Bone,

    /// <summary>Each point clicked is pinned to the bone nearest it, or freed; until stopped.</summary>
    Pins,

    /// <summary>Each bone clicked starts or stops being a collider; until stopped.</summary>
    Colliders,
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
