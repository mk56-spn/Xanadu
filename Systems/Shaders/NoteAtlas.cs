using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Shaders;

/// <summary>The note glyphs the game draws through shaders and particles.</summary>
public static class NoteAtlas
{
    /// <summary>The glyphs, in cell order. A shader picks a cell by index.</summary>
    public static readonly string[] Glyphs = ["♪", "♫"];

    /// <summary>
    /// Large cells for full-screen shader backgrounds, mipmapped so small notes
    /// stay smooth and the glow can read a blurred copy of each glyph.
    /// </summary>
    public static readonly GlyphAtlas Background = new(Glyphs, cellSize: 256, mipmaps: true);

    /// <summary>Small, mipmapped cells for particles a few dozen pixels across.</summary>
    public static readonly GlyphAtlas Particles = new(Glyphs, cellSize: 64, mipmaps: true);
}
