using Writer.Settings;
using System.Collections.Generic;

namespace Writer.Levels;

/// <summary>Serializable level document.</summary>
public sealed class LevelDocument
{
    public int Version { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public string AudioPath { get; set; } = "";

    public string Artist { get; set; } = "";

    public string Difficulty { get; set; } = "";

    public int Bpm { get; set; } = 120;

    public long OffsetMs { get; set; }

    public int SnapDivision { get; set; } = 4;

    public int PlayfieldWidth { get; set; } = 1000;

    public int PlayfieldHeight { get; set; } = 1000;

    public List<NoteDocument> Notes { get; set; } = new();

    public List<MovementEventDocument> MovementEvents { get; set; } = new();
}

/// <summary>Serializable movement-gate event.</summary>
public sealed class MovementEventDocument
{
    public long TimeMs { get; set; }
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
}
