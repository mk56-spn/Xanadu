using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Outlines the selected SVG's mesh where it is drawn, so its bending can be
/// seen, and while bones are being picked for it rings the ones that bend it.
/// Nothing is drawn in preview mode.
/// </summary>
public sealed class AnimatorMeshOverlay(IRectCanvas canvas, Rid target)
{
    private static readonly Color Wire = new(Palette.Cyan, 0.45f);
    private static readonly Color Bending = Palette.Warning;

    public void Draw(Entity session, IAttachmentVisuals visuals, XafPackage package, Transform2D[] world,
        Transform2D placement)
    {
        int index = session.GetComponent<AnimatorSelection>().Attachment;
        if (index < 0 || index >= package.Attachments.Count || AnimatorPreviewModeCommands.IsOn(session) ||
            package.Attachments[index].Mesh is null)
        {
            return;
        }

        if (visuals.TryMesh(index, out Vector2[] points, out int[] triangles))
        {
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector2 a = points[triangles[t]];
                Vector2 b = points[triangles[t + 1]];
                Vector2 c = points[triangles[t + 2]];
                canvas.DrawLine(target, a, b, Wire, 1f);
                canvas.DrawLine(target, b, c, Wire, 1f);
                canvas.DrawLine(target, c, a, Wire, 1f);
            }
        }

        if (!session.GetComponent<AnimatorMeshPick>().Active)
            return;
        foreach (int bone in XafSkin.Influences(package, package.Attachments[index]))
        {
            if (bone < world.Length)
                canvas.DrawCircleOutline(target, placement * world[bone].Origin, 11f, Bending, 2f, 24);
        }
    }
}
