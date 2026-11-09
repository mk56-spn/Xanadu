using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using Xanadu.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>
/// The host's retained canvas: the item tree the game builds through
/// <see cref="CanvasServer"/>. Each draw command is turned into triangles in the
/// item's own space as it is added, so a frame only transforms, colours and
/// batches what is already there (<see cref="CanvasRenderer"/>).
/// </summary>
/// <remarks>
/// Ordering follows Godot's canvas, which the game was written against: an
/// item draws before its children; siblings go by draw index, then by when
/// they were parented; z-index is relative to the parent's and sorts the whole
/// tree, lowest first, keeping tree order within one z.
/// </remarks>
internal sealed class CanvasTree : ICanvasServer
{
    private readonly Dictionary<ulong, CanvasItem> _items = new();
    private ulong _nextId = 1;
    private long _nextOrder;

    /// <summary>The item everything visible hangs from; the host passes it to the game as the parent canvas.</summary>
    public CanvasItem Root { get; }

    public CanvasTree()
    {
        Root = Make();
    }

    /// <summary>Hands out an id the game sees as a Godot <see cref="Rid"/>; textures share the same id space.</summary>
    public Rid MintRid() => ToRid(_nextId++);

    public static Rid ToRid(ulong id) => Unsafe.As<ulong, Rid>(ref id);

    public Rid RootRid => ToRid(Root.Id);

    public int Count => _items.Count;

    private CanvasItem Make()
    {
        Rid rid = MintRid();
        var item = new CanvasItem(rid.Id);
        _items.Add(item.Id, item);
        return item;
    }

    private CanvasItem? Find(Rid rid) => _items.GetValueOrDefault(rid.Id);

    /// <summary>The item behind an id, for the host's own use (offscreen roots).</summary>
    public CanvasItem? Item(Rid rid) => Find(rid);

    public Rid CreateItem() => ToRid(Make().Id);

    public void Free(Rid rid)
    {
        if (!_items.Remove(rid.Id, out CanvasItem? item))
            return;

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
        item.Parent = null;
    }

    public void SetParent(Rid rid, Rid parentRid)
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
    }

    public void SetZIndex(Rid rid, int z)
    {
        if (Find(rid) is { } item)
            item.Z = Math.Clamp(z, -4096, 4096);
    }

    public void SetDrawIndex(Rid rid, int index)
    {
        if (Find(rid) is not { } item || item.DrawIndex == index)
            return;

        item.DrawIndex = index;
        if (item.Parent is { } parent)
            parent.ChildrenSorted = false;
    }

    public void SetVisible(Rid rid, bool visible)
    {
        if (Find(rid) is { } item)
            item.Visible = visible;
    }

    public void SetTransform(Rid rid, Transform2D transform)
    {
        if (Find(rid) is { } item)
            item.Transform = transform;
    }

    public void SetModulate(Rid rid, Color color)
    {
        if (Find(rid) is { } item)
            item.Modulate = color;
    }

    public void SetSelfModulate(Rid rid, Color color)
    {
        if (Find(rid) is { } item)
            item.SelfModulate = color;
    }

    public void SetCustomRect(Rid rid, bool use, Rect2 rect)
    {
        if (Find(rid) is not { } item)
            return;

        item.UseCustomRect = use;
        item.CustomRect = rect;
    }

    public void SetClip(Rid rid, bool clip)
    {
        if (Find(rid) is { } item)
            item.Clip = clip;
    }

    public void SetMaterial(Rid rid, CanvasMaterial? material)
    {
        if (Find(rid) is { } item)
            item.Material = material;
    }

    /// <summary>One per-item value over the material's (Godot's instance uniforms).</summary>
    public void SetInstanceValue(Rid rid, string name, Vector4 value)
    {
        if (Find(rid) is not { } item)
            return;

        item.InstanceValues ??= new System.Collections.Generic.Dictionary<string, Vector4>();
        item.InstanceValues[name] = value;
    }

    /// <summary>
    /// The item and its children draw into an image of their own, composited
    /// as one quad with the item's material and modulate (step 6). The margins
    /// are Godot's; the host always fits the image to what was drawn.
    /// </summary>
    public void SetCanvasGroup(Rid rid, float clearMargin, bool fitEmpty, float fitMargin)
    {
        if (Find(rid) is { } item)
            item.Group = true;
    }

    public void SetCopyScreen(Rid rid, bool copy)
    {
        if (Find(rid) is { } item)
            item.CopyScreen = copy;
    }

    public void Clear(Rid rid)
    {
        if (Find(rid) is { } item)
            item.ClearCommands();
    }

    public void AddRect(Rid rid, Rect2 rect, Color color)
    {
        if (Find(rid) is not { } item)
            return;

        Vector2 a = rect.Position, c = rect.End;
        item.Quad(a, new Vector2(c.X, a.Y), c, new Vector2(a.X, c.Y), color, default);
    }

    public void AddCircle(Rid rid, Vector2 center, float radius, Color color)
    {
        if (Find(rid) is not { } item || radius <= 0)
            return;

        int segments = Math.Clamp((int)(radius * 0.75f), 12, 96);
        Vector2 previous = center + new Vector2(radius, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            Vector2 next = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            item.Triangle(center, previous, next, color, color, color, default);
            previous = next;
        }
    }

    public void AddLine(Rid rid, Vector2 from, Vector2 to, Color color, float width, bool antialiased)
    {
        if (Find(rid) is { } item)
            item.Segment(from, to, color, color, width);
    }

    public void AddPolyline(Rid rid, Vector2[] points, Color[] colors, float width, bool antialiased)
    {
        if (Find(rid) is not { } item || points.Length < 2 || colors.Length == 0)
            return;

        item.Strip(points, colors, width);
    }

    public void AddMultiline(Rid rid, Vector2[] points, Color[] colors, float width)
    {
        if (Find(rid) is not { } item || colors.Length == 0)
            return;

        for (int i = 0; i + 1 < points.Length; i += 2)
        {
            Color color = colors.Length == 1 ? colors[0] : colors[Math.Min(i / 2, colors.Length - 1)];
            item.Segment(points[i], points[i + 1], color, color, width);
        }
    }

    public void AddPolygon(Rid rid, Vector2[] points, Color[] colors, Vector2[]? uvs = null)
    {
        if (Find(rid) is not { } item || points.Length < 3 || colors.Length == 0)
            return;

        int[] indices = Triangulate.Polygon(points);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            item.Triangle(
                points[a], points[b], points[c],
                ColorAt(colors, a), ColorAt(colors, b), ColorAt(colors, c),
                default,
                uvs is null ? default : (uvs[a], uvs[b], uvs[c]));
        }
    }

    public void AddTriangleArray(Rid rid, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Rid texture = default)
    {
        if (Find(rid) is not { } item || colors.Length == 0)
            return;

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (a >= points.Length || b >= points.Length || c >= points.Length)
                break;

            item.Triangle(
                points[a], points[b], points[c],
                ColorAt(colors, a), ColorAt(colors, b), ColorAt(colors, c),
                texture.Id,
                uvs is null ? default : (uvs[a], uvs[b], uvs[c]));
        }
    }

    public void AddTextureRect(Rid rid, Rect2 rect, Rid texture)
    {
        if (Find(rid) is not { } item)
            return;

        Vector2 a = rect.Position, c = rect.End;
        item.Quad(a, new Vector2(c.X, a.Y), c, new Vector2(a.X, c.Y), Colors.White, texture.Id);
    }

    public void AddSetTransform(Rid rid, Transform2D transform)
    {
        if (Find(rid) is { } item)
            item.CommandTransform = transform;
    }

    private static Color ColorAt(Color[] colors, int index) =>
        colors.Length == 1 ? colors[0] : colors[Math.Min(index, colors.Length - 1)];
}

/// <summary>One canvas item: its place in the tree and its triangles, in its own space.</summary>
internal sealed class CanvasItem(ulong id)
{
    public readonly ulong Id = id;
    public CanvasItem? Parent;
    public readonly List<CanvasItem> Children = new();
    public bool ChildrenSorted = true;
    public long Order;
    public int Z;
    public int DrawIndex;
    public bool Visible = true;
    public Transform2D Transform = Transform2D.Identity;
    public Color Modulate = Colors.White;
    public Color SelfModulate = Colors.White;
    public bool UseCustomRect;
    public Rect2 CustomRect;
    public bool Clip;

    /// <summary>What the item's own draws shade and blend with; null is the plain canvas shader.</summary>
    public CanvasMaterial? Material;

    /// <summary>Draws into an image of its own, composited as one (a CanvasGroup).</summary>
    public bool Group;

    /// <summary>Refreshes the screen copy just before the item draws.</summary>
    public bool CopyScreen;

    /// <summary>Values for this item only, over the material's; null when none were set.</summary>
    public Dictionary<string, Vector4>? InstanceValues;

    /// <summary>Set by <c>AddSetTransform</c>: applied to the commands that follow.</summary>
    public Transform2D CommandTransform = Transform2D.Identity;

    public readonly List<CanvasVertex> Vertices = new();

    /// <summary>GPU particles drawn after the item's own draws; made by the first burst into it.</summary>
    public ParticleField? Particles;

    /// <summary>Where each texture's vertices start; a new run begins whenever the texture changes.</summary>
    public readonly List<(int Start, ulong Texture)> Runs = new();

    public void ClearCommands()
    {
        Vertices.Clear();
        Runs.Clear();
        CommandTransform = Transform2D.Identity;
    }

    public void SortChildren()
    {
        if (ChildrenSorted)
            return;

        Children.Sort(static (a, b) => a.DrawIndex != b.DrawIndex
            ? a.DrawIndex.CompareTo(b.DrawIndex)
            : a.Order.CompareTo(b.Order));
        ChildrenSorted = true;
    }

    private void UseTexture(ulong texture)
    {
        if (Runs.Count == 0 || Runs[^1].Texture != texture)
            Runs.Add((Vertices.Count, texture));
    }

    public void Triangle(
        Vector2 a, Vector2 b, Vector2 c,
        Color ca, Color cb, Color cc,
        ulong texture,
        (Vector2 A, Vector2 B, Vector2 C) uv = default)
    {
        UseTexture(texture);
        Transform2D t = CommandTransform;
        Vector2 pa = t * a, pb = t * b, pc = t * c;
        Vertices.Add(new CanvasVertex(pa, uv.A, ca, pa));
        Vertices.Add(new CanvasVertex(pb, uv.B, cb, pb));
        Vertices.Add(new CanvasVertex(pc, uv.C, cc, pc));
    }

    public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, ulong texture)
    {
        Triangle(a, b, c, color, color, color, texture, (new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1)));
        Triangle(a, c, d, color, color, color, texture, (new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1)));
    }

    /// <summary>A straight line as a quad; widths under one pixel draw one pixel wide.</summary>
    public void Segment(Vector2 from, Vector2 to, Color start, Color end, float width)
    {
        Vector2 along = to - from;
        float length = along.Length();
        if (length <= 0f)
            return;

        Vector2 side = new Vector2(-along.Y, along.X) / length * (MathF.Max(width, 1f) / 2f);
        Triangle(from + side, to + side, to - side, start, end, end, 0);
        Triangle(from + side, to - side, from - side, start, end, start, 0);
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
            Triangle(p + offsets[i], q + offsets[i + 1], q - offsets[i + 1], a, b, b, 0);
            Triangle(p + offsets[i], q - offsets[i + 1], p - offsets[i], a, b, a, 0);
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
/// One vertex as the GPU reads it: pixel position, texture UV, colour, and its
/// position in the item's own space (an effect's <c>local</c>, Godot's VERTEX).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct CanvasVertex(Vector2 position, Vector2 uv, Color color, Vector2 local) : MoonWorks.Graphics.IVertexType
{
    public float X = position.X, Y = position.Y, U = uv.X, V = uv.Y;
    public float R = color.R, G = color.G, B = color.B, A = color.A;
    public float LX = local.X, LY = local.Y;

    public static MoonWorks.Graphics.VertexElementFormat[] Formats { get; } =
    [
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float2,
        MoonWorks.Graphics.VertexElementFormat.Float4,
        MoonWorks.Graphics.VertexElementFormat.Float2,
    ];

    public static uint[] Offsets { get; } = [0, 8, 16, 32];
}
