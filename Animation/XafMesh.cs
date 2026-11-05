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

    /// <summary>
    /// The mesh traced from the art with its stored weights, which is drawn in
    /// place of the grid when there is one (see <see cref="XafSkinAuthoring"/>).
    /// </summary>
    public XafSkinMesh? Skin;
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

    /// <summary>
    /// The grid's triangles over the cells the art covers only, so the mesh
    /// follows the art's outline rather than its rectangle; every cell when
    /// <paramref name="covered"/> is null.
    /// </summary>
    public static int[] Triangles(XafMesh mesh, Vector2 size, bool[]? covered)
    {
        if (covered is null)
            return Triangles(mesh, size);

        (int columns, int rows) = Points(mesh, size);
        var triangles = new List<int>();
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                if (!covered[row * (columns - 1) + column])
                    continue;
                int a = row * columns + column;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
        }
        return triangles.ToArray();
    }

    /// <summary>Samples taken across a cell, each way, to find whether the art covers any of it.</summary>
    private const int CoverSamples = 5;

    /// <summary>Opacity under this is empty canvas, not art.</summary>
    public const float CoverAlpha = 0.05f;

    /// <summary>
    /// Which of the grid's cells the art covers, row by row, from its opacity
    /// at a point given as a fraction across and down the art: a cell with any
    /// sample over <see cref="CoverAlpha"/> counts, so the edge cells stay in.
    /// </summary>
    public static bool[] Covered(XafMesh mesh, Vector2 size, Func<Vector2, float> alpha)
    {
        (int columns, int rows) = Points(mesh, size);
        var covered = new bool[(columns - 1) * (rows - 1)];
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                bool any = false;
                for (int sy = 0; sy < CoverSamples && !any; sy++)
                {
                    for (int sx = 0; sx < CoverSamples && !any; sx++)
                    {
                        var uv = new Vector2(
                            (column + sx / (float)(CoverSamples - 1)) / (columns - 1),
                            (row + sy / (float)(CoverSamples - 1)) / (rows - 1));
                        any = alpha(uv) > CoverAlpha;
                    }
                }
                covered[row * (columns - 1) + column] = any;
            }
        }
        return covered;
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
        var distances = new float[influences.Count];
        for (int p = 0; p < restPoints.Length; p++)
        {
            for (int b = 0; b < influences.Count; b++)
                distances[b] = Distance(package, restBones, influences[b], restPoints[p], influences);
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
        (Vector2 from, Vector2 to) = Segment(package, world, bone);
        Vector2 along = to - from;
        float length = along.Length();
        Vector2 axis = length < 1e-4f ? Vector2.Zero : along / length;

        float StepCost(Vector2 step)
        {
            if (axis == Vector2.Zero)
                return step.Length();
            float forward = step.Dot(axis);
            float side = step.Cross(axis) * SideShare;
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
            float direct = Distance(package, world, bone, points[p], influences);
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

    /// <summary>
    /// Bends the rest points into the pose: each moves with each bone it
    /// follows as that bone moved from rest, mixed by weight. The attachment's
    /// own bone carries it as the attachment itself is posed, so keys on the
    /// attachment still move it.
    ///
    /// The moves are mixed as turns, not as end points (dual quaternion
    /// blending, flat): a point shared by the thigh and the shin swings round
    /// the knee by part of the bend instead of cutting across it, so the back
    /// of a bent knee keeps its volume. A bone turned into the screen first
    /// squashes the points it carries along itself, in the rest pose (see
    /// <see cref="XafRig.Foreshorten"/>). A move that is not a plain turn and
    /// shift, such as a mirrored bone or scaled art, is mixed point by point.
    /// </summary>
    public static Vector2[] Deform(IReadOnlyList<int> influences, float[][] weights, Vector2[] restPoints,
        Transform2D[] restBones, Transform2D[] bones, Transform2D restAttachment, Transform2D attachment,
        Transform2D[]? squash = null)
    {
        var moves = new Transform2D[influences.Count];
        var squashes = new Transform2D[influences.Count];
        var turns = new Transform2D[influences.Count];
        bool rigid = true;
        for (int b = 0; b < influences.Count; b++)
        {
            int bone = influences[b];
            Transform2D squashed = squash is not null && bone < squash.Length ? squash[bone] : Transform2D.Identity;
            moves[b] = b == 0
                ? attachment * restAttachment.AffineInverse()
                : bones[bone] * squashed * restBones[bone].AffineInverse();

            // The move is a squash in the rest pose, then a turn and a shift.
            squashes[b] = restBones[bone] * squashed * restBones[bone].AffineInverse();
            turns[b] = moves[b] * squashes[b].AffineInverse();
            rigid &= IsTurn(turns[b]);
        }

        var points = new Vector2[restPoints.Length];
        for (int p = 0; p < restPoints.Length; p++)
        {
            if (!rigid)
            {
                Vector2 sum = Vector2.Zero;
                for (int b = 0; b < moves.Length; b++)
                {
                    if (weights[p][b] > 0f)
                        sum += moves[b] * restPoints[p] * weights[p][b];
                }
                points[p] = sum;
                continue;
            }

            Vector2 squashedPoint = Vector2.Zero;
            for (int b = 0; b < squashes.Length; b++)
            {
                if (weights[p][b] > 0f)
                    squashedPoint += squashes[b] * restPoints[p] * weights[p][b];
            }
            points[p] = BlendTurns(turns, weights[p]) * squashedPoint;
        }
        return points;
    }

    /// <summary>Whether a transform only turns and shifts: an unscaled, unmirrored rotation.</summary>
    private static bool IsTurn(Transform2D transform) =>
        MathF.Abs(transform.X.Length() - 1f) < 1e-3f && MathF.Abs(transform.Y.Length() - 1f) < 1e-3f &&
        MathF.Abs(transform.X.Dot(transform.Y)) < 1e-3f && transform.Determinant() > 0f;

    /// <summary>
    /// Mixes turns and shifts by weight as dual quaternions about the axis out
    /// of the screen: each is the half turn (cos, sin) and the shift carried
    /// through it, the weighted sum is scaled back to a unit turn, and the
    /// shift is read back out. Halves that point the opposite way to the first
    /// are flipped, so two near turns never mix the long way round.
    /// </summary>
    private static Transform2D BlendTurns(Transform2D[] turns, float[] weights)
    {
        float rc = 0f, rs = 0f, dx = 0f, dy = 0f;
        float firstC = 0f, firstS = 0f;
        bool first = true;
        for (int b = 0; b < turns.Length; b++)
        {
            float w = weights[b];
            if (w <= 0f)
                continue;

            float half = MathF.Atan2(turns[b].X.Y, turns[b].X.X) / 2f;
            float c = MathF.Cos(half), s = MathF.Sin(half);
            if (first)
            {
                firstC = c;
                firstS = s;
                first = false;
            }
            else if (c * firstC + s * firstS < 0f)
            {
                w = -w;
            }

            Vector2 t = turns[b].Origin;
            rc += w * c;
            rs += w * s;
            dx += w * 0.5f * (c * t.X + s * t.Y);
            dy += w * 0.5f * (c * t.Y - s * t.X);
        }

        float length = MathF.Sqrt(rc * rc + rs * rs);
        if (length < 1e-6f)
            return Transform2D.Identity;
        rc /= length;
        rs /= length;
        dx /= length;
        dy /= length;

        var shift = new Vector2(2f * (rc * dx - rs * dy), 2f * (rc * dy + rs * dx));
        float angle = 2f * MathF.Atan2(rs, rc);
        return new Transform2D(angle, shift);
    }

    /// <summary>
    /// How far a point is from a bone, mostly along it: how far past either end
    /// of the bone it lies, plus a little of how far to the side. A point beside
    /// the thigh just above the knee then counts as on the thigh, not half on
    /// the shin, so the blend sits on the joint and the inside of a bend does
    /// not pinch.
    /// </summary>
    private static float Distance(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences) =>
        Distance(package, world, bone, point) + BeforeJoint(package, world, bone, point, influences) +
        OutAlongChild(package, world, bone, point, influences);

    /// <summary>
    /// The other side of <see cref="BeforeJoint"/>: how far a point lies out
    /// along a child that branches off sideways, less how far it lies either
    /// side of the joint along this bone's own line, for the child bending the
    /// same mesh that it reaches furthest along. A shin then does not keep the
    /// toes just because they sit level with the ankle; along a straight chain
    /// the two measures agree and nothing is added.
    /// </summary>
    private static float OutAlongChild(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences)
    {
        float most = 0f;
        for (int i = 0; i < influences.Count; i++)
        {
            int child = influences[i];
            if (package.Skeleton.Bones[child].Parent != bone)
                continue;

            (Vector2 joint, Vector2 end) = Segment(package, world, child);
            Vector2 inward = Inward(package, world, bone, child);
            Vector2 along = end - joint;
            if (inward == Vector2.Zero || along.LengthSquared() < 1e-6f)
                continue;

            // Either side of the joint along this bone's line takes away from
            // it, so the shin's own length above the ankle stays the shin's.
            Vector2 offset = point - joint;
            float beside = MathF.Abs(offset.Dot(inward));
            most = MathF.Max(most, offset.Dot(along.Normalized()) - beside);
        }
        return most;
    }

    /// <summary>
    /// How far a point lies on the parent's side of a bone's joint, measured
    /// along the line from the parent's joint to this one; zero past it, and
    /// zero when the parent does not bend the same mesh. A bone only carries
    /// what lies beyond its joint: a foot, which runs sideways from the ankle,
    /// does not take the bottom of the shin just because it lies beside it.
    /// </summary>
    private static float BeforeJoint(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences)
    {
        int parent = package.Skeleton.Bones[bone].Parent;
        if (parent < 0 || !Contains(influences, parent))
            return 0f;

        Vector2 joint = world[bone].Origin;
        Vector2 inward = Inward(package, world, parent, bone);
        if (inward == Vector2.Zero)
            return 0f;
        return MathF.Max(0f, (joint - point).Dot(inward));
    }

    /// <summary>
    /// The way a limb runs through the joint where <paramref name="child"/>
    /// meets <paramref name="parent"/>, as a unit vector. Where the child
    /// carries on the parent's own line, as the shin does the thigh's, it is
    /// the parent's way in to the joint. Where it branches off, as a leg does
    /// from the hips while the spine carries on up, the parent's line says
    /// nothing about it, and it is the child's own way out.
    /// </summary>
    private static Vector2 Inward(XafPackage package, Transform2D[] world, int parent, int child)
    {
        int next = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == parent);
        Vector2 way = next == child
            ? world[child].Origin - world[parent].Origin
            : Segment(package, world, child).To - world[child].Origin;
        return way.LengthSquared() < 1e-6f ? Vector2.Zero : way.Normalized();
    }

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }
        return false;
    }

    private static float Distance(XafPackage package, Transform2D[] world, int bone, Vector2 point)
    {
        (Vector2 from, Vector2 to) = Segment(package, world, bone);
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

    /// <summary>
    /// Where a bone runs, in the space of <paramref name="world"/>: from its
    /// joint to its first child's, or, with no child, to its
    /// <see cref="XafBone.Tip"/>; a bone with neither is a point.
    /// </summary>
    public static (Vector2 From, Vector2 To) Segment(XafPackage package, Transform2D[] world, int bone)
    {
        Vector2 from = world[bone].Origin;
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        if (child >= 0 && child < world.Length)
            return (from, world[child].Origin);
        return (from, world[bone] * package.Skeleton.Bones[bone].Tip);
    }

    /// <summary>How much a point's distance to the side of a bone counts against its distance along it.</summary>
    private const float SideShare = 0.25f;
}
