using System.Collections.Generic;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// The canvas the game draws into: items (recordings) that keep their draws
/// until cleared, with a transform, visibility, clip, colour and material,
/// hung under one another at a z or listed each frame by a mount
/// (<see cref="ICanvasMount"/>, how the UI draws). The host keeps their
/// primitives on the GPU and places them there (Docs/host.md §5.3).
/// </summary>
public interface ICanvasServer
{
    Handle CreateItem();

    void Free(Handle item);

    void SetParent(Handle item, Handle parent);

    /// <summary>Draw layer, relative to the parent's: higher draws over lower across the whole tree.</summary>
    void SetZIndex(Handle item, int z);


    void SetVisible(Handle item, bool visible);

    void SetTransform(Handle item, Transform2D transform);

    /// <summary>Multiplies the item and its children.</summary>
    void SetModulate(Handle item, Color color);

    /// <summary>Multiplies the item's own draws only.</summary>
    void SetSelfModulate(Handle item, Color color);

    /// <summary>The item's own rect, which <see cref="SetClip"/> clips children to.</summary>
    void SetCustomRect(Handle item, bool use, Rect2 rect);

    void SetClip(Handle item, bool clip);

    /// <summary>
    /// What the item's own draws are shaded and blended with; null for the
    /// plain canvas shader. Values changed on the material later are picked up
    /// when the item next draws.
    /// </summary>
    void SetMaterial(Handle item, CanvasMaterial? material);




    void Clear(Handle item);

    /// <summary>
    /// Makes <paramref name="item"/>'s own draws the look of each particle emitted
    /// into it: drawn once per live particle, placed, turned, sized and tinted by it,
    /// through whatever shader they were drawn with, and never on their own. Draw
    /// them around the item's origin, about 1 unit to a side's half: a particle's
    /// size scales that. Off, the particles draw as dots or strip cells.
    /// </summary>
    void SetParticleMesh(Handle item, bool on) { }

    void AddRect(Handle item, Rect2 rect, Color color);

    void AddCircle(Handle item, Vector2 center, float radius, Color color);

    void AddLine(Handle item, Vector2 from, Vector2 to, Color color, float width, bool antialiased);

    void AddPolyline(Handle item, Vector2[] points, Color[] colors, float width, bool antialiased);

    /// <summary>Separate segments, two points each; one colour for all or one per segment. Width below 0 is a plain one-pixel line.</summary>
    void AddMultiline(Handle item, Vector2[] points, Color[] colors, float width);

    /// <summary>A convex or concave polygon; one colour for all or one per point.</summary>
    void AddPolygon(Handle item, Vector2[] points, Color[] colors, Vector2[]? uvs = null);

    /// <summary>Indexed triangles, optionally textured by <paramref name="texture"/> (a texture id from the same server).</summary>
    void AddTriangleArray(Handle item, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Handle texture = default);

    /// <summary>
    /// Triangles three points each, with a curve tag per triangle
    /// (<see cref="CanvasCurves"/>): the GPU trims tagged ones to their
    /// quadratic curve. A server that cannot draws each curve as its chord.
    /// </summary>
    void AddCurveTriangles(Handle item, Vector2[] points, Color[] colors, Vector2[] uvs, byte[] curves)
    {
        (Vector2[] kept, Color[] keptColors, Vector2[] keptUvs) = CanvasCurves.Straighten(points, colors, uvs, curves);
        var indices = new int[kept.Length];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;
        AddTriangleArray(item, indices, kept, keptColors, keptUvs);
    }

    /// <summary>A texture stretched over a rect.</summary>
    void AddTextureRect(Handle item, Rect2 rect, Handle texture);

    /// <summary>Transforms the item's later commands until the next one.</summary>
    void AddSetTransform(Handle item, Transform2D transform);

    /// <summary>A region of a texture over a rect, tinted: one glyph or sprite.</summary>
    void AddTextureRegion(Handle item, Rect2 rect, Rect2 uv, Color color, Handle texture)
    {
        Vector2 a = rect.Position, c = rect.End, ua = uv.Position, uc = uv.End;
        AddTriangleArray(item, [0, 1, 2, 0, 2, 3], [a, new(c.X, a.Y), c, new(a.X, c.Y)], [color],
            [ua, new(uc.X, ua.Y), uc, new(ua.X, uc.Y)], texture);
    }

    /// <summary>
    /// Hangs <paramref name="mount"/> under <paramref name="item"/>: each frame,
    /// after the item's children, the renderer asks it which items to draw
    /// there and where (an ECS tree that keeps its own order, transforms and
    /// visibility instead of mirroring them into parented items).
    /// </summary>
    void Mount(Handle item, ICanvasMount mount) { }

    void Unmount(Handle item, ICanvasMount mount) { }

    /// <summary>
    /// Shades the item's draws that follow with <paramref name="material"/> and
    /// <paramref name="values"/> (a run of their own: an effect over just
    /// those, in their place among the item's other draws); null for plain.
    /// The same material and values object keep the run going.
    /// </summary>
    void SelectRun(Handle item, CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? values) { }
}

/// <summary>Something that lists, each frame, the items it wants drawn where it is mounted.</summary>
public interface ICanvasMount
{
    void Collect(ICanvasCollector into);
}

/// <summary>What a mount lists its items into, in draw order.</summary>
public interface ICanvasCollector
{
    /// <summary>
    /// Draws <paramref name="item"/> (an item with no parent) and its own
    /// children here: placed by <paramref name="transform"/>, faded by
    /// <paramref name="modulate"/>, at layer <paramref name="z"/>, all relative to
    /// the mount, and cut to <paramref name="clip"/> (in the mount's space) if given.
    /// </summary>
    void Item(Handle item, in Transform2D transform, Color modulate, int z, Rect2? clip);
}

/// <summary>
/// The canvas server the game draws through. Until a host installs its own with
/// <see cref="Use"/> it is <see cref="HeadlessCanvasServer"/>, which keeps no
/// pictures, so tests and tools that never draw need no setup.
/// </summary>
public static class CanvasServer
{
    public static ICanvasServer Current { get; private set; } = new HeadlessCanvasServer();

    public static void Use(ICanvasServer server) => Current = server ?? throw new ArgumentNullException(nameof(server));
}

/// <summary>Hands out item ids and ignores every draw: a canvas with nobody looking at it.</summary>
public sealed class HeadlessCanvasServer : ICanvasServer
{
    private ulong _nextId = 1;

    public Handle CreateItem() => new(_nextId++);

    public void Free(Handle item) { }
    public void SetParent(Handle item, Handle parent) { }
    public void SetZIndex(Handle item, int z) { }
    public void SetVisible(Handle item, bool visible) { }
    public void SetTransform(Handle item, Transform2D transform) { }
    public void SetModulate(Handle item, Color color) { }
    public void SetSelfModulate(Handle item, Color color) { }
    public void SetCustomRect(Handle item, bool use, Rect2 rect) { }
    public void SetClip(Handle item, bool clip) { }
    public void SetMaterial(Handle item, CanvasMaterial? material) { }
    public void Clear(Handle item) { }
    public void AddRect(Handle item, Rect2 rect, Color color) { }
    public void AddCircle(Handle item, Vector2 center, float radius, Color color) { }
    public void AddLine(Handle item, Vector2 from, Vector2 to, Color color, float width, bool antialiased) { }
    public void AddPolyline(Handle item, Vector2[] points, Color[] colors, float width, bool antialiased) { }
    public void AddMultiline(Handle item, Vector2[] points, Color[] colors, float width) { }
    public void AddPolygon(Handle item, Vector2[] points, Color[] colors, Vector2[]? uvs = null) { }
    public void AddTriangleArray(Handle item, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Handle texture = default) { }
    public void AddTextureRect(Handle item, Rect2 rect, Handle texture) { }
    public void AddSetTransform(Handle item, Transform2D transform) { }
}
