using System.Collections.Generic;
using Godot;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// The note glyphs of the UI font, blitted once into a texture for shaders to
/// sample: one cell per glyph, laid out left to right.
/// </summary>
public static class NoteAtlas
{
    /// <summary>The glyphs, in cell order. The shader picks a cell by index.</summary>
    public static readonly string[] Glyphs = ["♪", "♫"];

    private const int CellSize = 256;
    private const int FontSize = 200;

    private static DrawableTexture2D? _texture;
    private static readonly List<ImageTexture> KeepAlive = new();

    public static Texture2D Get()
    {
        if (_texture is not null)
            return _texture;

        _texture = new DrawableTexture2D();
        _texture.Setup(
            CellSize * Glyphs.Length,
            CellSize,
            DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0),
            false);

        Font font = ThemeDB.FallbackFont;
        for (int i = 0; i < Glyphs.Length; i++)
        {
            float width = font.GetStringSize(
                Glyphs[i], HorizontalAlignment.Left, -1, FontSize).X;
            var pen = new Vector2(
                CellSize * i + (CellSize - width) / 2,
                CellSize / 2f + FontSize * 0.33f);
            GodotGlyphBlitter.Blit(_texture, KeepAlive, Glyphs[i], FontSize, font, pen);
        }

        return _texture;
    }
}
