using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// The stand-in puppet (<see cref="Puppet"/>): its rig's Xif parts as posed,
/// drawn by <see cref="XifDrawer"/> like any Xif. It fills the body's box,
/// feet on its bottom edge, mirrored when facing left.
/// </summary>
public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Draws the puppet round <paramref name="center"/> (the body's centre on
    /// screen), faded by <paramref name="alpha"/> and flashing white by <paramref name="flash"/>.
    /// </summary>
    public static void DrawPuppet(IRectCanvas canvas, Handle rid, in Puppet puppet, Vector2 center, float scale, float alpha, float flash)
    {
        if (puppet.Package is not { } package || puppet.Pose is not { } pose || puppet.Bones.Length != package.Skeleton.Bones.Count)
            return;

        // Rig space has its root at the feet: the body's bottom edge.
        float side = puppet.FacingLeft ? -1f : 1f;
        var placement = new Transform2D(side * scale, 0f, 0f, scale, center.X, center.Y + PlayRules.BodyHeight / 2f * scale);
        var tint = new XifTint(new Color(1f, 1f, 1f, alpha), Colors.White, flash * 0.8f);
        XifDrawer.Shared.DrawRig(canvas, rid, package, pose, puppet.Bones, puppet.Parts, null, null, placement, scale, tint);
    }
}
