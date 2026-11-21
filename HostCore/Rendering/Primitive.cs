using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Core.Maths;
using Color = Xanadu.Core.Maths.Color;

namespace Xanadu.Host.Rendering;

/// <summary>What a <see cref="Primitive"/> is; the shader's <c>Kind*</c>.</summary>
internal enum PrimitiveKind : uint
{
    /// <summary>A box, its corners rounded by a radius, filled or stroked inside its edge; two colours make a ramp.</summary>
    Rect = 0,

    /// <summary>An ellipse filling its box, filled or stroked inside its edge.</summary>
    Disc = 1,

    /// <summary>A texture over its box, between two uv corners, times its colour.</summary>
    Texture = 2,

    /// <summary>A free triangle with a colour and uv per corner.</summary>
    Triangle = 3,

    /// <summary>
    /// The glow round a region of a texture, blurred where it is drawn: a box
    /// grown past the region by its pad, its uv the region itself.
    /// </summary>
    Halo = 4,
}

/// <summary>
/// One thing an item draws, in the item's own space: a box (rect, disc,
/// texture) by its centre and two half axes, or a triangle by its corners.
/// The renderer moves it to pixels with the item's transform and tint each
/// frame (<see cref="Write"/>); nothing is cut into triangles unless a
/// material's effect needs a mesh (<see cref="Tessellate"/>).
/// </summary>
internal struct Primitive
{
    public const uint FlagVertical = 1 << 8;
    public const uint FlagExact = 1 << 9;

    /// <summary>Box: centre, half axis x. Triangle: corners 0 and 1.</summary>
    public Vector4 A;

    /// <summary>Box: half axis y, corner radius, stroke (0 fills). Triangle: corner 2, uv 2.</summary>
    public Vector4 B;

    /// <summary>Box: uv at the min corner and at the max corner. Triangle: uv 0 and 1.</summary>
    public Vector4 Uv;

    public Color C0, C1, C2;
    public PrimitiveKind Kind;
    public uint Flags;

    /// <summary>A texture id from the canvas's id space; 0 for none.</summary>
    public ulong Texture;

    /// <summary>The item's material run it belongs to: 0 plain, else one past its index in <c>CanvasItem.Runs</c>.</summary>
    public int Run;

    /// <summary>A triangle's corners as points of the item's mesh (an effect's <c>v.index</c>).</summary>
    public int IndexA, IndexB, IndexC;

    public static Primitive Box(PrimitiveKind kind, Vector2 centre, Vector2 halfX, Vector2 halfY, Color colour,
        float radius = 0, float stroke = 0) => new()
    {
        Kind = kind,
        A = new Vector4(centre.X, centre.Y, halfX.X, halfX.Y),
        B = new Vector4(halfY.X, halfY.Y, radius, stroke),
        Uv = new Vector4(0, 0, 1, 1),
        C0 = colour,
        C1 = colour,
        C2 = colour,
    };

    public static Primitive Rect(Rect2 rect, Color colour) =>
        Box(PrimitiveKind.Rect, rect.Position + rect.Size / 2, new Vector2(rect.Size.X / 2, 0), new Vector2(0, rect.Size.Y / 2), colour);

    /// <summary>A straight line as a box along it, butt-ended; widths under a pixel draw a pixel wide. Colours ramp from start to end.</summary>
    public static Primitive Line(Vector2 from, Vector2 to, Color start, Color end, float width)
    {
        Vector2 along = to - from;
        float length = along.Length();
        if (length <= 0f)
            return default;

        Vector2 side = new Vector2(-along.Y, along.X) / length * (MathF.Max(width, 1f) / 2f);
        Primitive line = Box(PrimitiveKind.Rect, (from + to) / 2, along / 2, side, start);
        line.C1 = end;
        return line;
    }

    public static Primitive Disc(Vector2 centre, float radius, Color colour, float stroke = 0) =>
        Box(PrimitiveKind.Disc, centre, new Vector2(radius, 0), new Vector2(0, radius), colour, 0, stroke);

    public static Primitive TextureRect(Rect2 rect, Rect2 uv, Color colour, ulong texture)
    {
        Primitive quad = Box(PrimitiveKind.Texture, rect.Position + rect.Size / 2,
            new Vector2(rect.Size.X / 2, 0), new Vector2(0, rect.Size.Y / 2), colour);
        quad.Uv = new Vector4(uv.Position.X, uv.Position.Y, uv.End.X, uv.End.Y);
        quad.Texture = texture;
        return quad;
    }

    /// <summary>
    /// A halo round a texture region (a glyph): a box <paramref name="pad"/> pixels past
    /// <paramref name="rect"/>, whose shader blurs the region's alpha by <paramref name="sigma"/>
    /// pixels, scales it by <paramref name="intensity"/> and clamps it. The pad and σ ride in
    /// the radius and stroke slots, so they scale with the item; the intensity in the flags' top half.
    /// </summary>
    public static Primitive Halo(Rect2 rect, Rect2 uv, Color colour, ulong texture, float sigma, float pad, float intensity)
    {
        Primitive halo = Box(PrimitiveKind.Halo, rect.Position + rect.Size / 2,
            new Vector2(rect.Size.X / 2 + pad, 0), new Vector2(0, rect.Size.Y / 2 + pad), colour, pad, sigma);
        halo.Uv = new Vector4(uv.Position.X, uv.Position.Y, uv.End.X, uv.End.Y);
        halo.Texture = texture;
        halo.Flags = (uint)Math.Clamp(intensity * 1000f, 0f, 65535f) << 16;
        return halo;
    }

    public static Primitive Triangle(
        Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc,
        Vector2 ua, Vector2 ub, Vector2 uc, ulong texture, int ia, int ib, int ic) => new()
    {
        Kind = PrimitiveKind.Triangle,
        A = new Vector4(a.X, a.Y, b.X, b.Y),
        B = new Vector4(c.X, c.Y, uc.X, uc.Y),
        Uv = new Vector4(ua.X, ua.Y, ub.X, ub.Y),
        C0 = ca,
        C1 = cb,
        C2 = cc,
        Texture = texture,
        IndexA = ia,
        IndexB = ib,
        IndexC = ic,
    };

    public readonly bool IsEmpty => Kind == PrimitiveKind.Rect && A == default && B == default;

    /// <summary>The same primitive under <paramref name="t"/>.</summary>
    public readonly Primitive Transformed(in Transform2D t)
    {
        Primitive p = this;
        if (Kind == PrimitiveKind.Triangle)
        {
            Vector2 a = t * new Vector2(A.X, A.Y), b = t * new Vector2(A.Z, A.W), c = t * new Vector2(B.X, B.Y);
            p.A = new Vector4(a.X, a.Y, b.X, b.Y);
            p.B = new Vector4(c.X, c.Y, B.Z, B.W);
            return p;
        }

        Vector2 centre = t * new Vector2(A.X, A.Y);
        Vector2 x = t.BasisXform(new Vector2(A.Z, A.W));
        Vector2 y = t.BasisXform(new Vector2(B.X, B.Y));
        float scale = MathF.Sqrt(MathF.Abs(t.X.X * t.Y.Y - t.X.Y * t.Y.X));
        p.A = new Vector4(centre.X, centre.Y, x.X, x.Y);
        p.B = new Vector4(y.X, y.Y, B.Z * scale, B.W * scale);
        return p;
    }

    /// <summary>
    /// Whether, already in pixels, this can go in the opaque pre-pass: a plain
    /// filled rect, square to the screen, solid, with whole-pixel edges.
    /// </summary>
    public readonly bool IsOpaqueRect(Color tint)
    {
        if (Kind != PrimitiveKind.Rect || Texture != 0 || B.Z != 0 || B.W != 0)
            return false;
        if (A.W != 0 || B.X != 0)
            return false;
        if (C0.A * tint.A < 1f || C1 != C0)
            return false;

        return Whole(A.X - A.Z) && Whole(A.X + A.Z) && Whole(A.Y - B.Y) && Whole(A.Y + B.Y);
    }

    private static bool Whole(float value) => MathF.Abs(value - MathF.Round(value)) < 1e-3f;

    /// <summary>The axis-aligned pixel box a box primitive covers (min x, y, max x, y), for clipping on the CPU.</summary>
    public readonly Vector4 Bounds()
    {
        float hx = MathF.Abs(A.Z) + MathF.Abs(B.X), hy = MathF.Abs(A.W) + MathF.Abs(B.Y);
        return new Vector4(A.X - hx, A.Y - hy, A.X + hx, A.Y + hy);
    }

    /// <summary>Writes the GPU's copy, already in pixels: tinted, clipped, at a depth, reading texture slot <paramref name="slot"/>.</summary>
    public readonly void Write(ref GpuPrimitive gpu, Color tint, Vector4 clip, float depth, uint slot)
    {
        gpu.A = A;
        gpu.B = B;
        gpu.Uv = Uv;
        gpu.Clip = clip;
        (gpu.C0a, gpu.C0b) = Pack(C0 * tint);
        (gpu.C1a, gpu.C1b) = Pack(C1 * tint);
        (gpu.C2a, gpu.C2b) = Pack(C2 * tint);
        gpu.Kind = (uint)Kind | Flags;
        gpu.Slot = slot;
        gpu.Depth = depth;
        gpu.Pad0 = gpu.Pad1 = gpu.Pad2 = 0;
    }

    private static (uint, uint) Pack(Color c) =>
        (Half(c.R) | Half(c.G) << 16, Half(c.B) | Half(c.A) << 16);

    private static uint Half(float value) => BitConverter.HalfToUInt16Bits((Half)value);

    /// <summary>
    /// The primitive cut into the canvas vertices a material's effect draws
    /// with, under <paramref name="t"/> and <paramref name="tint"/>; its points
    /// are numbered from <paramref name="points"/>, which moves past them.
    /// </summary>
    public readonly void Tessellate(List<CanvasVertex> into, in Transform2D t, Color tint, ref int points, int columns = 1, int rows = 1)
    {
        if (Kind == PrimitiveKind.Triangle)
        {
            Vector2 a = new(A.X, A.Y), b = new(A.Z, A.W), c = new(B.X, B.Y);
            Add(into, t, tint, a, b, c, C0, C1, C2,
                new Vector2(Uv.X, Uv.Y), new Vector2(Uv.Z, Uv.W), new Vector2(B.Z, B.W), (uint)IndexA, (uint)IndexB, (uint)IndexC);
            points = Math.Max(points, Math.Max(IndexA, Math.Max(IndexB, IndexC)) + 1);
            return;
        }

        Vector2 centre = new(A.X, A.Y), x = new(A.Z, A.W), y = new(B.X, B.Y);
        if (Kind == PrimitiveKind.Disc)
        {
            // A fan, as many segments as the old canvas cut a circle into.
            float radius = MathF.Max(x.Length(), y.Length());
            int segments = Math.Clamp((int)(radius * 0.75f), 12, 96);
            Vector2 previous = centre + x;
            uint middle = (uint)points++;
            for (int i = 1; i <= segments; i++)
            {
                float angle = float.Tau * i / segments;
                Vector2 next = centre + x * MathF.Cos(angle) + y * MathF.Sin(angle);
                Add(into, t, tint, centre, previous, next, C0, C0, C0, default, default, default,
                    middle, (uint)points + (uint)(i - 1), (uint)points + (uint)(i % segments));
                previous = next;
            }

            points += segments;
            return;
        }

        Vector2 p0 = centre - x - y, p1 = centre + x - y, p2 = centre + x + y, p3 = centre - x + y;
        Vector2 u0 = new(Uv.X, Uv.Y), u2 = new(Uv.Z, Uv.W);
        Vector2 u1 = new(u2.X, u0.Y), u3 = new(u0.X, u2.Y);
        bool vertical = (Flags & FlagVertical) != 0;
        Color c0 = C0, c1 = vertical ? C0 : C1, c2 = C1, c3 = vertical ? C1 : C0;
        if (columns > 1 || rows > 1)
        {
            TessellateGrid(into, t, tint, ref points, columns, rows, [p0, p1, p2, p3], [u0, u1, u2, u3], [c0, c1, c2, c3]);
            return;
        }

        uint first = (uint)points;
        Add(into, t, tint, p0, p1, p2, c0, c1, c2, u0, u1, u2, first, first + 1, first + 2);
        Add(into, t, tint, p0, p2, p3, c0, c2, c3, u0, u2, u3, first, first + 2, first + 3);
        points += 4;
    }

    /// <summary>
    /// The rect cut into <paramref name="columns"/> × <paramref name="rows"/> cells (an effect's
    /// <c>[Grid]</c>), position, uv and colour blended from its four corners (top left, top right,
    /// bottom right, bottom left); neighbouring cells share their corners' indices.
    /// </summary>
    private static void TessellateGrid(List<CanvasVertex> into, in Transform2D t, Color tint, ref int points, int columns, int rows,
        Vector2[] p, Vector2[] u, Color[] c)
    {
        uint first = (uint)points;
        int stride = columns + 1;
        Vector2 P(int x, int y) => Blend(p[0], p[1], p[2], p[3], (float)x / columns, (float)y / rows);
        Vector2 U(int x, int y) => Blend(u[0], u[1], u[2], u[3], (float)x / columns, (float)y / rows);
        Color C(int x, int y)
        {
            float fx = (float)x / columns, fy = (float)y / rows;
            Color top = c[0].Lerp(c[1], fx), bottom = c[3].Lerp(c[2], fx);
            return top.Lerp(bottom, fy);
        }

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                uint a = first + (uint)(y * stride + x), b = a + 1, d = a + (uint)stride, e = d + 1;
                Add(into, t, tint, P(x, y), P(x + 1, y), P(x + 1, y + 1), C(x, y), C(x + 1, y), C(x + 1, y + 1),
                    U(x, y), U(x + 1, y), U(x + 1, y + 1), a, b, e);
                Add(into, t, tint, P(x, y), P(x + 1, y + 1), P(x, y + 1), C(x, y), C(x + 1, y + 1), C(x, y + 1),
                    U(x, y), U(x + 1, y + 1), U(x, y + 1), a, e, d);
            }
        }

        points += stride * (rows + 1);
    }

    private static Vector2 Blend(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft, float x, float y) =>
        Vector2.Lerp(Vector2.Lerp(topLeft, topRight, x), Vector2.Lerp(bottomLeft, bottomRight, x), y);

    private static void Add(
        List<CanvasVertex> into, in Transform2D t, Color tint,
        Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc,
        Vector2 ua, Vector2 ub, Vector2 uc, uint ia, uint ib, uint ic)
    {
        var rest = RestFrame.Of(a, b, c, ua, ub, uc).Transformed(t);
        into.Add(new CanvasVertex(t * a, ua, ca * tint, a, rest, ia));
        into.Add(new CanvasVertex(t * b, ub, cb * tint, b, rest, ib));
        into.Add(new CanvasVertex(t * c, uc, cc * tint, c, rest, ic));
    }
}

/// <summary>A <see cref="Primitive"/> as the GPU reads it: <c>Primitive</c> in primitives.slang, 112 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuPrimitive
{
    public Vector4 A, B, Uv, Clip;
    public uint C0a, C0b, C1a, C1b;
    public uint C2a, C2b, Kind, Slot;
    public float Depth, Pad0, Pad1, Pad2;
}

/// <summary>One recording drawn this frame, as the expand stage reads it: <c>Instance</c> in primitive_expand.slang, 96 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuInstance
{
    /// <summary>The transform's x and y axes.</summary>
    public Vector4 Basis;

    /// <summary>The transform's origin, its scale (for radii and strokes), unused.</summary>
    public Vector4 Origin;

    public Vector4 Tint;

    /// <summary>Pixels: min x, y, max x, y.</summary>
    public Vector4 Clip;

    public uint PoolStart, Count, OutStart, SlotBase;
    public uint Flags, OpaqueBase, ArgsOffset, DepthBase;
}
