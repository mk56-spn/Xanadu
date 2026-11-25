namespace Xanadu.Slang;

/// <summary>
/// An image a graph declares: <c>[Image(format)] RWTexture2D&lt;T&gt; name;</c>, with a <c>scale:</c> or fixed size
/// (<see cref="ImageSize"/>) and a <c>clear:</c> value if wanted. Each item drawn gets its own for the frame (but for
/// one a <c>[Pass]</c> touches, which is the material's): the runner takes it from a pool and clears it to its value
/// (0 when none) before its first node. Passes read and write it at <c>group.pixel</c>; <c>present</c> shows it.
/// </summary>
/// <param name="Format">Its texel format as Slang names it: rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui.</param>
/// <param name="Clear">What every texel holds before the item's first node touches it.</param>
/// <param name="Life">How long what it holds lasts: the frame, or longer.</param>
/// <param name="Source">For a <see cref="ImageLife.History"/>, the image (its place) it is last frame's copy of; -1 otherwise.</param>
/// <param name="Size">How big it is: the item's box (the default), a part of it, or fixed.</param>
public sealed record BuildImage(string Name, string Format, string Type, float Clear, int Start, int End,
    ImageLife Life = ImageLife.Frame, int Source = -1, ImageSize Size = default);

/// <summary>
/// An image's size: <c>[Image(format)]</c> is the item's box, <c>[Image(format, scale: s)]</c> the box times s (rounded
/// up), <c>[Image(format, w, h)]</c> fixed. Only a fixed image can be the material's, written by a <c>[Pass]</c>.
/// </summary>
/// <param name="Scale">Texels per pixel of the box; 0 means the default, 1.</param>
public readonly record struct ImageSize(float Scale = 0, int Width = 0, int Height = 0)
{
    public bool Fixed => Width > 0;

    /// <summary>Whether it is the box's own size, texel for pixel.</summary>
    public bool Box => !Fixed && (Scale == 0 || Scale == 1);

    /// <summary>The texels it covers for a box this big: fixed, or the box scaled and rounded up.</summary>
    public (uint Width, uint Height) For(int width, int height) => Fixed
        ? ((uint)Width, (uint)Height)
        : Box ? ((uint)width, (uint)height) : (Scaled(width), Scaled(height));

    private uint Scaled(int size) => (uint)Math.Max(1, Math.Ceiling(size * Scale));
}

/// <summary>How long an image's texels last.</summary>
public enum ImageLife
{
    /// <summary>The frame: taken from a pool, cleared, given back when the frame is done.</summary>
    Frame,

    /// <summary><c>[Kept]</c>: the item's own from frame to frame, cleared only when made (and when its box's size changes).</summary>
    Kept,

    /// <summary>
    /// <c>[Cached]</c>: held like a kept image, but filled only when it goes stale: when it is made (a new compile, a new
    /// size, a new item) or the material's values or host buffers change. A node that touches only cached images and
    /// writes no buffer is skipped in between (<see cref="GraphNode.Fills"/>), so drawing a cached image costs a read.
    /// </summary>
    Cached,

    /// <summary>
    /// <c>[History(source)] RWTexture2D&lt;T&gt; name;</c>: what the source held at the end of last frame, cleared like the source
    /// on the first. The runner swaps the two each frame, so nothing is copied.
    /// </summary>
    History,
}
