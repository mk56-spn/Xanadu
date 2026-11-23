using System;
using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Animation;

/// <summary>
/// How a cloth's art is laid on its sheet: the art's box is stretched over the
/// whole grid (left edge on the first column, top edge on the top row), and
/// every triangle of the art is cut along the grid's cells, so each piece lies
/// inside one cell and bends with its four corners as the cloth swings.
/// </summary>
public static class RigClothArt
{
    /// <summary>
    /// The art cut into the cells of a grid. Each vertex names its cell, where
    /// it sits in it (0 to 1 across and down), its colour and its place across
    /// the art's box (0 to 1, for the shader); every three triangle indices make one.
    /// </summary>
    public sealed record Cut(int[] Cells, Vector2[] InCell, Color[] Colors, Vector2[] Uvs, int[] Triangles, Rect2 Bounds);

    /// <summary>Cuts <paramref name="mesh"/> along the cells of a grid of <paramref name="columns"/> by <paramref name="rows"/> points.</summary>
    public static Cut Of(ArtMesh mesh, int columns, int rows)
    {
        var cells = new List<int>();
        var inCell = new List<Vector2>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        int across = Math.Max(1, columns - 1);
        int down = Math.Max(1, rows - 1);
        Vector2 size = new(MathF.Max(mesh.Bounds.Size.X, 1e-3f), MathF.Max(mesh.Bounds.Size.Y, 1e-3f));

        var clipped = new List<Vector2>(8);
        var scratch = new List<Vector2>(8);
        Span<Vector2> corner = stackalloc Vector2[3];
        for (int t = 0; t + 2 < mesh.Triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector2 uv = (mesh.Points[mesh.Triangles[t + k]] - mesh.Bounds.Position) / size;
                // In grid units: whole part is the cell, fraction the place in it.
                corner[k] = new Vector2(Math.Clamp(uv.X, 0f, 1f) * across, Math.Clamp(uv.Y, 0f, 1f) * down);
            }
            Color colour = mesh.Colors[mesh.Triangles[t]];
            int c0 = Math.Clamp((int)MathF.Floor(MathF.Min(corner[0].X, MathF.Min(corner[1].X, corner[2].X))), 0, across - 1);
            int c1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(corner[0].X, MathF.Max(corner[1].X, corner[2].X))) - 1, c0, across - 1);
            int r0 = Math.Clamp((int)MathF.Floor(MathF.Min(corner[0].Y, MathF.Min(corner[1].Y, corner[2].Y))), 0, down - 1);
            int r1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(corner[0].Y, MathF.Max(corner[1].Y, corner[2].Y))) - 1, r0, down - 1);

            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    clipped.Clear();
                    clipped.Add(corner[0]);
                    clipped.Add(corner[1]);
                    clipped.Add(corner[2]);
                    Clip(clipped, scratch, 0, column, +1f);
                    Clip(clipped, scratch, 0, column + 1, -1f);
                    Clip(clipped, scratch, 1, row, +1f);
                    Clip(clipped, scratch, 1, row + 1, -1f);
                    if (clipped.Count < 3)
                        continue;

                    int start = cells.Count;
                    int cell = row * columns + column;
                    foreach (Vector2 point in clipped)
                    {
                        cells.Add(cell);
                        inCell.Add(new Vector2(
                            Math.Clamp(point.X - column, 0f, 1f),
                            Math.Clamp(point.Y - row, 0f, 1f)));
                        colors.Add(colour);
                        uvs.Add(new Vector2(point.X / across, point.Y / down));
                    }
                    // The cut is convex (a triangle clipped by a box): a fan covers it.
                    for (int i = 1; i + 1 < clipped.Count; i++)
                    {
                        triangles.Add(start);
                        triangles.Add(start + i);
                        triangles.Add(start + i + 1);
                    }
                }
            }
        }
        return new Cut([.. cells], [.. inCell], [.. colors], [.. uvs], [.. triangles], mesh.Bounds);
    }

    /// <summary>Where a vertex of a cut lands on the simulated sheet: its cell's four corners, mixed by its place in the cell.</summary>
    public static Vector2 On(IReadOnlyList<Vector2> points, int columns, int cell, Vector2 inCell)
    {
        Vector2 a = points[cell];
        Vector2 b = points[cell + 1];
        Vector2 c = points[cell + columns];
        Vector2 d = points[cell + columns + 1];
        Vector2 top = a + (b - a) * inCell.X;
        Vector2 bottom = c + (d - c) * inCell.X;
        return top + (bottom - top) * inCell.Y;
    }

    /// <summary>A new cloth's art: a purple sheet with a darker band at the hem, so it reads as hanging cloth.</summary>
    public static XifGeometry DefaultArt() => new()
    {
        Polygons =
        {
            new XifPolygon
            {
                Points = { new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 34), new Vector2(0, 34) },
                Fill = new Color("#7e22ce"),
            },
            new XifPolygon
            {
                Points = { new Vector2(0, 34), new Vector2(20, 34), new Vector2(20, 40), new Vector2(0, 40) },
                Fill = new Color("#5b1894"),
            },
        },
    };

    /// <summary>Keeps the part of the convex polygon on one side of a grid line (axis 0: x = at, 1: y = at); side +1 keeps the larger side.</summary>
    private static void Clip(List<Vector2> polygon, List<Vector2> scratch, int axis, float at, float side)
    {
        if (polygon.Count < 3)
            return;
        scratch.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 from = polygon[i];
            Vector2 to = polygon[(i + 1) % polygon.Count];
            float f = ((axis == 0 ? from.X : from.Y) - at) * side;
            float g = ((axis == 0 ? to.X : to.Y) - at) * side;
            if (f >= 0f)
                scratch.Add(from);
            if ((f >= 0f) != (g >= 0f))
                scratch.Add(from + (to - from) * (f / (f - g)));
        }
        polygon.Clear();
        polygon.AddRange(scratch);
    }
}
