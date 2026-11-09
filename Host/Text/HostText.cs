using System;
using System.Collections.Generic;
using System.IO;
using FreeTypeSharp;
using Godot;
using MoonWorks.Graphics;
using Texture = MoonWorks.Graphics.Texture;
using Xanadu.Storage;
using static FreeTypeSharp.FT;

namespace Xanadu.Host.Text;

/// <summary>Which face a piece of text is set in.</summary>
internal enum FontKind { Regular, Bold, Mono }

/// <summary>
/// The host's text: owns the font files (the one place they are read), the
/// faces made from them, and the glyph images cached in the
/// <see cref="TextAtlas"/>. Lays strings out into atlas quads for
/// <see cref="HostRectCanvas"/> to hand to the canvas.
/// </summary>
/// <remarks>
/// Regular text is Source Sans 3 at weight 400 and bold the same file at 700
/// (Godot drew regular text in its built-in Open Sans); mono is JetBrains Mono.
/// </remarks>
internal sealed unsafe class HostText : IDisposable
{
    private const string SansPath = "res://Assets/Fonts/SourceSans3-Variable.ttf";
    private const string MonoPath = "res://Assets/Fonts/JetBrainsMono-Regular.ttf";

    /// <summary>One glyph image: its face, size, glyph, and what was made of it.</summary>
    private readonly record struct Key(int Face, int Size, uint Glyph, GlyphLook Look, float A, float B);

    private enum GlyphLook : byte { Fill, Outline, Glow }

    private readonly FT_LibraryRec_* _library;
    private readonly FontFace[] _faces;
    private readonly Dictionary<Key, AtlasGlyph> _glyphs = new();
    private readonly List<ShapedGlyph> _shaped = new();

    public TextAtlas Atlas { get; }

    public HostText(GraphicsDevice device, Func<Texture, ulong> registerTexture)
    {
        FT_LibraryRec_* library;
        if (FT_Init_FreeType(&library) != 0)
            throw new InvalidOperationException("FreeType did not start.");
        _library = library;

        byte[] sans = File.ReadAllBytes(GamePaths.Globalize(SansPath));
        byte[] mono = File.ReadAllBytes(GamePaths.Globalize(MonoPath));
        _faces =
        [
            new FontFace(library, sans, 0, "Source Sans 3", weight: 400),
            new FontFace(library, sans, 1, "Source Sans 3 Bold", weight: 700),
            new FontFace(library, mono, 2, "JetBrains Mono"),
        ];
        Atlas = new TextAtlas(device, registerTexture);
    }

    public FontFace Face(FontKind kind) => _faces[(int)kind];

    /// <summary>A glyph placed on the canvas: its atlas image and the top-left pixel it goes at.</summary>
    public readonly record struct Placed(AtlasGlyph Glyph, float X, float Y);

    /// <summary>
    /// Shapes <paramref name="text"/> and places each glyph with its pen
    /// starting at <paramref name="origin"/> on the baseline. With
    /// <paramref name="outline"/> above 0 the glyphs are outlines that many
    /// pixels wide; with <paramref name="glowRadius"/> above 0 they are blurred
    /// halos (<see cref="GlowBlur"/>).
    /// </summary>
    public void Place(
        string text, FontKind kind, int size, Vector2 origin, List<Placed> into,
        float outline = 0, float glowRadius = 0, float glowIntensity = 1)
    {
        into.Clear();
        FontFace face = Face(kind);
        face.Shape(text, size, _shaped);
        float baseline = MathF.Round(origin.Y);
        foreach (ShapedGlyph shaped in _shaped)
        {
            if (shaped.Index == 0)
                continue;

            AtlasGlyph glyph = glowRadius > 0
                ? Glyph(face, size, shaped.Index, GlyphLook.Glow, glowRadius, glowIntensity)
                : outline > 0
                    ? Glyph(face, size, shaped.Index, GlyphLook.Outline, outline, 0)
                    : Glyph(face, size, shaped.Index, GlyphLook.Fill, 0, 0);
            if (glyph.IsEmpty)
                continue;

            // Whole pixels, as Godot draws glyphs with subpixel positioning off.
            float x = MathF.Round(origin.X + shaped.X) + glyph.Left;
            float y = baseline + MathF.Round(shaped.Y) - glyph.Top;
            into.Add(new Placed(glyph, x, y));
        }
    }

    /// <summary>
    /// Coverage of <paramref name="text"/> rendered at <paramref name="size"/> in
    /// a regular face, as one bitmap with its top-left relative to a pen at the
    /// origin on the baseline: a <c>GlyphAtlas</c> cell's ink.
    /// </summary>
    public GlyphBitmap RenderInk(string text, int size)
    {
        FontFace face = Face(FontKind.Regular);
        face.Shape(text, size, _shaped);
        var pieces = new List<(GlyphBitmap Bitmap, int X, int Y)>();
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (ShapedGlyph shaped in _shaped)
        {
            if (shaped.Index == 0)
                continue;

            GlyphBitmap bitmap = face.Render(shaped.Index, size, 0);
            if (bitmap.Width == 0 || bitmap.Height == 0)
                continue;

            int x = (int)MathF.Round(shaped.X) + bitmap.Left, y = (int)MathF.Round(shaped.Y) - bitmap.Top;
            pieces.Add((bitmap, x, y));
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + bitmap.Width);
            bottom = Math.Max(bottom, y + bitmap.Height);
        }

        if (pieces.Count == 0)
            return new GlyphBitmap(0, 0, 0, 0, []);

        int w = right - left, h = bottom - top;
        var coverage = new byte[w * h];
        foreach ((GlyphBitmap bitmap, int x, int y) in pieces)
            for (int row = 0; row < bitmap.Height; row++)
                for (int col = 0; col < bitmap.Width; col++)
                {
                    ref byte target = ref coverage[(y - top + row) * w + x - left + col];
                    target = Math.Max(target, bitmap.Coverage[row * bitmap.Width + col]);
                }

        return new GlyphBitmap(w, h, left, -top, coverage);
    }

    /// <summary>The advance of the whole string, in pixels.</summary>
    public float Width(string text, FontKind kind, int size) => Face(kind).Shape(text, size, _shaped);

    private AtlasGlyph Glyph(FontFace face, int size, uint index, GlyphLook look, float a, float b)
    {
        var key = new Key(face.Id, size, index, look, a, b);
        if (_glyphs.TryGetValue(key, out AtlasGlyph cached))
            return cached;

        GlyphBitmap bitmap = look switch
        {
            // Godot's outline size is the stroke's full width; half of it shows outside the glyph.
            GlyphLook.Outline => face.Render(index, size, a / 2f),
            GlyphLook.Glow => GlowBlur.Halo(face.Render(index, size, 0), a, b),
            _ => face.Render(index, size, 0),
        };
        AtlasGlyph placed = Atlas.Add(bitmap);
        _glyphs.Add(key, placed);
        return placed;
    }

    public void Dispose()
    {
        foreach (FontFace face in _faces)
            face.Dispose();
        Atlas.Dispose();
        FT_Done_FreeType(_library);
    }
}
