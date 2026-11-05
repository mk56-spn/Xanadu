using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>Which bones a skin point follows, and by how much, from its rest-pose distances.</summary>
public static class XafSkinWeights
{
    /// <summary>Weights under this share of a point are dropped, so a point follows only the bones that matter.</summary>
    private const float Least = 0.01f;

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
        var distances = new float[influences.Count];
        for (int p = 0; p < restPoints.Length; p++)
        {
            for (int b = 0; b < influences.Count; b++)
                distances[b] = XafSkinBones.Distance(package, restBones, influences[b], restPoints[p], influences);
            weights[p] = Share(distances, blend);
        }
        return weights;
    }

    /// <summary>
    /// Weights as <see cref="Weights(XafPackage, IReadOnlyList{int}, Vector2[], Transform2D[], float)"/>
    /// gives them, but with each distance measured through the art rather than
    /// across the canvas: a path over the covered cells of the grid, from where
    /// the bone runs through the art. A point on one leg is then far from the
    /// other leg's bones however near it sits across the gap, and a bone that
    /// does not run through the art at all only reaches it from its nearest
    /// point. Points on no covered cell are not drawn and keep the plain weights.
    /// </summary>
    public static float[][] Weights(XafPackage package, IReadOnlyList<int> influences, Vector2[] restPoints,
        Transform2D[] restBones, float blend, int columns, int rows, bool[] covered)
    {
        float[][] plain = Weights(package, influences, restPoints, restBones, blend);
        List<int>[] neighbours = Neighbours(columns, rows, covered);
        var through = new float[influences.Count][];
        for (int b = 0; b < influences.Count; b++)
            through[b] = Through(package, restBones, influences[b], restPoints, neighbours, columns, rows, influences);

        var weights = new float[restPoints.Length][];
        var distances = new float[influences.Count];
        for (int p = 0; p < restPoints.Length; p++)
        {
            bool reached = false;
            for (int b = 0; b < influences.Count; b++)
            {
                distances[b] = through[b][p];
                reached |= !float.IsPositiveInfinity(distances[b]);
            }
            // A point off the art, or on an island no bone reaches, keeps the plain weights.
            weights[p] = neighbours[p].Count > 0 && reached ? Share(distances, blend) : plain[p];
        }
        return weights;
    }

    /// <summary>
    /// Shares a point out over its bones from its distance to each: the
    /// nearest gets most, and the ones about as near, within the blend width,
    /// share in, so the bend is smooth across a joint.
    /// </summary>
    private static float[] Share(float[] distances, float blend)
    {
        float width = Math.Max(0.5f, blend);
        float nearest = float.MaxValue;
        foreach (float distance in distances)
            nearest = Math.Min(nearest, distance);

        var row = new float[distances.Length];
        float total = 0f;
        for (int b = 0; b < distances.Length; b++)
        {
            if (float.IsPositiveInfinity(distances[b]))
                continue;
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
        return row;
    }

    /// <summary>For each grid point, the points it shares a covered cell with; none for a point on no covered cell.</summary>
    private static List<int>[] Neighbours(int columns, int rows, bool[] covered)
    {
        var neighbours = new List<int>[columns * rows];
        for (int i = 0; i < neighbours.Length; i++)
            neighbours[i] = new List<int>();

        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                if (!covered[row * (columns - 1) + column])
                    continue;
                int[] corners = { row * columns + column, row * columns + column + 1,
                    (row + 1) * columns + column, (row + 1) * columns + column + 1 };
                foreach (int a in corners)
                {
                    foreach (int b in corners)
                    {
                        if (a != b && !neighbours[a].Contains(b))
                            neighbours[a].Add(b);
                    }
                }
            }
        }
        return neighbours;
    }

    /// <summary>
    /// Every grid point's distance from a bone, travelling only over covered
    /// cells. It starts from the points that lie along the bone within about a
    /// cell of it, and every step costs as <see cref="Distance"/> measures, the
    /// part across the bone counting for little, so inside one limb it comes to
    /// what the plain distance gives. Unreachable points are infinitely far.
    /// </summary>
    private static float[] Through(XafPackage package, Transform2D[] world, int bone, Vector2[] points,
        List<int>[] neighbours, int columns, int rows, IReadOnlyList<int> influences)
    {
        (Vector2 from, Vector2 to) = XafSkinBones.Segment(package, world, bone);
        Vector2 along = to - from;
        float length = along.Length();
        Vector2 axis = length < 1e-4f ? Vector2.Zero : along / length;

        float StepCost(Vector2 step)
        {
            if (axis == Vector2.Zero)
                return step.Length();
            float forward = step.Dot(axis);
            float side = step.Cross(axis) * XafSkinBones.SideShare;
            return MathF.Sqrt(forward * forward + side * side);
        }

        var distance = new float[points.Length];
        Array.Fill(distance, float.PositiveInfinity);
        var queue = new PriorityQueue<int, float>();

        // Points next to the bone itself start the search, at what they are from it.
        float cell = columns > 1 && rows > 1
            ? Math.Max(points[1].DistanceTo(points[0]), points[columns].DistanceTo(points[0]))
            : 1f;
        int closest = -1;
        float closestDistance = float.PositiveInfinity;
        for (int p = 0; p < points.Length; p++)
        {
            if (neighbours[p].Count == 0)
                continue;
            float direct = XafSkinBones.Distance(package, world, bone, points[p], influences);
            if (direct < closestDistance)
            {
                closestDistance = direct;
                closest = p;
            }

            Vector2 offset = points[p] - from;
            float t = axis == Vector2.Zero ? 0f : offset.Dot(axis);
            float beside = axis == Vector2.Zero ? offset.Length() : MathF.Abs(offset.Cross(axis));
            if (t >= -cell && t <= length + cell && beside <= cell * 1.5f)
            {
                distance[p] = direct;
                queue.Enqueue(p, direct);
            }
        }

        // A bone that runs past the art, such as the hips over a leg, reaches it where it comes nearest.
        if (queue.Count == 0 && closest >= 0)
        {
            distance[closest] = closestDistance;
            queue.Enqueue(closest, closestDistance);
        }

        while (queue.TryDequeue(out int p, out float reached))
        {
            if (reached > distance[p])
                continue;
            foreach (int next in neighbours[p])
            {
                float through = reached + StepCost(points[next] - points[p]);
                if (through < distance[next])
                {
                    distance[next] = through;
                    queue.Enqueue(next, through);
                }
            }
        }
        return distance;
    }
}
