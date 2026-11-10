using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>The skin grid over an attachment's art: its points, triangles and covered cells.</summary>
public static class XafSkinGrid
{
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
}
