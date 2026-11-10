using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// A skin's own mesh, stored with the rig as DragonBones and Rive store theirs:
/// points traced from the art's outline and filled in, the triangles between
/// them, and each point's weights over the bones that bend it. Points are in
/// art space: the SVG's units, from the top left of its view box, which is also
/// where the raster's texture starts, so a point's texture spot is its place
/// over the art's size.
/// </summary>
public sealed class XafSkinMesh
{
    /// <summary>Most bones one point may follow; the rest of its weight goes to these.</summary>
    public const int MaxInfluences = 4;

    /// <summary>The art's size, in its own units: the view box the points are laid over.</summary>
    public Vector2 ArtSize;

    public Vector2[] Points = Array.Empty<Vector2>();

    /// <summary>Three point indices per triangle.</summary>
    public int[] Triangles = Array.Empty<int>();

    /// <summary>The bones the weights are over, by id, in the order of each point's weight row.</summary>
    public List<string> Bones = new();

    /// <summary>One row per point, one weight per bone in <see cref="Bones"/>, summing to one.</summary>
    public float[][] Weights = Array.Empty<float[]>();

    /// <summary>Counts edits to the weights, so whatever caches them knows to look again. Not saved.</summary>
    public int Version;

    public XafSkinMesh Clone() => new()
    {
        ArtSize = ArtSize,
        Points = (Vector2[])Points.Clone(),
        Triangles = (int[])Triangles.Clone(),
        Bones = new List<string>(Bones),
        Weights = Array.ConvertAll(Weights, row => (float[])row.Clone()),
    };

    public static bool AreEqual(XafSkinMesh? a, XafSkinMesh? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (a.ArtSize != b.ArtSize || a.Points.Length != b.Points.Length || a.Triangles.Length != b.Triangles.Length ||
            a.Weights.Length != b.Weights.Length || !a.Bones.SequenceEqualTo(b.Bones))
        {
            return false;
        }
        for (int i = 0; i < a.Points.Length; i++)
        {
            if (a.Points[i] != b.Points[i])
                return false;
        }
        for (int i = 0; i < a.Triangles.Length; i++)
        {
            if (a.Triangles[i] != b.Triangles[i])
                return false;
        }
        for (int i = 0; i < a.Weights.Length; i++)
        {
            if (a.Weights[i].Length != b.Weights[i].Length)
                return false;
            for (int j = 0; j < a.Weights[i].Length; j++)
            {
                if (a.Weights[i][j] != b.Weights[i][j])
                    return false;
            }
        }
        return true;
    }

    /// <summary>Keeps each point's strongest <see cref="MaxInfluences"/> weights over a hundredth, summing to one.</summary>
    public static void Tidy(float[] row)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (!(row[i] >= 0.01f))
                row[i] = 0f;
        }
        if (row.Length > MaxInfluences)
        {
            float[] sorted = (float[])row.Clone();
            Array.Sort(sorted);
            float least = sorted[^MaxInfluences];
            int kept = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] > 0f && (row[i] < least || kept == MaxInfluences))
                    row[i] = 0f;
                else if (row[i] > 0f)
                    kept++;
            }
        }

        float total = 0f;
        foreach (float weight in row)
            total += weight;
        if (total <= 0f)
            return;
        for (int i = 0; i < row.Length; i++)
            row[i] /= total;
    }
}

internal static class XafListExtensions
{
    public static bool SequenceEqualTo(this List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }
}
