using System;
using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Double-clicking a limb's art in the preview: whether a press is the
/// second of a double click, and which part's art is under the pointer.
/// </summary>
internal static class AnimatorArtPick
{
    /// <summary>Two presses this close in time and place are a double click.</summary>
    private const long DoubleMs = 350;

    private const float DoubleSlack = 6f;

    private static long _lastMs = long.MinValue / 2;
    private static Vector2 _lastAt;

    /// <summary>Notes a press; true when it finishes a double click (which it then uses up).</summary>
    public static bool DoubleClick(Vector2 pointer)
    {
        long now = Environment.TickCount64;
        bool twice = now - _lastMs <= DoubleMs && pointer.DistanceTo(_lastAt) <= DoubleSlack;
        _lastMs = twice ? long.MinValue / 2 : now;
        _lastAt = pointer;
        return twice;
    }

    /// <summary>
    /// The frontmost part whose showing art holds the pointer, by layer then
    /// part order as the art is drawn; -1 for none. Skinned parts are tested
    /// at their rigid place, which is close enough to find them.
    /// </summary>
    public static int PartAt(RigPackage package, RigPose pose, Transform2D[] attachments, Transform2D placement, Vector2 pointer)
    {
        int best = -1;
        int bestLayer = int.MinValue;
        for (int i = 0; i < package.Attachments.Count && i < attachments.Length; i++)
        {
            RigAttachment part = package.Attachments[i];
            RigAttachmentState state = i < pose.AttachmentStates.Length ? pose.AttachmentStates[i] : default;
            if (state.Alpha <= 0f)
                continue;
            int art = state.Art >= 0 && state.Art < part.ArtCount ? state.Art : 0;
            Vector2 local = (placement * attachments[i]).AffineInverse() * pointer;
            if (!Holds(part.Art(art), local))
                continue;
            int layer = state.LayerOf(part);
            if (layer >= bestLayer)
            {
                best = i;
                bestLayer = layer;
            }
        }
        return best;
    }

    private static bool Holds(XifGeometry geometry, Vector2 point)
    {
        foreach (XifPolygon polygon in geometry.Polygons)
        {
            List<Vector2> outline = polygon.Points;
            bool inside = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                Vector2 a = outline[i], b = outline[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            if (inside)
                return true;
        }
        return false;
    }
}
