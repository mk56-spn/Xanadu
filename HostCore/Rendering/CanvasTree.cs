using System.Collections.Generic;
using Xanadu.Core.Rendering;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>
/// The host's retained canvas: the item tree the game builds through
/// <see cref="CanvasServer"/>. Each draw command becomes a primitive in the
/// item's own space as it is added; the renderer keeps them on the GPU and only
/// places them each frame (<see cref="CanvasRenderer"/>).
/// </summary>
/// <remarks>
/// Order: an item draws before its children and its mounts' items; siblings go
/// by when they were hung; z is relative to the parent's
/// and sorts the whole frame, lowest first, keeping tree order within one z.
/// </remarks>
internal sealed class CanvasTree : ICanvasServer
{
    private readonly Dictionary<ulong, CanvasItem> _items = [];
    private ulong _nextId = 1;
    private long _nextOrder;

    /// <summary>The item everything visible hangs from; the host passes it to the game as the parent canvas.</summary>
    public CanvasItem Root { get; }

    public CanvasTree()
    {
        Root = Make();
    }

    /// <summary>Hands out an item id as a <see cref="Handle"/>; textures share the same id space.</summary>
    public Handle MintHandle() => ToHandle(_nextId++);

    public static Handle ToHandle(ulong id) => new(id);

    public Handle RootHandle => ToHandle(Root.Id);

    public int Count => _items.Count;

    /// <summary>Items in a resident tree changed since the renderer last looked (<see cref="CanvasItem.Touch"/>).</summary>
    public readonly List<CanvasItem> Touched = [];

    /// <summary>A mount was taken down: whoever kept state for it lets go.</summary>
    public event Action<ICanvasMount>? Unmounted;

    private CanvasItem Make()
    {
        Handle rid = MintHandle();
        var item = new CanvasItem(rid.Id) { TouchList = Touched };
        _items.Add(item.Id, item);
        return item;
    }

    private CanvasItem? Find(Handle rid) => _items.GetValueOrDefault(rid.Id);

    /// <summary>The item behind an id, for the host's own use (offscreen roots).</summary>
    public CanvasItem? Item(Handle rid) => Find(rid);

    public Handle CreateItem() => ToHandle(Make().Id);

    public void Free(Handle rid)
    {
        if (!_items.Remove(rid.Id, out CanvasItem? item))
            return;

        item.ResidentOwner?.Forget(item.ResidentSlot);
        Detach(item);
        item.Particles?.Dispose();
        item.Particles = null;
        foreach (CanvasItem child in item.Children)
            child.Parent = null;
        item.Children.Clear();
    }

    private static void Detach(CanvasItem item)
    {
        item.Parent?.Children.Remove(item);
        item.Parent?.Touch();
        item.Parent = null;
    }

    public void SetParent(Handle rid, Handle parentRid)
    {
        if (Find(rid) is not { } item)
            return;

        Detach(item);
        if (Find(parentRid) is not { } parent || parent == item)
            return;

        item.Parent = parent;
        item.Order = _nextOrder++;
        parent.Children.Add(item);
        parent.ChildrenSorted = false;
        parent.Touch();
    }

    public void SetZIndex(Handle rid, int z)
    {
        if (Find(rid) is { } item)
            item.Z = Math.Clamp(z, -4096, 4096);
    }


    public void SetVisible(Handle rid, bool visible)
    {
        if (Find(rid) is { } item && item.Visible != visible)
        {
            item.Visible = visible;
            item.Touch();
        }
    }

    public void SetTransform(Handle rid, Transform2D transform)
    {
        if (Find(rid) is { } item && item.Transform != transform)
        {
            item.Transform = transform;
            item.Touch();
        }
    }

    public void SetModulate(Handle rid, Color color)
    {
        if (Find(rid) is { } item && item.Modulate != color)
        {
            item.Modulate = color;
            item.Touch();
        }
    }

    public void SetSelfModulate(Handle rid, Color color)
    {
        if (Find(rid) is { } item && item.SelfModulate != color)
        {
            item.SelfModulate = color;
            item.Touch();
        }
    }

    public void SetCustomRect(Handle rid, bool use, Rect2 rect)
    {
        if (Find(rid) is not { } item)
            return;

        item.UseCustomRect = use;
        item.CustomRect = rect;
        item.Touch();
    }

    public void SetClip(Handle rid, bool clip)
    {
        if (Find(rid) is { } item && item.Clip != clip)
        {
            item.Clip = clip;
            item.Touch();
        }
    }

    public void SetMaterial(Handle rid, CanvasMaterial? material)
    {
        if (Find(rid) is { } item && item.Material != material)
        {
            item.Material = material;
            item.Touch();
        }
    }




    public void SetParticleMesh(Handle rid, bool on)
    {
        if (Find(rid) is { } item && item.ParticleMesh != on)
        {
            item.ParticleMesh = on;
            item.Touch();
        }
    }

    public void Clear(Handle rid)
    {
        if (Find(rid) is { } item)
            item.ClearCommands();
    }

    public void AddRect(Handle rid, Rect2 rect, Color color)
    {
        if (Find(rid) is { } item)
            item.Add(Primitive.Rect(rect, color));
    }

    public void AddCircle(Handle rid, Vector2 center, float radius, Color color)
    {
        if (Find(rid) is { } item && radius > 0)
            item.Add(Primitive.Disc(center, radius, color));
    }

    public void AddLine(Handle rid, Vector2 from, Vector2 to, Color color, float width, bool antialiased)
    {
        if (Find(rid) is { } item)
            item.Add(Primitive.Line(from, to, color, color, width));
    }

    public void AddPolyline(Handle rid, Vector2[] points, Color[] colors, float width, bool antialiased)
    {
        if (Find(rid) is not { } item || points.Length < 2 || colors.Length == 0)
            return;

        item.Strip(points, colors, width);
    }

    public void AddMultiline(Handle rid, Vector2[] points, Color[] colors, float width)
    {
        if (Find(rid) is not { } item || colors.Length == 0)
            return;

        for (int i = 0; i + 1 < points.Length; i += 2)
        {
            Color color = colors.Length == 1 ? colors[0] : colors[Math.Min(i / 2, colors.Length - 1)];
            item.Add(Primitive.Line(points[i], points[i + 1], color, color, width));
        }
    }

    public void AddPolygon(Handle rid, Vector2[] points, Color[] colors, Vector2[]? uvs = null)
    {
        if (Find(rid) is not { } item || points.Length < 3 || colors.Length == 0)
            return;

        int[] indices = Triangulate.Polygon(points);
        int first = item.Points;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            item.Add(Primitive.Triangle(
                points[a], points[b], points[c],
                ColorAt(colors, a), ColorAt(colors, b), ColorAt(colors, c),
                uvs is null ? default : uvs[a], uvs is null ? default : uvs[b], uvs is null ? default : uvs[c],
                0, first + a, first + b, first + c));
        }

        item.Points = first + points.Length;
    }

    public void AddTriangleArray(Handle rid, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Handle texture = default)
    {
        if (Find(rid) is not { } item || colors.Length == 0)
            return;

        int first = item.Points;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (a >= points.Length || b >= points.Length || c >= points.Length)
                break;

            item.Add(Primitive.Triangle(
                points[a], points[b], points[c],
                ColorAt(colors, a), ColorAt(colors, b), ColorAt(colors, c),
                uvs is null ? default : uvs[a], uvs is null ? default : uvs[b], uvs is null ? default : uvs[c],
                texture.Id, first + a, first + b, first + c));
        }

        item.Points = first + points.Length;
    }

    /// <summary>Triangles with curve tags: a tagged one keeps its corners (start, control, end) and is trimmed to the curve in primitives.slang.</summary>
    public void AddCurveTriangles(Handle rid, Vector2[] points, Color[] colors, Vector2[] uvs, byte[] curves)
    {
        if (Find(rid) is not { } item || colors.Length == 0)
            return;

        int first = item.Points;
        for (int i = 0; i + 2 < points.Length; i += 3)
        {
            Primitive triangle = Primitive.Triangle(
                points[i], points[i + 1], points[i + 2],
                ColorAt(colors, i), ColorAt(colors, i + 1), ColorAt(colors, i + 2),
                i < uvs.Length ? uvs[i] : default, i + 1 < uvs.Length ? uvs[i + 1] : default, i + 2 < uvs.Length ? uvs[i + 2] : default,
                0, first + i, first + i + 1, first + i + 2);
            byte tag = i / 3 < curves.Length ? curves[i / 3] : CanvasCurves.None;
            if (tag != CanvasCurves.None)
                triangle.Flags |= Primitive.FlagCurve | (tag == CanvasCurves.Concave ? Primitive.FlagConcave : 0u);
            item.Add(triangle);
        }

        item.Points = first + points.Length;
    }

    public void AddTextureRect(Handle rid, Rect2 rect, Handle texture)
    {
        if (Find(rid) is { } item)
            item.Add(Primitive.TextureRect(rect, new Rect2(0, 0, 1, 1), Colors.White, texture.Id));
    }

    /// <summary>A region of a texture over a rect, tinted: one glyph, one sprite. One primitive, not two triangles.</summary>
    public void AddTextureRegion(Handle rid, Rect2 rect, Rect2 uv, Color color, Handle texture)
    {
        if (Find(rid) is { } item)
            item.Add(Primitive.TextureRect(rect, uv, color, texture.Id));
    }

    /// <summary>A glow round a region of a texture: drawn <paramref name="pad"/> past its rect and blurred on the GPU (<see cref="Primitive.Halo"/>).</summary>
    public void AddHalo(Handle rid, Rect2 rect, Rect2 uv, Color color, Handle texture, float sigma, float pad, float intensity)
    {
        if (Find(rid) is { } item)
            item.Add(Primitive.Halo(rect, uv, color, texture.Id, sigma, pad, intensity));
    }

    public void SelectRun(Handle rid, CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? values)
    {
        if (Find(rid) is not { } item)
            return;

        if (material is null && values is null)
        {
            item.CurrentRun = 0;
            return;
        }

        if (item.CurrentRun > 0 && item.Runs[item.CurrentRun - 1] is var run &&
            run.Material == material && ReferenceEquals(run.Values, values))
            return;

        item.Runs.Add((material, values));
        item.CurrentRun = item.Runs.Count;
        item.Touch();
    }

    public void Mount(Handle rid, ICanvasMount mount)
    {
        if (Find(rid) is not { } item)
            return;

        (item.Mounts ??= []).Add(mount);
        item.Touch();
    }

    public void Unmount(Handle rid, ICanvasMount mount)
    {
        if (Find(rid) is not { } item)
            return;

        item.Mounts?.Remove(mount);
        item.Touch();
        Unmounted?.Invoke(mount);
    }

    public void AddSetTransform(Handle rid, Transform2D transform)
    {
        if (Find(rid) is { } item)
            item.CommandTransform = transform;
    }

    private static Color ColorAt(Color[] colors, int index) =>
        colors.Length == 1 ? colors[0] : colors[Math.Min(index, colors.Length - 1)];
}

/// <summary>One canvas item: its place in the tree and its primitives, in its own space.</summary>
internal sealed class CanvasItem(ulong id)
{
    public readonly ulong Id = id;
    public CanvasItem? Parent;
    public readonly List<CanvasItem> Children = [];
    public bool ChildrenSorted = true;
    public long Order;
    public int Z;
    public bool Visible = true;
    public Transform2D Transform = Transform2D.Identity;
    public Color Modulate = Colors.White;
    public Color SelfModulate = Colors.White;
    public bool UseCustomRect;
    public Rect2 CustomRect;
    public bool Clip;

    /// <summary>What the item's own draws shade and blend with; null is the plain primitive pipeline.</summary>
    public CanvasMaterial? Material;

    /// <summary>Shaded runs among the item's draws: each primitive's <see cref="Primitive.Run"/> points into this.</summary>
    public readonly List<(CanvasMaterial? Material, IReadOnlyDictionary<string, Vector4>? Values)> Runs = [];

    /// <summary>The run draws added now go into; 0 is plain.</summary>
    public int CurrentRun;

    /// <summary>Set by <c>AddSetTransform</c>: applied to the commands that follow.</summary>
    public Transform2D CommandTransform = Transform2D.Identity;

    public readonly List<Primitive> Primitives = [];

    /// <summary>
    /// Mesh points added so far: the next command's points are numbered from here, so a
    /// triangle's corner indices are its points in the item, shared by every triangle that uses it.
    /// </summary>
    public int Points;

    /// <summary>GPU particles drawn after the item's own draws; made by the first burst into it.</summary>
    public ParticleField? Particles;

    /// <summary>
    /// Its own draws are the look of each of its particles: drawn once per live
    /// particle of <see cref="Particles"/>, turned, sized, tinted and placed by it,
    /// and not on their own.
    /// </summary>
    public bool ParticleMesh;

    /// <summary>Trees that list their own items here each frame, after this item's children.</summary>
    public List<ICanvasMount>? Mounts;

    /// <summary>Moves on whenever the primitives change, so the renderer knows to send them again.</summary>
    public int Version;

    /// <summary>The resident tree this item draws in, and its slot there; null and -1 when it draws through the walk.</summary>
    public HostResidentTree? ResidentOwner;
    public int ResidentSlot = -1;

    /// <summary>The tree's list of touched items, and whether this one is on it.</summary>
    public List<CanvasItem>? TouchList;
    public bool IsTouched;

    /// <summary>Tells the resident tree this item is in (if any) that its recording or its own place, look or kind changed.</summary>
    public void Touch()
    {
        if (ResidentOwner is null || IsTouched)
            return;

        IsTouched = true;
        TouchList?.Add(this);
    }

    /// <summary>Where the renderer keeps this item's primitives on the GPU, and for which version and pool.</summary>
    public int PoolStart = -1, PoolCapacity, PoolVersion = -1, PoolEpoch = -1;

    /// <summary>A hash of the primitives last sent, so a picture cleared and drawn again the same is not sent again.</summary>
    public int PoolHash;

    /// <summary>Whether <see cref="PoolHash"/> is the hash of what was sent (false when the send skipped hashing).</summary>
    public bool PoolHashKnown;

    /// <summary>Redraws in a row that came out different, and sends that skipped the hash since.</summary>
    public int HashMisses, HashSkips;

    /// <summary>The textures the primitives read, in the order of their local slots on the GPU.</summary>
    public readonly List<ulong> LocalTextures = [];

    public void ClearCommands()
    {
        Version++;
        Touch();
        Primitives.Clear();
        Runs.Clear();
        CurrentRun = 0;
        Points = 0;
        CommandTransform = Transform2D.Identity;
    }

    public void SortChildren()
    {
        if (ChildrenSorted)
            return;

        Children.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        ChildrenSorted = true;
    }

    public void Add(in Primitive primitive)
    {
        if (primitive.IsEmpty)
            return;

        Version++;
        Touch();
        Primitive placed = CommandTransform == Transform2D.Identity ? primitive : primitive.Transformed(CommandTransform);
        placed.Run = CurrentRun;
        Primitives.Add(placed);
    }

    /// <summary>A connected line with mitred joints, so a translucent stroke doesn't double up at its corners.</summary>
    public void Strip(Vector2[] points, Color[] colors, float width)
    {
        float half = MathF.Max(width, 1f) / 2f;
        int count = points.Length;
        Span<Vector2> offsets = count <= 256 ? stackalloc Vector2[count] : new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 before = i > 0 ? Normal(points[i - 1], points[i]) : Vector2.Zero;
            Vector2 after = i + 1 < count ? Normal(points[i], points[i + 1]) : Vector2.Zero;
            if (before == Vector2.Zero)
                before = after;
            if (after == Vector2.Zero)
                after = before;

            Vector2 miter = before + after;
            float miterLength = miter.Length();
            if (miterLength < 1e-4f)
            {
                offsets[i] = before * half;
                continue;
            }

            miter /= miterLength;
            float cos = miter.Dot(before);
            offsets[i] = miter * (half / MathF.Max(cos, 0.25f));
        }

        for (int i = 0; i + 1 < count; i++)
        {
            Color a = colors.Length == 1 ? colors[0] : colors[Math.Min(i, colors.Length - 1)];
            Color b = colors.Length == 1 ? colors[0] : colors[Math.Min(i + 1, colors.Length - 1)];
            Vector2 p = points[i], q = points[i + 1];
            int first = Points;
            Add(Primitive.Triangle(p + offsets[i], q + offsets[i + 1], q - offsets[i + 1], a, b, b,
                default, default, default, 0, first, first + 1, first + 2));
            Add(Primitive.Triangle(p + offsets[i], q - offsets[i + 1], p - offsets[i], a, b, a,
                default, default, default, 0, first, first + 2, first + 3));
            Points = first + 4;
        }
    }

    private static Vector2 Normal(Vector2 from, Vector2 to)
    {
        Vector2 along = to - from;
        float length = along.Length();
        return length <= 0f ? Vector2.Zero : new Vector2(-along.Y, along.X) / length;
    }
}

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
