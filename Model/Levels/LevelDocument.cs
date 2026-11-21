using System.Collections.Generic;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Levels;

/// <summary>
/// A level as saved: which song it charts and its notes. Everything else the
/// classic game had (items, groups, layers, shaders) is gone; each comes back
/// as its own field or list when it is rebuilt.
/// </summary>
public sealed class LevelDocument
{
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

    public List<NoteDocument> Notes { get; set; } = new();
}

/// <summary>A note as saved: an action note at its time and place.</summary>
public sealed class NoteDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The action key the note asks for.</summary>
    public KeybindAction Target { get; set; } = KeybindAction.Action1;

    /// <summary>The note component this document makes.</summary>
    public Note ToNote() => new(TimeMs, new Vector2(X, Y), Target);

    /// <summary>The document a note component saves as.</summary>
    public static NoteDocument From(in Note note) => new()
    {
        TimeMs = note.TimeMs,
        X = note.Position.X,
        Y = note.Position.Y,
        Target = note.Target,
    };
}
