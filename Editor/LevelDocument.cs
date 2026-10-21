using System.Collections.Generic;

namespace Writer.Editor;

/// <summary>Serializable level document.</summary>
public sealed class LevelDocument
{
    public string Name { get; set; } = "Untitled";

    public string AudioPath { get; set; } = "";

    public int Bpm { get; set; } = 120;

    public long OffsetMs { get; set; }

    public int SnapDivision { get; set; } = 4;

    public int PlayfieldWidth { get; set; } = 1000;

    public int PlayfieldHeight { get; set; } = 1000;

    public List<NoteDocument> Notes { get; set; } = new();
}

/// <summary>Serializable note data.</summary>
public sealed class NoteDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public long FadeInMs { get; set; } = 600;

    public long FadeOutMs { get; set; } = 600;
}
