using System;
using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// The retained canvas-item tree the game draws into: items with a parent,
/// transform, order, visibility, clip and colour, each holding a list of draw
/// commands until it is cleared. The calls began as the subset of Godot's
/// <c>RenderingServer</c> the game used; the MoonWorks host renders the tree
/// (docs/moonworks-port.md step 2).
/// </summary>
public interface ICanvasServer
{
    Rid CreateItem();

    void Free(Rid item);

    void SetParent(Rid item, Rid parent);

    /// <summary>Draw layer, relative to the parent's: higher draws over lower across the whole tree.</summary>
    void SetZIndex(Rid item, int z);

    /// <summary>Order among siblings: higher draws later.</summary>
    void SetDrawIndex(Rid item, int index);

    void SetVisible(Rid item, bool visible);

    void SetTransform(Rid item, Transform2D transform);

    /// <summary>Multiplies the item and its children.</summary>
    void SetModulate(Rid item, Color color);

    /// <summary>Multiplies the item's own draws only.</summary>
    void SetSelfModulate(Rid item, Color color);

    /// <summary>The item's own rect, which <see cref="SetClip"/> clips children to.</summary>
    void SetCustomRect(Rid item, bool use, Rect2 rect);

    void SetClip(Rid item, bool clip);

    /// <summary>
    /// What the item's own draws are shaded and blended with; null for the
    /// plain canvas shader. Values changed on the material later are picked up
    /// when the item next draws.
    /// </summary>
    void SetMaterial(Rid item, CanvasMaterial? material);

    /// <summary>One value for this item only, over its material's (Godot's instance uniforms).</summary>
    void SetInstanceValue(Rid item, string name, Vector4 value);

    /// <summary>
    /// Refreshes the screen copy (what screen-reading shaders sample) just
    /// before the item draws: Godot's copy-to-back-buffer. Hosts that copy
    /// whenever a screen reader needs it may treat this as a hint.
    /// </summary>
    void SetCopyScreen(Rid item, bool copy);

    /// <summary>Draws the item's children into one image composited as a whole (a CanvasGroup).</summary>
    void SetCanvasGroup(Rid item, float clearMargin, bool fitEmpty, float fitMargin);

    void Clear(Rid item);

    void AddRect(Rid item, Rect2 rect, Color color);

    void AddCircle(Rid item, Vector2 center, float radius, Color color);

    void AddLine(Rid item, Vector2 from, Vector2 to, Color color, float width, bool antialiased);

    void AddPolyline(Rid item, Vector2[] points, Color[] colors, float width, bool antialiased);

    /// <summary>Separate segments, two points each; one colour for all or one per segment. Width below 0 is a plain one-pixel line.</summary>
    void AddMultiline(Rid item, Vector2[] points, Color[] colors, float width);

    /// <summary>A convex or concave polygon; one colour for all or one per point.</summary>
    void AddPolygon(Rid item, Vector2[] points, Color[] colors, Vector2[]? uvs = null);

    /// <summary>Indexed triangles, optionally textured by <paramref name="texture"/> (a texture id from the same server).</summary>
    void AddTriangleArray(Rid item, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Rid texture = default);

    /// <summary>A texture stretched over a rect.</summary>
    void AddTextureRect(Rid item, Rect2 rect, Rid texture);

    /// <summary>Transforms the item's later commands until the next one.</summary>
    void AddSetTransform(Rid item, Transform2D transform);
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

    public Rid CreateItem()
    {
        ulong id = _nextId++;
        return System.Runtime.CompilerServices.Unsafe.As<ulong, Rid>(ref id);
    }

    public void Free(Rid item) { }
    public void SetParent(Rid item, Rid parent) { }
    public void SetZIndex(Rid item, int z) { }
    public void SetDrawIndex(Rid item, int index) { }
    public void SetVisible(Rid item, bool visible) { }
    public void SetTransform(Rid item, Transform2D transform) { }
    public void SetModulate(Rid item, Color color) { }
    public void SetSelfModulate(Rid item, Color color) { }
    public void SetCustomRect(Rid item, bool use, Rect2 rect) { }
    public void SetClip(Rid item, bool clip) { }
    public void SetMaterial(Rid item, CanvasMaterial? material) { }
    public void SetInstanceValue(Rid item, string name, Vector4 value) { }
    public void SetCopyScreen(Rid item, bool copy) { }
    public void SetCanvasGroup(Rid item, float clearMargin, bool fitEmpty, float fitMargin) { }
    public void Clear(Rid item) { }
    public void AddRect(Rid item, Rect2 rect, Color color) { }
    public void AddCircle(Rid item, Vector2 center, float radius, Color color) { }
    public void AddLine(Rid item, Vector2 from, Vector2 to, Color color, float width, bool antialiased) { }
    public void AddPolyline(Rid item, Vector2[] points, Color[] colors, float width, bool antialiased) { }
    public void AddMultiline(Rid item, Vector2[] points, Color[] colors, float width) { }
    public void AddPolygon(Rid item, Vector2[] points, Color[] colors, Vector2[]? uvs = null) { }
    public void AddTriangleArray(Rid item, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Rid texture = default) { }
    public void AddTextureRect(Rid item, Rect2 rect, Rid texture) { }
    public void AddSetTransform(Rid item, Transform2D transform) { }
}
