using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The quad a preview shows an effect on, cut into a grid of cells with uv
/// 0..1 across, so a <c>deform</c> that bends it (a wave, a bulge) has corners
/// inside to move, not just the four at its edges. Shades the same as one rect.
/// </summary>
internal static class PreviewMesh
{
    /// <summary>Cells across; down follows the area's shape, so cells stay near square.</summary>
    public const int Columns = 96;

    public static void Add(Handle item, Rect2 area, Color colour)
    {
        int columns = Columns;
        int rows = Math.Max(1, (int)MathF.Round(Columns * area.Size.Y / Math.Max(area.Size.X, 1)));
        var points = new Vector2[(columns + 1) * (rows + 1)];
        var uvs = new Vector2[points.Length];
        for (int y = 0; y <= rows; y++)
        {
            for (int x = 0; x <= columns; x++)
            {
                var uv = new Vector2((float)x / columns, (float)y / rows);
                points[y * (columns + 1) + x] = area.Position + uv * area.Size;
                uvs[y * (columns + 1) + x] = uv;
            }
        }

        var indices = new int[columns * rows * 6];
        int n = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                indices[n++] = a; indices[n++] = b; indices[n++] = d;
                indices[n++] = a; indices[n++] = d; indices[n++] = c;
            }
        }

        CanvasServer.Current.AddTriangleArray(item, indices, points, [colour], uvs);
    }
}
