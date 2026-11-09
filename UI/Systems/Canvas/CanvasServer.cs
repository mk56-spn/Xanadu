using System;
using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// The retained canvas-item tree the game draws into: items with a parent,
/// transform, order, visibility, clip and colour, each holding a list of draw
/// commands until it is cleared. The calls mirror the subset of Godot's
/// <c>RenderingServer</c> the game uses, so the Godot backend forwards them and
/// the MoonWorks host renders its own tree (docs/moonworks-port.md step 2).
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
/// The canvas server the game draws through. Godot's by default; the MoonWorks
/// host installs its own with <see cref="Use"/> before building the world.
/// </summary>
public static class CanvasServer
{
    public static ICanvasServer Current { get; private set; } = new GodotCanvasServer();

    public static void Use(ICanvasServer server) => Current = server ?? throw new ArgumentNullException(nameof(server));
}

/// <summary>Forwards to Godot's <c>RenderingServer</c>.</summary>
public sealed class GodotCanvasServer : ICanvasServer
{
    public Rid CreateItem() => RenderingServer.CanvasItemCreate();

    public void Free(Rid item) => RenderingServer.FreeRid(item);

    public void SetParent(Rid item, Rid parent) => RenderingServer.CanvasItemSetParent(item, parent);

    public void SetZIndex(Rid item, int z) => RenderingServer.CanvasItemSetZIndex(item, z);

    public void SetDrawIndex(Rid item, int index) => RenderingServer.CanvasItemSetDrawIndex(item, index);

    public void SetVisible(Rid item, bool visible) => RenderingServer.CanvasItemSetVisible(item, visible);

    public void SetTransform(Rid item, Transform2D transform) => RenderingServer.CanvasItemSetTransform(item, transform);

    public void SetModulate(Rid item, Color color) => RenderingServer.CanvasItemSetModulate(item, color);

    public void SetSelfModulate(Rid item, Color color) => RenderingServer.CanvasItemSetSelfModulate(item, color);

    public void SetCustomRect(Rid item, bool use, Rect2 rect) => RenderingServer.CanvasItemSetCustomRect(item, use, rect);

    public void SetClip(Rid item, bool clip) => RenderingServer.CanvasItemSetClip(item, clip);

    // Godot is reference only (docs/moonworks-port.md P2): its build draws
    // engine-free materials plain.
    public void SetMaterial(Rid item, CanvasMaterial? material) { }

    public void SetInstanceValue(Rid item, string name, Vector4 value) { }

    public void SetCopyScreen(Rid item, bool copy) =>
        RenderingServer.CanvasItemSetCopyToBackbuffer(item, copy, new Rect2());

    public void SetCanvasGroup(Rid item, float clearMargin, bool fitEmpty, float fitMargin) =>
        RenderingServer.CanvasItemSetCanvasGroupMode(
            item,
            RenderingServer.CanvasGroupMode.Transparent,
            clearMargin,
            fitEmpty,
            fitMargin,
            blurMipmaps: false);

    public void Clear(Rid item) => RenderingServer.CanvasItemClear(item);

    public void AddRect(Rid item, Rect2 rect, Color color) => RenderingServer.CanvasItemAddRect(item, rect, color);

    public void AddCircle(Rid item, Vector2 center, float radius, Color color) =>
        RenderingServer.CanvasItemAddCircle(item, center, radius, color, true);

    public void AddLine(Rid item, Vector2 from, Vector2 to, Color color, float width, bool antialiased) =>
        RenderingServer.CanvasItemAddLine(item, from, to, color, width, antialiased);

    public void AddPolyline(Rid item, Vector2[] points, Color[] colors, float width, bool antialiased) =>
        RenderingServer.CanvasItemAddPolyline(item, points, colors, width, antialiased);

    public void AddMultiline(Rid item, Vector2[] points, Color[] colors, float width) =>
        RenderingServer.CanvasItemAddMultiline(item, points, colors, width);

    public void AddPolygon(Rid item, Vector2[] points, Color[] colors, Vector2[]? uvs = null)
    {
        if (uvs is null)
            RenderingServer.CanvasItemAddPolygon(item, points, colors);
        else
            RenderingServer.CanvasItemAddPolygon(item, points, colors, uvs);
    }

    public void AddTriangleArray(Rid item, int[] indices, Vector2[] points, Color[] colors, Vector2[]? uvs = null, Rid texture = default)
    {
        if (uvs is null)
            RenderingServer.CanvasItemAddTriangleArray(item, indices, points, colors, texture: texture);
        else
            RenderingServer.CanvasItemAddTriangleArray(item, indices, points, colors, uvs, texture: texture);
    }

    public void AddTextureRect(Rid item, Rect2 rect, Rid texture) =>
        RenderingServer.CanvasItemAddTextureRect(item, rect, texture);

    public void AddSetTransform(Rid item, Transform2D transform) =>
        RenderingServer.CanvasItemAddSetTransform(item, transform);
}
