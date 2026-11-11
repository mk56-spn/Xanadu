using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// An attachment drawn as a mesh instead of a rigid picture: a grid over its
/// art whose points each follow the bones near them, so a thigh's art bends
/// round the knee with the shin instead of meeting it at a hard seam. The
/// weights are not stored; they come from the rest pose every time, so they
/// always fit the rig as it is.
/// </summary>
public sealed class XafMesh
{
    public const int MinDetail = 1;
    public const int MaxDetail = 32;
    public const float MaxBlend = 500f;

    /// <summary>Cells along the art's longer side; the shorter side gets as many as keep them square, at least two.</summary>
    public int Detail = 8;

    /// <summary>
    /// The bones the points may follow. Empty means the attachment's own bone
    /// and the bones hanging from it, which is what a limb segment wants.
    /// </summary>
    public List<string> Bones = new();

    /// <summary>
    /// How wide, in pixels, the zone is where a point follows two bones at once;
    /// wider bends more softly.
    /// </summary>
    public float Blend = 10f;

    /// <summary>
    /// The mesh traced from the art with its stored weights, which is drawn in
    /// place of the grid when there is one (see <see cref="XafSkinAuthoring"/>).
    /// </summary>
    public XafSkinMesh? Skin;
}
