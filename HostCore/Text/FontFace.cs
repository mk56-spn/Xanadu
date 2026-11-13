using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using HarfBuzzSharp;
using static FreeTypeSharp.FT;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbFont = HarfBuzzSharp.Font;

namespace Xanadu.Host.Text;

/// <summary>One glyph placed by shaping: the face it comes from (a fallback's for a character the main face lacks), its index there, and its pen offset, in pixels.</summary>
internal readonly record struct ShapedGlyph(FontFace Face, uint Index, float X, float Y);

/// <summary>A glyph's coverage as FreeType rendered it: one byte per pixel, and where it sits against the pen.</summary>
internal readonly record struct GlyphBitmap(int Width, int Height, int Left, int Top, byte[] Coverage);

/// <summary>
/// One font file at one weight: HarfBuzz shapes the text (kerning, ligatures,
/// GPOS) and FreeType renders and strokes the glyphs. Variable fonts get the
/// weight on both, so advances and outlines agree.
/// </summary>
internal sealed unsafe class FontFace : IDisposable
{
    private const int LoadTargetLight = 1 << 16; // FT_LOAD_TARGET_LIGHT: vertical-only hinting, Godot's default.

    private readonly FT_LibraryRec_* _library;
    private readonly FT_FaceRec_* _face;
    private readonly GCHandle _bytes;
    private readonly Blob _blob;
    private readonly Face _hbFace;
    private readonly HbFont _hbFont;
    private FT_StrokerRec_* _stroker;
    private int _size = -1;

    /// <summary>Small and stable, for glyph cache keys.</summary>
    public int Id { get; }

    public string Name { get; }

    /// <summary>
    /// Faces tried, in order, for a character this one lacks (symbols, emoji),
    /// as Godot fell back to other fonts; a character none has stays in this face.
    /// </summary>
    public FontFace[] Fallbacks { get; set; } = [];

    /// <summary>Whether the font has a glyph for <paramref name="codepoint"/>.</summary>
    public bool Has(int codepoint) => FT_Get_Char_Index(_face, (nuint)codepoint) != 0;

    public FontFace(FT_LibraryRec_* library, byte[] data, int id, string name, float? weight = null)
    {
        _library = library;
        Id = id;
        Name = name;
        _bytes = GCHandle.Alloc(data, GCHandleType.Pinned);
        byte* bytes = (byte*)_bytes.AddrOfPinnedObject();

        FT_FaceRec_* face;
        Check(FT_New_Memory_Face(library, bytes, data.Length, 0, &face), "load " + name);
        _face = face;

        _blob = new Blob(_bytes.AddrOfPinnedObject(), data.Length, MemoryMode.ReadOnly);
        _hbFace = new Face(_blob, 0);
        _hbFont = new HbFont(_hbFace);
        _hbFont.SetFunctionsOpenType();

        if (weight is { } wght)
            SetWeight(wght);
    }

    /// <summary>Sets the <c>wght</c> axis on FreeType and HarfBuzz; other axes keep their defaults.</summary>
    private void SetWeight(float weight)
    {
        FreeTypeVariations.MMVar* mm;
        if (FreeTypeVariations.FT_Get_MM_Var(_face, &mm) != 0)
            return;

        try
        {
            uint count = mm->num_axis;
            var coords = new nint[count];
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                FreeTypeVariations.VarAxis axis = mm->axis[i];
                coords[i] = axis.def;
                if (axis.tag == Tag("wght"))
                {
                    coords[i] = (nint)(Math.Clamp(weight, axis.minimum / 65536f, axis.maximum / 65536f) * 65536f);
                    found = true;
                }
            }

            if (!found)
                return;

            fixed (nint* c = coords)
                FreeTypeVariations.FT_Set_Var_Design_Coordinates(_face, count, c);

            var variation = new FreeTypeVariations.HbVariation { Tag = Tag("wght"), Value = weight };
            FreeTypeVariations.hb_font_set_variations(_hbFont.Handle, &variation, 1);
        }
        finally
        {
            FreeTypeVariations.FT_Done_MM_Var(_library, mm);
        }
    }

    private static uint Tag(string tag) =>
        (uint)tag[0] << 24 | (uint)tag[1] << 16 | (uint)tag[2] << 8 | tag[3];

    private void UseSize(int size)
    {
        if (_size == size)
            return;

        Check(FT_Set_Pixel_Sizes(_face, 0, (uint)size), "size " + Name);
        _size = size;
    }

    /// <summary>Pixels from the baseline up to the top of the line, as Godot's <c>Font.GetAscent</c>.</summary>
    public float Ascent(int size)
    {
        UseSize(size);
        return _face->size->metrics.ascender / 64f;
    }

    /// <summary>Line height (ascent plus descent), as Godot's <c>Font.GetHeight</c>.</summary>
    public float Height(int size)
    {
        UseSize(size);
        return (_face->size->metrics.ascender - _face->size->metrics.descender) / 64f;
    }

    /// <summary>Shapes <paramref name="text"/> at <paramref name="size"/> pixels, into <paramref name="glyphs"/>; returns the total advance.</summary>
    /// <summary>Shaped runs by text and size: UI text is mostly the same strings every frame, and shaping is HarfBuzz plus a scale change each time.</summary>
    private readonly Dictionary<(string Text, int Size), (ShapedGlyph[] Glyphs, float Width)> _shaped = new();

    /// <summary>Past this many runs the cache starts over, so ever-changing text (timers, counters) can't grow it without bound.</summary>
    private const int MaxShaped = 4096;

    public float Shape(string text, int size, List<ShapedGlyph> glyphs)
    {
        glyphs.Clear();
        if (_shaped.TryGetValue((text, size), out (ShapedGlyph[] Glyphs, float Width) run))
        {
            glyphs.AddRange(run.Glyphs);
            return run.Width;
        }

        float width = ShapeUncached(text, size, glyphs);
        if (_shaped.Count >= MaxShaped)
            _shaped.Clear();
        _shaped[(text, size)] = (glyphs.ToArray(), width);
        return width;
    }

    /// <summary>
    /// Shapes the text in runs, each in the first face that has its characters:
    /// this one, else a fallback. Spaces and marks stay in the run before them.
    /// </summary>
    private float ShapeUncached(string text, int size, List<ShapedGlyph> glyphs)
    {
        if (Fallbacks.Length == 0)
            return ShapeRun(text, size, 0, glyphs);

        float pen = 0;
        int runStart = 0;
        FontFace? runFace = null;
        int index = 0;
        foreach (System.Text.Rune rune in text.EnumerateRunes())
        {
            FontFace face = runFace is not null && Joins(rune) ? runFace : For(rune.Value);
            if (runFace is not null && face != runFace)
            {
                pen += runFace.ShapeRun(text[runStart..index], size, pen, glyphs);
                runStart = index;
            }

            runFace = face;
            index += rune.Utf16SequenceLength;
        }

        if (runFace is not null)
            pen += runFace.ShapeRun(text[runStart..], size, pen, glyphs);
        return pen;
    }

    /// <summary>Spaces, joiners, variation selectors and marks go with the run they follow.</summary>
    private static bool Joins(System.Text.Rune rune) =>
        System.Text.Rune.IsWhiteSpace(rune) ||
        System.Text.Rune.GetUnicodeCategory(rune) is
            System.Globalization.UnicodeCategory.NonSpacingMark or
            System.Globalization.UnicodeCategory.EnclosingMark or
            System.Globalization.UnicodeCategory.Format ||
        rune.Value is >= 0xFE00 and <= 0xFE0F;

    private FontFace For(int codepoint)
    {
        if (Has(codepoint))
            return this;

        foreach (FontFace fallback in Fallbacks)
        {
            if (fallback.Has(codepoint))
                return fallback;
        }

        return this;
    }

    /// <summary>Shapes one run in this face, its pen starting at <paramref name="start"/>; returns its advance.</summary>
    private float ShapeRun(string text, int size, float start, List<ShapedGlyph> glyphs)
    {
        _hbFont.SetScale(size * 64, size * 64);
        using var buffer = new HbBuffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        _hbFont.Shape(buffer);

        ReadOnlySpan<GlyphInfo> infos = buffer.GetGlyphInfoSpan();
        ReadOnlySpan<GlyphPosition> positions = buffer.GetGlyphPositionSpan();
        float pen = 0;
        for (int i = 0; i < infos.Length; i++)
        {
            GlyphPosition p = positions[i];
            glyphs.Add(new ShapedGlyph(this, infos[i].Codepoint, start + pen + p.XOffset / 64f, -p.YOffset / 64f));
            pen += p.XAdvance / 64f;
        }

        return pen;
    }

    /// <summary>
    /// Renders one glyph's coverage; with <paramref name="outline"/> above 0,
    /// the glyph grown by that many pixels instead (its outer stroke, filled).
    /// </summary>
    public GlyphBitmap Render(uint index, int size, float outline)
    {
        UseSize(size);
        if (FT_Load_Glyph(_face, index, (FT_LOAD)LoadTargetLight) != 0)
            return default;

        if (outline <= 0)
        {
            FT_GlyphSlotRec_* slot = _face->glyph;
            if (FT_Render_Glyph(slot, FT_Render_Mode_.FT_RENDER_MODE_NORMAL) != 0)
                return default;
            return Copy(slot->bitmap, slot->bitmap_left, slot->bitmap_top);
        }

        if (_stroker == null)
        {
            FT_StrokerRec_* stroker;
            Check(FT_Stroker_New(_library, &stroker), "stroker");
            _stroker = stroker;
        }

        FT_Stroker_Set(
            _stroker,
            (nint)(outline * 64),
            FT_Stroker_LineCap_.FT_STROKER_LINECAP_ROUND,
            FT_Stroker_LineJoin_.FT_STROKER_LINEJOIN_ROUND,
            0);

        FT_GlyphRec_* glyph;
        if (FT_Get_Glyph(_face->glyph, &glyph) != 0)
            return default;

        try
        {
            // The outside border only: the glyph grown by the stroke radius, drawn under the fill.
            if (FT_Glyph_StrokeBorder(&glyph, _stroker, 0, 1) != 0)
                return default;
            if (FT_Glyph_To_Bitmap(&glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL, null, 1) != 0)
                return default;

            var bitmapGlyph = (FreeTypeVariations.BitmapGlyph*)glyph;
            return Copy(bitmapGlyph->bitmap, bitmapGlyph->left, bitmapGlyph->top);
        }
        finally
        {
            FT_Done_Glyph(glyph);
        }
    }

    private static GlyphBitmap Copy(FT_Bitmap_ bitmap, int left, int top)
    {
        int width = (int)bitmap.width, height = (int)bitmap.rows;
        if (width == 0 || height == 0)
            return new GlyphBitmap(0, 0, left, top, []);

        var coverage = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            byte* row = bitmap.buffer + (bitmap.pitch >= 0 ? y : y - height + 1) * bitmap.pitch;
            new ReadOnlySpan<byte>(row, width).CopyTo(coverage.AsSpan(y * width, width));
        }

        return new GlyphBitmap(width, height, left, top, coverage);
    }

    private static void Check(FT_Error error, string what)
    {
        if (error != 0)
            throw new InvalidOperationException($"FreeType could not {what} (error {(int)error}).");
    }

    public void Dispose()
    {
        _hbFont.Dispose();
        _hbFace.Dispose();
        _blob.Dispose();
        if (_stroker != null)
            FT_Stroker_Done(_stroker);
        FT_Done_Face(_face);
        _bytes.Free();
    }
}

/// <summary>The variable-font calls FreeTypeSharp and HarfBuzzSharp don't bind.</summary>
internal static unsafe class FreeTypeVariations
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VarAxis
    {
        public byte* name;
        public nint minimum, def, maximum; // FT_Fixed (16.16 in a C long)
        public nuint tag;
        public uint strid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MMVar
    {
        public uint num_axis, num_designs, num_namedstyles;
        public VarAxis* axis;
        public void* namedstyle;
    }

    /// <summary>FT_BitmapGlyphRec: what FT_Glyph_To_Bitmap turns a glyph into.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapGlyph
    {
        public FT_GlyphRec_ root;
        public int left, top;
        public FT_Bitmap_ bitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HbVariation
    {
        public uint Tag;
        public float Value;
    }

    [DllImport("freetype")]
    public static extern int FT_Get_MM_Var(FT_FaceRec_* face, MMVar** master);

    [DllImport("freetype")]
    public static extern int FT_Done_MM_Var(FT_LibraryRec_* library, MMVar* master);

    [DllImport("freetype")]
    public static extern int FT_Set_Var_Design_Coordinates(FT_FaceRec_* face, uint count, nint* coords);

    [DllImport("libHarfBuzzSharp")]
    public static extern void hb_font_set_variations(IntPtr font, HbVariation* variations, uint length);
}
