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
    private static readonly Color Cold = new("#2563eb");
    private static readonly Color Hot = new("#ef4444");
    private static readonly Color Brush = new(Colors.White, 0.8f);

    /// <summary>
    /// While painting: each skin point where it is drawn, from blue (none of the
    /// painted bone) to red (all of it), the painted bone ringed, and the brush.
    /// </summary>
    private void DrawWeights(IAttachmentVisuals visuals, XafPackage package, int index, AnimatorWeightPaint paint,
        Transform2D[] world, Transform2D placement)
    {
        if (package.Attachments[index].Mesh?.Skin is not { } skin ||
            !visuals.TryMesh(index, out Vector2[] points, out _) || points.Length != skin.Weights.Length)
        {
            return;
        }

        int slot = paint.Bone >= 0 && paint.Bone < package.Skeleton.Bones.Count
            ? skin.Bones.IndexOf(package.Skeleton.Bones[paint.Bone].Id)
            : -1;
        for (int i = 0; i < points.Length; i++)
        {
            float weight = slot >= 0 ? skin.Weights[i][slot] : 0f;
            canvas.DrawCircle(target, points[i], 2.5f, Cold.Lerp(Hot, weight));
        }

        if (paint.Bone >= 0 && paint.Bone < world.Length)
            canvas.DrawCircleOutline(target, placement * world[paint.Bone].Origin, 11f, Bending, 2f, 24);
        canvas.DrawCircleOutline(target, paint.Pointer, paint.Radius, Brush, 1.5f, 40);
    }

    public void Draw(Entity session, IAttachmentVisuals visuals, XafPackage package, Transform2D[] world,
        Transform2D placement)
    {
        AnimatorWeightPaint paint = session.GetComponent<AnimatorWeightPaint>();
        int index = paint.Active ? paint.Attachment : session.GetComponent<AnimatorSelection>().Attachment;
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

        if (paint.Active)
            DrawWeights(visuals, package, index, paint, world, placement);

        if (!session.GetComponent<AnimatorMeshPick>().Active)
            return;
        foreach (int bone in XafSkin.Influences(package, package.Attachments[index]))
        {
            if (bone < world.Length)
                canvas.DrawCircleOutline(target, placement * world[bone].Origin, 11f, Bending, 2f, 24);
        }
    }
}
