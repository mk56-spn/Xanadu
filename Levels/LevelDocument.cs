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

    public int PlayfieldWidth { get; set; } = 1000;

    public int PlayfieldHeight { get; set; } = 1000;

    public List<NoteDocument> Notes { get; set; } = new();

    public List<MineDocument> Mines { get; set; } = new();

    public List<StartPointDocument> StartPoints { get; set; } = new();

    public List<BlockDocument> Blocks { get; set; } = new();

    /// <summary>Spike tiles: permanent hazards that kill on contact.</summary>
    public List<SpikeDocument> Spikes { get; set; } = new();

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
    /// <summary>The groups this block belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }
}

/// <summary>Serializable spike tile data: a hazard that kills on contact.</summary>
public sealed class SpikeDocument
{
    /// <summary>The groups this spike belongs to.</summary>
    public List<int> Groups { get; set; } = new();

    /// <summary>The shader slot it is drawn with, or 0 for none.</summary>
    public int Shader { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }
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
}
