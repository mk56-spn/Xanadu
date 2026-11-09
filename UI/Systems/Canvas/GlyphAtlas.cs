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
    bool mipmaps = false,
    Font? font = null)
{
    private readonly List<ImageTexture> _keepAlive = new();
    private DrawableTexture2D? _texture;

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
    /// The strip as a canvas texture id, for <see cref="CanvasMaterial.SetTexture"/>:
    /// built by the host when it builds glyph strips itself, else Godot's texture.
    /// </summary>
    public Rid TextureId => _textureId ??= HostPlatform.GlyphStrip is { } build ? build(this) : Texture.GetRid();

    /// <summary>The strip, built the first time it is asked for.</summary>
    public Texture2D Texture => _texture ??= Build();
    private DrawableTexture2D Build()
    {
        var texture = new DrawableTexture2D();
        texture.Setup(
            CellSize * Count,
            CellSize,
            DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0),
            mipmaps);

        Font face = font ?? ThemeDB.FallbackFont;
        float room = CellSize * fill;
        for (int i = 0; i < Count; i++)
        {
            // Measured at the cell size, then scaled so the larger side fills the room.
            Rect2 measured = GodotGlyphBlitter.InkBounds(Glyphs[i], CellSize, face);
            if (measured.Size.X <= 0 || measured.Size.Y <= 0)
                continue;

            float scale = room / Mathf.Max(measured.Size.X, measured.Size.Y);
            int fontSize = Mathf.Max(1, Mathf.FloorToInt(CellSize * scale));
            Rect2 ink = GodotGlyphBlitter.InkBounds(Glyphs[i], fontSize, face);
            Vector2 centre = new(CellSize * (i + 0.5f), CellSize * 0.5f);
            GodotGlyphBlitter.Blit(texture, _keepAlive, Glyphs[i], fontSize, face, centre - ink.GetCenter());
        }

        if (mipmaps)
            texture.GenerateMipmaps();

        return texture;
    }
}
