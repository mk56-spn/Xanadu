using System;
using Godot;
using Godot.Collections;
using Xanadu.Particles;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Particles;

/// <summary>
/// What every particle system shares: the soft dot, the note glyph strip and
/// the material that draws one cell of it per particle, and the quads the
/// particles are drawn as. One kit per canvas owner; instances borrow from it.
/// </summary>
public sealed class ParticleKit : IDisposable
{
    private const float TextureSize = 16f;

    private readonly ImageTexture _softDot;
    private readonly GlyphAtlas _notes;
    private readonly CanvasItemMaterial _noteFrames;
    private readonly Rid _quad;
    private readonly Rid _stripQuad;

    private static ParticleKit? _shared;

    /// <summary>One kit for whatever draws particles outside a pool of its own, such as rig effects; never freed.</summary>
    public static ParticleKit Shared => _shared ??= new ParticleKit();

    public ParticleKit()
    {
        _softDot = SoftDot();
        _notes = NoteAtlas.Particles;
        _noteFrames = NoteFrames(_notes);
        _quad = Quad(1);
        _stripQuad = Quad(_notes.Count);
    }

    public Texture2D Texture(XpfTexture texture) =>
        texture == XpfTexture.NoteGlyphs ? _notes.Texture : _softDot;

    public Rid Mesh(XpfTexture texture) => texture == XpfTexture.NoteGlyphs ? _stripQuad : _quad;

    /// <summary>The canvas item material a texture needs, or null for none.</summary>
    public CanvasItemMaterial? Frames(XpfTexture texture) =>
        texture == XpfTexture.NoteGlyphs ? _noteFrames : null;

    public void Dispose()
    {
        RenderingServer.FreeRid(_quad);
        RenderingServer.FreeRid(_stripQuad);
        _softDot.Dispose();
        _noteFrames.Dispose();
    }

    /// <summary>A white dot with soft edges; the process material colours and scales it.</summary>
    private static ImageTexture SoftDot()
    {
        int size = (int)TextureSize;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).Length() / radius;
                float alpha = Mathf.Clamp(1f - distance, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// The canvas item material that draws one cell of the note strip per
    /// particle: the process material's animation offset, random from 0 to 1,
    /// picks the cell, and with no animation speed the particle keeps it.
    /// </summary>
    private static CanvasItemMaterial NoteFrames(GlyphAtlas atlas) => new()
    {
        ParticlesAnimation = true,
        ParticlesAnimHFrames = atlas.Count,
        ParticlesAnimVFrames = 1,
        ParticlesAnimLoop = false,
    };

    /// <summary>
    /// The draw pass: one quad centred on the particle, <paramref name="frames"/>
    /// cells wide. A strip of animation frames needs the whole strip's width,
    /// because <see cref="CanvasItemMaterial"/> divides the vertices by the
    /// frame counts to bring the quad down to one cell; a square quad would be
    /// squashed to a sliver of a cell.
    /// </summary>
    private static Rid Quad(int frames)
    {
        float half = TextureSize / 2f;
        float halfWide = half * frames;
        Rid mesh = RenderingServer.MeshCreate();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)RenderingServer.ArrayType.Max);
        arrays[(int)RenderingServer.ArrayType.Vertex] = new[]
        {
            new Vector2(-halfWide, -half), new Vector2(halfWide, -half),
            new Vector2(halfWide, half), new Vector2(-halfWide, half),
        };
        arrays[(int)RenderingServer.ArrayType.TexUV] = new[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
        };
        arrays[(int)RenderingServer.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        RenderingServer.MeshAddSurfaceFromArrays(
            mesh,
            RenderingServer.PrimitiveType.Triangles,
            arrays,
            new Godot.Collections.Array(),
            new Dictionary(),
            RenderingServer.ArrayFormat.FlagUse2DVertices);
        return mesh;
    }
}
