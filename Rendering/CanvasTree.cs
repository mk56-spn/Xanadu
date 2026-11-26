using System.Collections.Generic;
using Xanadu.Core.Rendering;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering;

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

    public void AddCopies(Handle rid, CanvasMesh mesh, ReadOnlySpan<CanvasCopy> copies, ReadOnlySpan<Vector4> values)
    {
        if (Find(rid) is not { } item || copies.IsEmpty)
            return;

        (item.Copies ??= []).Add(new CopyDraw(item.Primitives.Count, item.CurrentRun, mesh, copies.ToArray(), values.ToArray()));
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
