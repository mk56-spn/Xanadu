using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// A strip of text glyphs blitted once into a texture, so shaders and
/// particles can draw them without text rendering: one square cell per glyph,
/// left to right, white coverage in the alpha channel and each glyph's ink
/// centred in its cell. Any string works as a glyph — a letter, a symbol such
/// as ♪, or an emoji the font falls back to the system for.
/// </summary>
/// <remarks>
/// Pick a cell near the size the glyphs are drawn at; with
/// <paramref name="mipmaps"/> on, smaller draws stay smooth when the canvas
/// item filters with mipmaps.
/// </remarks>
public sealed class GlyphAtlas(
    IReadOnlyList<string> glyphs,
    int cellSize = 256,
    float fill = 0.8f,
    bool mipmaps = false)
{

    /// <summary>The glyphs, in cell order; cell <c>i</c> holds <c>Glyphs[i]</c>.</summary>
    public IReadOnlyList<string> Glyphs { get; } = glyphs;

    /// <summary>How many cells the strip has.</summary>
    public int Count => Glyphs.Count;

    /// <summary>The side of one cell, in pixels.</summary>
    public int CellSize { get; } = cellSize;

    /// <summary>How much of a cell the larger side of each glyph fills.</summary>
    public float Fill { get; } = fill;

    /// <summary>Whether the strip has mipmaps, for drawing it smaller than its cells.</summary>
    public bool Mipmaps { get; } = mipmaps;

    private Rid? _textureId;

    /// <summary>
    /// The strip as a canvas texture id, for <see cref="CanvasMaterial.SetTexture"/>,
    /// built once by the host; the default id where nothing draws (tests).
    /// </summary>
    public Rid TextureId => _textureId ??= HostPlatform.GlyphStrip is { } build ? build(this) : default;
}
