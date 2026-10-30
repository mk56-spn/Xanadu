using System;
using System.Collections.Generic;
using Godot;

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
}

/// <summary>The grid, its weights and its bending, as plain maths both the animator and the game draw with.</summary>
public static class XafSkin
{
    /// <summary>Weights under this share of a point are dropped, so a point follows only the bones that matter.</summary>
    private const float Least = 0.01f;

    /// <summary>Points across and down the grid for art of the given size.</summary>
    public static (int Columns, int Rows) Points(XafMesh mesh, Vector2 size)
    {
        int detail = Math.Clamp(mesh.Detail, XafMesh.MinDetail, XafMesh.MaxDetail);
        float longest = Math.Max(1f, Math.Max(size.X, size.Y));
        int across = Math.Max(2, (int)MathF.Round(detail * size.X / longest));
        int down = Math.Max(2, (int)MathF.Round(detail * size.Y / longest));
        return (across + 1, down + 1);
    }

    /// <summary>The grid's points over the art, in the attachment's own space, and where each samples the art.</summary>
    public static (Vector2[] Points, Vector2[] Uvs) Grid(XafMesh mesh, Rect2 art)
    {
        (int columns, int rows) = Points(mesh, art.Size);
        var points = new Vector2[columns * rows];
        var uvs = new Vector2[points.Length];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var uv = new Vector2(column / (float)(columns - 1), row / (float)(rows - 1));
                points[row * columns + column] = art.Position + art.Size * uv;
                uvs[row * columns + column] = uv;
            }
        }
        return (points, uvs);
    }

    public static int[] Triangles(XafMesh mesh, Vector2 size)
    {
        (int columns, int rows) = Points(mesh, size);
        return XafClothLayout.Triangles(columns, rows);
    }

    /// <summary>The bones a mesh's points may follow, as indices; its own bone comes first.</summary>
    public static List<int> Influences(XafPackage package, XafAttachment attachment)
    {
        var bones = new List<int>();
        int own = XafRig.FindBone(package, attachment.BoneId);
        if (own >= 0)
            bones.Add(own);

        if (attachment.Mesh is { Bones.Count: > 0 } mesh)
        {
            foreach (string id in mesh.Bones)
            {
                int bone = XafRig.FindBone(package, id);
                if (bone >= 0 && !bones.Contains(bone))
                    bones.Add(bone);
            }
            return bones;
        }

        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            if (package.Skeleton.Bones[i].Parent == own && own >= 0)
                bones.Add(i);
        }
        return bones;
    }

    /// <summary>
    /// Each point's weights over the influences, from the rest pose: a point
    /// follows the bone it lies closest to, and shares out over the ones about
    /// as close, within the blend width, so the bend is smooth across a joint.
    /// </summary>
    public static float[][] Weights(XafPackage package, IReadOnlyList<int> influences, Vector2[] restPoints,
        Transform2D[] restBones, float blend)
    {
        var weights = new float[restPoints.Length][];
        float width = Math.Max(0.5f, blend);
        var distances = new float[influences.Count];
        for (int p = 0; p < restPoints.Length; p++)
        {
            float nearest = float.MaxValue;
            for (int b = 0; b < influences.Count; b++)
            {
                distances[b] = Distance(package, restBones, influences[b], restPoints[p]);
                nearest = Math.Min(nearest, distances[b]);
            }

            var row = new float[influences.Count];
            float total = 0f;
            for (int b = 0; b < influences.Count; b++)
            {
                float over = (distances[b] - nearest) / width;
                row[b] = 1f / (1f + over * over * over * over);
                total += row[b];
            }

            float kept = 0f;
            for (int b = 0; b < row.Length; b++)
            {
                row[b] /= total;
                if (row[b] < Least)
                    row[b] = 0f;
                kept += row[b];
            }
            for (int b = 0; b < row.Length; b++)
                row[b] /= kept;
            weights[p] = row;
        }
        return weights;
    }

    /// <summary>
    /// Bends the rest points into the pose: each moves with each bone it
    /// follows as that bone moved from rest, mixed by weight. The attachment's
    /// own bone carries it as the attachment itself is posed, so keys on the
    /// attachment still move it.
    /// </summary>
    public static Vector2[] Deform(IReadOnlyList<int> influences, float[][] weights, Vector2[] restPoints,
        Transform2D[] restBones, Transform2D[] bones, Transform2D restAttachment, Transform2D attachment)
    {
        var moves = new Transform2D[influences.Count];
        for (int b = 0; b < influences.Count; b++)
        {
            int bone = influences[b];
            moves[b] = b == 0
                ? attachment * restAttachment.AffineInverse()
                : bones[bone] * restBones[bone].AffineInverse();
        }

        var points = new Vector2[restPoints.Length];
        for (int p = 0; p < restPoints.Length; p++)
        {
            Vector2 sum = Vector2.Zero;
            for (int b = 0; b < moves.Length; b++)
            {
                if (weights[p][b] > 0f)
                    sum += moves[b] * restPoints[p] * weights[p][b];
            }
            points[p] = sum;
        }
        return points;
    }

    /// <summary>
    /// How far a point is from a bone, mostly along it: how far past either end
    /// of the bone it lies, plus a little of how far to the side. A point beside
    /// the thigh just above the knee then counts as on the thigh, not half on
    /// the shin, so the blend sits on the joint and the inside of a bend does
    /// not pinch.
    /// </summary>
    private static float Distance(XafPackage package, Transform2D[] world, int bone, Vector2 point)
    {
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        Vector2 from = world[bone].Origin;
        Vector2 to = child >= 0 ? world[child].Origin : from;
        Vector2 along = to - from;
        float length = along.Length();
        if (length < 1e-4f)
            return from.DistanceTo(point);

        Vector2 axis = along / length;
        float t = (point - from).Dot(axis);
        float past = t < 0f ? -t : t > length ? t - length : 0f;
        float side = MathF.Abs((point - from).Cross(axis)) * SideShare;
        return MathF.Sqrt(past * past + side * side);
    }

    /// <summary>How much a point's distance to the side of a bone counts against its distance along it.</summary>
    private const float SideShare = 0.25f;
}
