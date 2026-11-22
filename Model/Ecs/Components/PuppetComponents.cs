using Friflo.Engine.ECS;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The player's bone puppet as posed this frame: the rig, and each bone's
/// place in rig space (root at the feet, facing right, rig units = playfield
/// units). Written by the puppet system from the body's state; the drawer
/// draws the bones in place of the plain body.
/// </summary>
public struct Puppet() : IComponent
{
    public XafPackage? Package;

    /// <summary>Every bone's world transform in rig space, in the package's bone order.</summary>
    public Transform2D[] Bones = [];

    /// <summary>Facing left: drawn mirrored.</summary>
    public bool FacingLeft;

    /// <summary>The clip it is in (or fading to), for checking.</summary>
    public string Clip = "";
}
