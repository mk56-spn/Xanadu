using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using System.Collections.Generic;

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

    /// <summary>Text written into the level.</summary>
    public List<TextDocument> Texts { get; set; } = new();

    /// <summary>Grass patches: swaying blades with no collision.</summary>
    public List<GrassDocument> Grass { get; set; } = new();

    /// <summary>The groups that have a name or effects; the rest are empty slots.</summary>
    public List<GroupDocument> Groups { get; set; } = new();

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

/// <summary>Serializable group: a slot number, a name and its timed effects.</summary>
public sealed class GroupDocument
{
    /// <summary>The slot, 0 to <see cref="GroupLimits.Count"/> - 1, items refer to.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public List<GroupEffectDocument> Effects { get; set; } = new();
}

/// <summary>Serializable group effect; times are seconds.</summary>
public sealed class GroupEffectDocument
{
    public GroupEffectKind Kind { get; set; }

    public float Start { get; set; }

    public float End { get; set; }

    /// <summary>How the effect travels between <see cref="Start"/> and <see cref="End"/>.</summary>
    public GroupEase Ease { get; set; } = GroupEase.Linear;

    /// <summary>A muted effect stays in the group but contributes nothing.</summary>
    public bool Muted { get; set; }

    /// <summary>The move's offset, for a move.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The target colour as #rrggbbaa, for a colour effect.</summary>
    public string Tint { get; set; } = "#ffffffff";
}

/// <summary>Serializable collidable block data.</summary>
public sealed class BlockDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this block belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    /// <summary>The Form construction shape this block belongs to, or 0 for permanent geometry.</summary>
    public int ShapeId { get; set; }

    /// <summary>The order this block is revealed in its shape.</summary>
    public int ShapePiece { get; set; }

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
}

/// <summary>Serializable spike tile data: a hazard that kills on contact.</summary>
public sealed class SpikeDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this spike belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

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

/// <summary>A grass patch: blades standing along a base line, growing up from it.</summary>
public sealed class GrassDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this patch belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

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
}

/// <summary>Text written into a level: a centred label on the playfield.</summary>
public sealed class TextDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this text belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

    /// <summary>The centre of the text, in playfield units.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The height of a line, in playfield units.</summary>
    public float Size { get; set; } = Xanadu.Ecs.LevelText.DefaultSize;

    public string Text { get; set; } = "";
}

/// <summary>Serializable practice start state.</summary>
public sealed class StartPointDocument
{
    /// <summary>The groups this start point belongs to.</summary>
    public List<int> Groups { get; set; } = new();

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
public sealed class MineDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this mine belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public long FadeInMs { get; set; } = 600;

    public long FadeOutMs { get; set; } = 600;
}

/// <summary>Serializable note data.</summary>
public sealed class NoteDocument
{
    /// <summary>The colour its drawing is multiplied by, as RGBA8; white changes nothing.</summary>
    public uint SelfModulate { get; set; } = Xanadu.Ecs.SelfModulate.White;

    /// <summary>The groups this note belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The self-contained item a note is: the primitives and shaders it uses.</summary>
    public Xif Item { get; set; } = new();

    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public KeybindAction Target { get; set; } = KeybindAction.M;

    public NoteKind Kind { get; set; } = NoteKind.Hit;

    public DashDirection DashDirection { get; set; } = DashDirection.Up;

    public int ContactRequired { get; set; } = 1;

    public CharacterKind Character { get; set; } = CharacterKind.Contact;

    /// <summary>One, two or three dash lengths for a dash note.</summary>
    public DashStrength DashStrength { get; set; } = DashStrength.Single;

    /// <summary>The pair a pair note belongs to; ignored by every other kind.</summary>
    public PairIndex PairIndex { get; set; } = PairIndex.One;

    /// <summary>A momentum note's push in playfield units per second; ignored by other kinds.</summary>
    public float Momentum { get; set; } = Note.DefaultMomentum;

    /// <summary>The left-side charge a contact barrier spends.</summary>
    public int ContactLeft { get; set; }

    /// <summary>The right-side charge a contact barrier spends.</summary>
    public int ContactRight { get; set; }

    /// <summary>The Splitline side a channel/rail note belongs to.</summary>
    public ChannelSide ChannelSide { get; set; } = ChannelSide.Left;

    /// <summary>How long a rail hold lasts; zero for a tap.</summary>
    public long HoldMs { get; set; }

    /// <summary>The Form construction shape a construction/step note belongs to.</summary>
    public int ShapeId { get; set; }

    /// <summary>Which attached block a construction step reveals.</summary>
    public int ShapePiece { get; set; }
}
