using Writer.Ecs;
using Writer.Settings;
using System.Collections.Generic;

namespace Writer.Levels;

/// <summary>Serializable level document.</summary>
public sealed class LevelDocument
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
}

/// <summary>Serializable practice start state.</summary>
public sealed class StartPointDocument
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
public sealed class MineDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public long FadeInMs { get; set; } = 600;

    public long FadeOutMs { get; set; } = 600;
}

/// <summary>Serializable note data.</summary>
public sealed class NoteDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public long FadeInMs { get; set; } = 600;

    public long FadeOutMs { get; set; } = 600;

    public KeybindAction Target { get; set; } = KeybindAction.M;

    public NoteKind Kind { get; set; } = NoteKind.Hit;

    public DashDirection DashDirection { get; set; } = DashDirection.Up;
}
