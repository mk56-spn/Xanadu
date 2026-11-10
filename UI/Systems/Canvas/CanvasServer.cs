using System;

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
    Handle CreateItem();

    void Free(Handle item);

    void SetParent(Handle item, Handle parent);

    /// <summary>Draw layer, relative to the parent's: higher draws over lower across the whole tree.</summary>
    void SetZIndex(Handle item, int z);

    /// <summary>Order among siblings: higher draws later.</summary>
    void SetDrawIndex(Handle item, int index);

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

    /// <summary>One value for this item only, over its material's (Godot's instance uniforms).</summary>
    void SetInstanceValue(Handle item, string name, Vector4 value);

    /// <summary>
    /// Refreshes the screen copy (what screen-reading shaders sample) just
    /// before the item draws: Godot's copy-to-back-buffer. Hosts that copy
    /// whenever a screen reader needs it may treat this as a hint.
    /// </summary>
    void SetCopyScreen(Handle item, bool copy);

    /// <summary>Draws the item's children into one image composited as a whole (a CanvasGroup).</summary>
    void SetCanvasGroup(Handle item, float clearMargin, bool fitEmpty, float fitMargin);

    void Clear(Handle item);

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

    /// <summary>A texture stretched over a rect.</summary>
    void AddTextureRect(Handle item, Rect2 rect, Handle texture);

    /// <summary>Transforms the item's later commands until the next one.</summary>
    void AddSetTransform(Handle item, Transform2D transform);
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
    public void SetDrawIndex(Handle item, int index) { }
    public void SetVisible(Handle item, bool visible) { }
    public void SetTransform(Handle item, Transform2D transform) { }
    public void SetModulate(Handle item, Color color) { }
    public void SetSelfModulate(Handle item, Color color) { }
    public void SetCustomRect(Handle item, bool use, Rect2 rect) { }
    public void SetClip(Handle item, bool clip) { }
    public void SetMaterial(Handle item, CanvasMaterial? material) { }
    public void SetInstanceValue(Handle item, string name, Vector4 value) { }
    public void SetCopyScreen(Handle item, bool copy) { }
    public void SetCanvasGroup(Handle item, float clearMargin, bool fitEmpty, float fitMargin) { }
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
