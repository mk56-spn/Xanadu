using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the selected cloth's structure over the preview: its grid, its
/// pinned points (bright) and free ones (small), and its colliders as capsule
/// outlines. Brighter while a pick mode waits for clicks, so it is clear what
/// a click will do. Nothing is drawn in preview mode.
/// </summary>
public sealed class AnimatorClothOverlay(IRectCanvas canvas, Rid target)
{
    private static readonly Color Grid = new(Palette.TextMuted, 0.35f);
    private static readonly Color Free = new(Palette.TextMuted, 0.8f);
    private static readonly Color Pinned = Palette.Accent;
    private static readonly Color Collider = new(Palette.Lime, 0.7f);
    private static readonly Color Picking = Palette.Warning;

    public void Draw(Entity session, XafPackage package, Transform2D[] world, Transform2D placement)
    {
        int index = AnimatorClothCommands.Selected(session);
        if (index < 0 || AnimatorPreviewModeCommands.IsOn(session))
            return;

        XafCloth cloth = package.Cloths[index];
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
                else
                    canvas.DrawCircle(target, at, pinning ? 3f : 1.5f, pinning ? Picking : Free);
            }
        }

        float scale = placement.X.Length();
        foreach (XafClothCollider collider in cloth.Colliders)
        {
            if (XafClothLayout.Capsule(package, collider, world) is not { } capsule)
                continue;

            DrawCapsule(placement * capsule.From, placement * capsule.To, collider.Radius * scale,
                colliding ? Picking : Collider);
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
