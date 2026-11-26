using Xanadu.Core.Rendering;

namespace Xanadu.Rendering;

/// <summary>
/// The quad at rest a triangle belongs to: its uv-(0, 0) corner and its two sides, in
/// pixels. On a quad, position = corner + xAxis * u + yAxis * v, so a triangle's three
/// corners and their uvs give it exactly; every triangle of a mesh whose uv runs evenly
/// across it (a rect, a grid) gives the same one. Where uv doesn't span the triangle
/// (lines, untextured shapes) the sides are zero and the corner is the triangle's middle.
/// </summary>
internal readonly record struct RestFrame(Vector2 Corner, Vector2 XAxis, Vector2 YAxis)
{
    public static RestFrame Of(Vector2 pa, Vector2 pb, Vector2 pc, Vector2 ua, Vector2 ub, Vector2 uc)
    {
        Vector2 d1 = pb - pa, d2 = pc - pa, e1 = ub - ua, e2 = uc - ua;
        float det = e1.X * e2.Y - e2.X * e1.Y;
        if (MathF.Abs(det) < 1e-12f)
            return new RestFrame((pa + pb + pc) / 3f, Vector2.Zero, Vector2.Zero);

        // [xAxis yAxis] · [e1 e2] = [d1 d2], so the sides are [d1 d2] · [e1 e2]⁻¹.
        Vector2 xAxis = (d1 * e2.Y - d2 * e1.Y) / det;
        Vector2 yAxis = (d2 * e1.X - d1 * e2.X) / det;
        return new RestFrame(pa - xAxis * ua.X - yAxis * ua.Y, xAxis, yAxis);
    }

    /// <summary>The same quad under a transform: the corner moved, the sides turned and scaled.</summary>
    public RestFrame Transformed(in Transform2D t) => new(t * Corner, t.BasisXform(XAxis), t.BasisXform(YAxis));
}

/// <summary>
/// One vertex as the GPU reads it: pixel position, texture UV, colour, its
/// position in the item's own space (an effect's <c>local</c>, Godot's VERTEX),
/// the quad at rest it belongs to (a deform's <c>v.rest</c>) and its point in the
/// item's mesh (<c>v.index</c>; triangles sharing a corner share it).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct CanvasVertex(Vector2 position, Vector2 uv, Color color, Vector2 local, RestFrame rest, uint index) : MoonWorks.Graphics.IVertexType
{
    public float X = position.X, Y = position.Y, U = uv.X, V = uv.Y;
    public float R = color.R, G = color.G, B = color.B, A = color.A;
    public float LX = local.X, LY = local.Y;
    public float CornerX = rest.Corner.X, CornerY = rest.Corner.Y;
    public float XAxisX = rest.XAxis.X, XAxisY = rest.XAxis.Y, YAxisX = rest.YAxis.X, YAxisY = rest.YAxis.Y;
    public uint Index = index;

    public readonly RestFrame Rest => new(new Vector2(CornerX, CornerY), new Vector2(XAxisX, XAxisY), new Vector2(YAxisX, YAxisY));

    public static MoonWorks.Graphics.VertexElementFormat[] Formats { get; } =
    [
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float4,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Uint,
    ];

    public static uint[] Offsets { get; } = [0, 8, 16, 32, 40, 48, 56, 64];
}
