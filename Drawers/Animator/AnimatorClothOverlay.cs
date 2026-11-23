using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Animator;

/// <summary>
/// Shows the selected cloth's structure over the preview: its grid, its
/// pinned points (bright), tethered ones (cyan; while pinning, with a line to
/// their anchor and a ring for their reach), free ones (small), and its
/// colliders as capsule outlines. Brighter while a pick mode waits for clicks, so it is clear what
/// a click will do. Nothing is drawn in preview mode.
/// </summary>
public sealed class AnimatorClothOverlay(IRectCanvas canvas, Handle target)
{
    private static readonly Color Grid = new(Palette.TextMuted, 0.35f);
    private static readonly Color Free = new(Palette.TextMuted, 0.8f);
    private static readonly Color Pinned = Palette.Accent;
    private static readonly Color Collider = new(Palette.Lime, 0.7f);
    private static readonly Color Picking = Palette.Warning;
    private static readonly Color Tethered = Palette.Cyan;
    private static readonly Color Reach = new(Palette.Cyan, 0.35f);

    public void Draw(Entity session, RigPackage package, Transform2D[] world, Transform2D placement)
    {
        int index = AnimatorSelected.Cloth(session);
        if (index < 0 || AnimatorPreviewMode.IsOn(session))
            return;

        RigCloth cloth = package.Cloths[index];
        AnimatorCloths cloths = session.GetComponent<AnimatorCloths>();
        IReadOnlyList<Vector2> points = cloths.Simulation.Points(index);
        if (points.Count != cloth.Count)
            return;

        bool pinning = cloths.Picking == AnimatorClothPick.Pins;
        bool colliding = cloths.Picking == AnimatorClothPick.Colliders;
        for (int row = 0; row < cloth.Rows; row++)
        {
            for (int column = 0; column < cloth.Columns; column++)
            {
                Vector2 at = placement * points[cloth.Index(column, row)];
                if (column + 1 < cloth.Columns)
                    canvas.DrawLine(target, at, placement * points[cloth.Index(column + 1, row)], Grid, 1f);
                if (row + 1 < cloth.Rows)
                    canvas.DrawLine(target, at, placement * points[cloth.Index(column, row + 1)], Grid, 1f);
            }
        }

        for (int row = 0; row < cloth.Rows; row++)
        {
            for (int column = 0; column < cloth.Columns; column++)
            {
                Vector2 at = placement * points[cloth.Index(column, row)];
                if (cloth.IsPinned(column, row))
                    canvas.DrawCircle(target, at, pinning ? 4.5f : 3.5f, Pinned);
                else if (cloth.IsTethered(column, row))
                    canvas.DrawCircle(target, at, pinning ? 4.5f : 3.5f, Tethered);
                else
                    canvas.DrawCircle(target, at, pinning ? 3f : 1.5f, pinning ? Picking : Free);
            }
        }

        float scale = placement.X.Length();
        if (pinning)
            DrawTethers(package, cloth, world, placement, points, scale);
        foreach (RigClothCollider collider in cloth.Colliders)
        {
            if (RigClothLayout.Capsule(package, collider, world) is not { } capsule)
                continue;

            DrawCapsule(placement * capsule.From, placement * capsule.To, collider.Radius * scale,
                colliding ? Picking : Collider);
        }
    }

    /// <summary>Each tether's anchor on its bone, its reach around that, and a line to the point.</summary>
    private void DrawTethers(RigPackage package, RigCloth cloth, Transform2D[] world, Transform2D placement,
        IReadOnlyList<Vector2> points, float scale)
    {
        if (cloth.Tethers.Count == 0)
            return;

        Transform2D[] rest = RigRig.WorldBones(package, RigClothLayout.RestPose(package));
        Vector2[] laidOut = RigClothLayout.RestPoints(package, cloth, rest);
        foreach (RigClothTether tether in cloth.Tethers)
        {
            int bone = RigRig.FindBone(package, tether.BoneId);
            int point = cloth.Index(tether.Column, tether.Row);
            if (bone < 0 || bone >= world.Length || point >= points.Count)
                continue;

            Vector2 anchor = placement * (world[bone] * (rest[bone].AffineInverse() * laidOut[point]));
            canvas.DrawCircleOutline(target, anchor, tether.Radius * scale, Reach, 1f, 24);
            canvas.DrawLine(target, anchor, placement * points[point], Reach, 1f);
        }
    }

    /// <summary>A capsule's outline: its two sides and a circle at each end.</summary>
    private void DrawCapsule(Vector2 from, Vector2 to, float radius, Color color)
    {
        Vector2 along = to - from;
        if (along.Length() > 0.5f)
        {
            Vector2 side = new Vector2(-along.Y, along.X).Normalized() * radius;
            canvas.DrawLine(target, from + side, to + side, color, 1.5f);
            canvas.DrawLine(target, from - side, to - side, color, 1.5f);
            canvas.DrawCircleOutline(target, to, radius, color, 1.5f, 24);
        }
        canvas.DrawCircleOutline(target, from, radius, color, 1.5f, 24);
    }
}
