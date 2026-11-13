using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Timeline;

namespace Xanadu.Levels;

/// <summary>Serializable level document.</summary>
public sealed class LevelDocument : IChartItems
{
    public int Version { get; set; } = 2;

    /// <summary>Stable level identity; unrelated to its file path.</summary>
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

    /// <summary>Stable human-readable level slug.</summary>
    public string Slug { get; set; } = "";

    /// <summary>The program-owned song this chart references.</summary>
    public string SongId { get; set; } = "";

    public string Name { get; set; } = "Untitled";


    public int SnapDivision { get; set; } = 4;

    /// <summary>A new level's playfield, in units on each side; the editor and the play camera scroll round it.</summary>
    public const int DefaultFieldSize = 5000;

    public int PlayfieldWidth { get; set; } = DefaultFieldSize;

    public int PlayfieldHeight { get; set; } = DefaultFieldSize;

    /// <summary>The playfield background as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string BackgroundColor { get; set; } = "";

    /// <summary>The colour around the playfield as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string OutsideColor { get; set; } = "";

    /// <summary>The playfield's frame and border line as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string FrameColor { get; set; } = "";

    public List<NoteDocument> Notes { get; set; } = new();

    public List<MineDocument> Mines { get; set; } = new();

    public List<StartPointDocument> StartPoints { get; set; } = new();

    public List<BlockDocument> Blocks { get; set; } = new();

    /// <summary>Spike tiles: permanent hazards that kill on contact.</summary>
    public List<SpikeDocument> Spikes { get; set; } = new();

    /// <summary>Friction bars: crossing one turns the ground's friction on.</summary>
    public List<FrictionBarDocument> FrictionBars { get; set; } = new();

    /// <summary>Text written into the level.</summary>
    public List<TextDocument> Texts { get; set; } = new();

    /// <summary>Grass patches: swaying blades with no collision.</summary>
    public List<GrassDocument> Grass { get; set; } = new();

    /// <summary>Lens flares: shader-drawn light with no collision.</summary>
    public List<FlareDocument> Flares { get; set; } = new();

    /// <summary>Filled shapes with no collision.</summary>
    public List<PolygonDocument> Polygons { get; set; } = new();

    /// <summary>Screen-read regions: rects that refresh the back buffer where the draw order reaches them.</summary>
    public List<ScreenReadDocument> ScreenReads { get; set; } = new();

    /// <summary>The groups that have a name or effects; the rest are empty slots.</summary>
    public List<GroupDocument> Groups { get; set; } = new();

    /// <summary>The layers that have a name or a pass; every other layer number is a plain layer.</summary>
    public List<LayerDocument> Layers { get; set; } = new();

    /// <summary>Custom shaders, in slots 1 to <see cref="ShaderLimits.Count"/>.</summary>
    public List<ShaderDocument> Shaders { get; set; } = new();

    /// <summary>The shader slot the player is drawn with, or 0 for none.</summary>
    public int PlayerShader { get; set; }

    /// <summary>The shader slot the playfield background is drawn with, or 0 for none.</summary>
    public int FieldShader { get; set; }
}

/// <summary>
/// A shader a level carries with it: the source, copied in so the level is
/// self-contained, and the uniform values tuned for it.
/// </summary>
public sealed class ShaderDocument
{
    /// <summary>The slot, 1 to <see cref="ShaderLimits.Count"/>, items refer to.</summary>
    public int Slot { get; set; }

    public string Name { get; set; } = "";

    public string Source { get; set; } = "";

    /// <summary>Tuned uniform values by name; vectors and colours are one float per channel.</summary>
    public Dictionary<string, float[]> Params { get; set; } = new();
}

/// <summary>
/// Serializable layer table entry (Docs/render-architecture.md step 5): a
/// layer's name and its pass, with the pass shader's keyed settings.
/// </summary>
public sealed class LayerDocument
{
    /// <summary>The layer, −200…−1 or 1…200.</summary>
    public int Number { get; set; }

    public string Name { get; set; } = "";

    public LayerPassKind Pass { get; set; }

    /// <summary>The shader slot the pass draws with, or 0 for none.</summary>
    public int PassShader { get; set; }

    public List<ShaderKeyDocument> PassKeys { get; set; } = new();
}

/// <summary>Serializable shader key: an instance uniform's value (1 to 4 floats) at a time, in seconds.</summary>
public sealed class ShaderKeyDocument
{
    public string Param { get; set; } = "";

    public float Time { get; set; }

    public float[] Value { get; set; } = [];

    public EasePreset Ease { get; set; } = EasePreset.Linear;
}

/// <summary>Serializable group: a slot number, a name and its timed effects.</summary>
public sealed class GroupDocument
{
    /// <summary>The slot, 0 to <see cref="GroupLimits.Count"/> - 1, items refer to.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public List<GroupEffectDocument> Effects { get; set; } = new();

    /// <summary>The shader slot its item-shader track draws with, or 0 for none.</summary>
    public int Shader { get; set; }

    /// <summary>The track's keyed instance-uniform values.</summary>
    public List<ShaderKeyDocument> ShaderKeys { get; set; } = new();

    /// <summary>Whether its members draw into one buffer first (D12).</summary>
    public bool Buffered { get; set; }

    /// <summary>The buffer's tint as #rrggbbaa.</summary>
    public string BufferTint { get; set; } = "#ffffffff";

    /// <summary>The buffer's alpha, multiplied with the tint's.</summary>
    public float BufferAlpha { get; set; } = 1f;

    /// <summary>The shader slot the buffer is drawn with, or 0 for none.</summary>
    public int BufferShader { get; set; }

    /// <summary>The buffer shader's keyed instance values.</summary>
    public List<ShaderKeyDocument> BufferKeys { get; set; } = new();

    /// <summary>How the buffer blends into its layer.</summary>
    public Xanadu.Rendering.BlendMode BufferBlend { get; set; }
}

/// <summary>Serializable group effect; times are seconds.</summary>
public sealed class GroupEffectDocument
{
    public GroupEffectKind Kind { get; set; }

    public float Start { get; set; }

    public float End { get; set; }

    /// <summary>How the effect travels between <see cref="Start"/> and <see cref="End"/>.</summary>
    public EasePreset Ease { get; set; } = EasePreset.Linear;

    /// <summary>A muted effect stays in the group but contributes nothing.</summary>
    public bool Muted { get; set; }

    /// <summary>The move's offset, for a move.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The target colour as #rrggbbaa, for a colour effect.</summary>
    public string Tint { get; set; } = "#ffffffff";

    /// <summary>How many times it plays (1 = once), a new cycle every <see cref="EveryBeats"/> beats.</summary>
    public int Cycles { get; set; } = 1;

    public float EveryBeats { get; set; } = 1f;

    public GroupCycle Cycle { get; set; } = GroupCycle.Restart;
}

/// <summary>
/// What every placed item stores the same way, whatever its kind: its groups,
/// shader, shader source, layer, z, blend and colour. The file format writes it
/// once per item as a shared header, so a new kind or a new shared setting is
/// one change, not one per kind.
/// </summary>
public abstract class ItemDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups the item belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The layer, −200…−1 behind the player or 1…200 in front; 0 means the kind's default.</summary>
    public int Layer { get; set; }

    /// <summary>The order inside the layer; higher draws in front.</summary>
    public int Z { get; set; }

    /// <summary>How its pixels combine with the layer, Docs/render-architecture.md D17.</summary>
    public Xanadu.Rendering.BlendMode Blend { get; set; }

    /// <summary>The group slot whose item-shader track it follows, or -1 for its own shader.</summary>
    public int ShaderSource { get; set; } = -1;

    /// <summary>The shader slot it is drawn with, or 0 for none; kinds that draw otherwise ignore it.</summary>
    public int Shader { get; set; }
}

/// <summary>Serializable collidable block data.</summary>
public sealed class BlockDocument : ItemDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    /// <summary>The only side that collides, as a <see cref="Xanadu.Ecs.BlockOneWay"/>; 0 is solid all round.</summary>
    public int OneWay { get; set; }

    /// <summary>Outline thickness in playfield units; 0 is no outline.</summary>
    public float OutlineWidth { get; set; } = Xanadu.Ecs.Block.DefaultOutlineWidth;

    /// <summary>The outline's colour, as RGBA8.</summary>
    public uint OutlineColor { get; set; } = Xanadu.Ecs.Block.DefaultOutlineColor.ToRgba32();

    /// <summary>The groups whose colour fades reach the interior.</summary>
    public uint FillGroups { get; set; } = Xanadu.Ecs.Block.AllGroups;

    /// <summary>The groups whose colour fades reach the outline.</summary>
    public uint OutlineGroups { get; set; } = Xanadu.Ecs.Block.AllGroups;

    /// <summary>A ramp, as a <see cref="Xanadu.Ecs.BlockSlope"/>; 0 is a box.</summary>
    public int Slope { get; set; }
}

/// <summary>Serializable spike tile data: a hazard that kills on contact.</summary>
public sealed class SpikeDocument : ItemDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    /// <summary>Where the tip points, in degrees clockwise from up.</summary>
    public float RotationDegrees { get; set; }

    /// <summary>Outline thickness in playfield units; 0 is no outline.</summary>
    public float OutlineWidth { get; set; } = Xanadu.Ecs.Block.DefaultOutlineWidth;

    /// <summary>The outline's colour, as RGBA8.</summary>
    public uint OutlineColor { get; set; } = Xanadu.Ecs.Block.DefaultOutlineColor.ToRgba32();
}

/// <summary>A friction bar: an upright line that turns ground friction on when crossed.</summary>
public sealed class FrictionBarDocument : ItemDocument
{
    /// <summary>Top-left corner of the bar's box, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    public float Height { get; set; }
}

/// <summary>A grass patch: blades standing along a base line, growing up from it.</summary>
public sealed class GrassDocument : ItemDocument
{
    /// <summary>The middle of the base line, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; } = Xanadu.Ecs.GrassPatch.DefaultWidth;

    public float Length { get; set; } = Xanadu.Ecs.GrassPatch.DefaultLength;

    /// <summary>Blades per 100 playfield units of width.</summary>
    public float Density { get; set; } = Xanadu.Ecs.GrassPatch.DefaultDensity;

    /// <summary>How far a blade leans at the most, in degrees.</summary>
    public float Sway { get; set; } = Xanadu.Ecs.GrassPatch.DefaultSway;

    /// <summary>The blades' colour, as RGBA8.</summary>
    public uint Color { get; set; } = Xanadu.Ecs.GrassPatch.DefaultColor.ToRgba32();

    /// <summary>How far each blade's colour may stray, 0 to 1.</summary>
    public float Variation { get; set; } = Xanadu.Ecs.GrassPatch.DefaultVariation;

    /// <summary>Picks where each blade stands and how it looks.</summary>
    public uint Seed { get; set; }

    /// <summary>How far the patch is turned about the centre of its box, clockwise, in degrees.</summary>
    public float Rotation { get; set; }
}

/// <summary>A lens flare: a core, a halo and one or two streaks across a turned box, drawn by a shader.</summary>
public sealed class FlareDocument : ItemDocument
{
    public FlareDocument() => Blend = Xanadu.Rendering.BlendMode.Add;

    /// <summary>The middle of the box, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; } = Xanadu.Ecs.LevelFlare.DefaultWidth;

    public float Height { get; set; } = Xanadu.Ecs.LevelFlare.DefaultHeight;

    /// <summary>1 for a streak along the width, 2 for one along the height as well.</summary>
    public int Axes { get; set; } = 1;

    public float Thickness { get; set; } = Xanadu.Ecs.LevelFlare.DefaultThickness;

    public float Falloff { get; set; } = Xanadu.Ecs.LevelFlare.DefaultFalloff;

    public float Core { get; set; } = Xanadu.Ecs.LevelFlare.DefaultCore;

    public float Halo { get; set; } = Xanadu.Ecs.LevelFlare.DefaultHalo;

    public float Glow { get; set; } = Xanadu.Ecs.LevelFlare.DefaultGlow;

    public float Intensity { get; set; } = Xanadu.Ecs.LevelFlare.DefaultIntensity;

    public float Heat { get; set; } = Xanadu.Ecs.LevelFlare.DefaultHeat;

    /// <summary>The flare's colour, as RGBA8.</summary>
    public uint Color { get; set; } = Xanadu.Ecs.LevelFlare.DefaultColor.ToRgba32();

    /// <summary>How far the flare is turned about its centre, clockwise, in degrees.</summary>
    public float Rotation { get; set; }
}

/// <summary>A filled shape: an item of polygons around an origin, turned about it.</summary>
public sealed class PolygonDocument : ItemDocument
{
    /// <summary>The item's origin, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>How far the shape is turned about its origin, clockwise, in degrees.</summary>
    public float Rotation { get; set; }

    /// <summary>The polygons, their outlines in the item's own space.</summary>
    public Xif Item { get; set; } = new();
}

/// <summary>Text written into a level: a centred label on the playfield.</summary>
public sealed class TextDocument : ItemDocument
{
    /// <summary>The centre of the text, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The height of a line, in playfield units.</summary>
    public float Size { get; set; } = Xanadu.Ecs.LevelText.DefaultSize;

    public string Text { get; set; } = "";

    /// <summary>How far the text is turned about its centre, clockwise, in degrees.</summary>
    public float Rotation { get; set; }
}

/// <summary>
/// A screen-read region: a rect that refreshes the back buffer when the draw
/// order reaches it (Docs/render-architecture.md step 11).
/// </summary>
public sealed class ScreenReadDocument : ItemDocument
{
    /// <summary>The top-left corner, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; } = Xanadu.Ecs.ScreenReadRegion.DefaultWidth;

    public float Height { get; set; } = Xanadu.Ecs.ScreenReadRegion.DefaultHeight;
}

/// <summary>Serializable practice start state.</summary>
public sealed class StartPointDocument : ItemDocument
{
    public string Name { get; set; } = "";

    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float MomentumX { get; set; }

    public float MomentumY { get; set; }

    public float DirectionX { get; set; }

    public float DirectionY { get; set; } = -1;
}

/// <summary>Serializable mine hazard data.</summary>
public sealed class MineDocument : ItemDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public long FadeInMs { get; set; } = 600;

    public long FadeOutMs { get; set; } = 600;
}

/// <summary>Serializable note data.</summary>
public sealed class NoteDocument : ItemDocument
{
    /// <summary>The self-contained item a note is: the primitives and shaders it uses.</summary>
    public Xif Item { get; set; } = new();

    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The action key a coloured note asks for; ignored by other kinds.</summary>
    public KeybindAction Target { get; set; } = KeybindAction.Action1;

    public NoteKind Kind { get; set; } = NoteKind.Hit;

    /// <summary>One, two or three dash lengths for a dash note.</summary>
    public DashStrength DashStrength { get; set; } = DashStrength.Single;

    /// <summary>A momentum note's speed in playfield units per second; ignored by other kinds.</summary>
    public float Momentum { get; set; } = Note.DefaultMomentum;

    /// <summary>A rhombus directional note: turns the pressed way 45° clockwise.</summary>
    public bool Diagonal { get; set; }

    /// <summary>What a Contact barrier asks of each of the three bars.</summary>
    public int CostFirst { get; set; }

    public int CostSecond { get; set; }

    public int CostThird { get; set; }
}
