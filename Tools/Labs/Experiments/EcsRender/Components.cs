using Friflo.Engine.ECS;

namespace Xanadu.Labs.Experiments.EcsRender;

// What an entity is drawn from. No canvas item, no handle, no tree: an entity with
// these is on the stage, and one without them is not. Logic systems write them;
// render systems only read them (and keep LabelGlyphs).

/// <summary>Where the entity is on the stage (pixels) and how far it is turned (radians, clockwise).</summary>
internal struct Placed : IComponent
{
    public Vector2 Position;
    public float Turn;
}

/// <summary>Its draw order: the layer first, then z inside it; equal ones keep the order they were found in.</summary>
internal struct Depth : IComponent
{
    public short Layer, Z;
}

internal struct Tint : IComponent
{
    public Color Value;
}

internal enum ShapeKind : byte { Box, Disc, Ring }

/// <summary>A shape centred on the entity's place. <see cref="Edge"/> is a box's corner radius or a ring's width.</summary>
internal struct Shape : IComponent
{
    public ShapeKind Kind;
    public Vector2 Size;
    public float Edge;
}

/// <summary>A line of monospaced text whose top-left is the entity's place.</summary>
internal struct Label : IComponent
{
    public string Text;
    public int Size;
}

/// <summary>
/// A label's glyphs, laid out from its own corner. The renderer's own: it fills
/// this in when the label's text or size is no longer what it was made from.
/// </summary>
internal struct LabelGlyphs : IComponent
{
    public string? From;
    public int Size;
    public GlyphQuad[]? Quads;
    public float Width, Height;
}

/// <summary>One glyph of a laid-out label: its box from the label's corner, and where its image is in the atlas.</summary>
internal readonly record struct GlyphQuad(float X, float Y, float Width, float Height, Vector4 Uv, short Page);

/// <summary>The lab's stand-in for game state: something circling a home point.</summary>
internal struct Drift : IComponent
{
    public Vector2 Home;
    public float Radius, Speed, Phase;
}

/// <summary>Counts frames; its entity's label says how many.</summary>
internal struct FrameCounter : IComponent
{
    public long Frames;
}
