using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// A sheet of cloth, such as a dress or a cape: a grid of points that hangs
/// straight down from its top edge in the rest pose, then swings under
/// gravity. Some points are pinned to bones, which carry them; the rest are
/// free, held to each other by their rest distances, and the sheet's side edges
/// are kept out of the colliders, so a dress swings out over a kicking leg
/// instead of passing through it.
/// The file holds only the settings; the points are simulated where the rig is shown.
/// </summary>
public sealed class XafCloth
{
    public const int MinColumns = 2;
    public const int MaxColumns = 24;
    public const int MinRows = 2;
    public const int MaxRows = 40;
    public const float MaxSize = 2000f;

    public string Id = "";

    /// <summary>The bone the top edge is laid out from; the sheet hangs straight down from it at rest.</summary>
    public string BoneId = "";

    /// <summary>The centre of the top edge, in the bone's own space.</summary>
    public Vector2 Offset;

    /// <summary>Points across the sheet, and rows of them from the top edge to the hem.</summary>
    public int Columns = 7;
    public int Rows = 13;

    /// <summary>Width at the top edge and at the hem; rows between flare evenly from one to the other.</summary>
    public float TopWidth = 26f;
    public float HemWidth = 60f;

    /// <summary>Distance from the top edge to the hem at rest.</summary>
    public float Length = 120f;

    /// <summary>Downward pull in pixels per second squared.</summary>
    public float Gravity = 900f;

    /// <summary>Sideways pull, positive towards the way the character faces.</summary>
    public float Lean;

    /// <summary>Share of velocity kept each step; 1 never slows down, 0 stops dead.</summary>
    public float Damping = 0.94f;

    /// <summary>How hard the sheet resists folding, 0 to 1: low drapes like silk, high holds like felt.</summary>
    public float Stiffness = 0.5f;

    public Color Fill = new("#7e22ce");

    /// <summary>Draw layer, as for attachments: higher draws in front.</summary>
    public int ZOrder = 2;

    public List<XafClothPin> Pins = new();
    public List<XafClothCollider> Colliders = new();

    public int Count => Columns * Rows;

    public int Index(int column, int row) => row * Columns + column;

    public bool IsPinned(int column, int row) =>
        Pins.Exists(pin => pin.Column == column && pin.Row == row);
}

/// <summary>A point of the grid carried by a bone; where it sits on the bone comes from the rest pose.</summary>
public sealed class XafClothPin
{
    public int Column;
    public int Row;
    public string BoneId = "";
}

/// <summary>
/// A capsule the cloth cannot enter, along a bone from its joint to its first
/// child's joint; a bone with no children is a circle at its joint.
/// </summary>
public sealed class XafClothCollider
{
    public string BoneId = "";
    public float Radius = 8f;
}

/// <summary>Where a cloth's points sit at rest, and the other geometry its simulation and drawing share.</summary>
public static class XafClothLayout
{
    /// <summary>The package's rest pose, which a cloth is laid out in.</summary>
    public static XafPose RestPose(XafPackage package)
    {
        var pose = new XafPose(package.Skeleton.Bones.Count, package.Attachments.Count, package.Effects.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        for (int i = 0; i < pose.Attachments.Length; i++)
            pose.Attachments[i] = XafTransform.From(package.Attachments[i].Transform);
        return pose;
    }

    /// <summary>
    /// The points at rest, in rig space: rows evenly spaced straight down from
    /// the top edge, each centred under it and as wide as the flare gives.
    /// </summary>
    public static Vector2[] RestPoints(XafPackage package, XafCloth cloth, Transform2D[] restWorld)
    {
        int bone = XafRig.FindBone(package, cloth.BoneId);
        Vector2 top = bone >= 0 && bone < restWorld.Length ? restWorld[bone] * cloth.Offset : cloth.Offset;
        var points = new Vector2[cloth.Count];
        for (int row = 0; row < cloth.Rows; row++)
        {
            float down = row / (float)Math.Max(1, cloth.Rows - 1);
            float width = Mathf.Lerp(cloth.TopWidth, cloth.HemWidth, down);
            for (int column = 0; column < cloth.Columns; column++)
            {
                float across = column / (float)Math.Max(1, cloth.Columns - 1) - 0.5f;
                points[cloth.Index(column, row)] = top + new Vector2(across * width, down * cloth.Length);
            }
        }
        return points;
    }

    /// <summary>The ends of a collider's capsule in the given pose, or null when its bone is missing.</summary>
    public static (Vector2 From, Vector2 To)? Capsule(XafPackage package, XafClothCollider collider, Transform2D[] world)
    {
        int bone = XafRig.FindBone(package, collider.BoneId);
        if (bone < 0 || bone >= world.Length)
            return null;

        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        Vector2 from = world[bone].Origin;
        return (from, child >= 0 && child < world.Length ? world[child].Origin : from);
    }

    /// <summary>
    /// The point of the segment from <paramref name="from"/> to <paramref name="to"/>
    /// nearest <paramref name="point"/>. Plain maths rather than Godot's
    /// Geometry2D, which needs the engine running.
    /// </summary>
    public static Vector2 ClosestOnSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 along = to - from;
        float length = along.LengthSquared();
        if (length <= 1e-8f)
            return from;

        float t = Math.Clamp((point - from).Dot(along) / length, 0f, 1f);
        return from + along * t;
    }

    /// <summary>Two triangles per cell, as indices into the points, for drawing the sheet.</summary>
    public static int[] Triangles(int columns, int rows)
    {
        var triangles = new List<int>((columns - 1) * (rows - 1) * 6);
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
        }
        return triangles.ToArray();
    }

    /// <summary>The sheet's outline: down one side, along the hem, up the other side and across the top.</summary>
    public static int[] Outline(int columns, int rows)
    {
        var outline = new List<int>();
        for (int row = 0; row < rows; row++)
            outline.Add(row * columns);
        for (int column = 1; column < columns; column++)
            outline.Add((rows - 1) * columns + column);
        for (int row = rows - 2; row >= 0; row--)
            outline.Add(row * columns + columns - 1);
        for (int column = columns - 2; column >= 0; column--)
            outline.Add(column);
        return outline.ToArray();
    }
}
