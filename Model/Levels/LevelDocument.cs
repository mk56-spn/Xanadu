using System;
using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Levels;

/// <summary>
/// A level as saved: which song it charts, its notes and its chords. Everything else the
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

    public List<ChordDocument> Chords { get; set; } = new();

    public List<PadDocument> Pads { get; set; } = new();

    public List<DraftDocument> Drafts { get; set; } = new();

    /// <summary>Dash enders: a dash entering one ends there.</summary>
    public List<DashStopDocument> DashStops { get; set; } = new();

    /// <summary>Placed Xifs that only look: grass, flares.</summary>
    public List<DecoDocument> Decos { get; set; } = new();

    /// <summary>The level's groups: sets of items, by id, and the keys over song beats that move and fade them.</summary>
    public List<GroupDocument> Groups { get; set; } = new();
}

/// <summary>A deco as saved: its origin and its own Xif tree.</summary>
public sealed class DecoDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public Xif Xif { get; set; } = new();

    public Deco ToDeco() => new(new Vector2(X, Y), XifCopy.Clone(Xif));

    public static DecoDocument From(in Deco deco) => new() { X = deco.Position.X, Y = deco.Position.Y, Xif = XifCopy.Clone(deco.Xif) };
}

/// <summary>A chord as saved: when it starts, and where its first line meets the judgement line.</summary>
public sealed class ChordDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public Chord ToChord() => new(TimeMs, new Vector2(X, Y));

    public static ChordDocument From(in Chord chord) => new()
    {
        TimeMs = chord.TimeMs,
        X = chord.Anchor.X,
        Y = chord.Anchor.Y,
    };
}

/// <summary>A pad as saved: its centre and the upward speed it launches at.</summary>
public sealed class PadDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Speed { get; set; }

    /// <summary>A timed pad's time, ms; -1 fires on touch.</summary>
    public long TimeMs { get; set; } = -1;

    public Pad ToPad() => new(new Vector2(X, Y), Speed, TimeMs);

    public static PadDocument From(in Pad pad) => new()
    {
        X = pad.Position.X,
        Y = pad.Position.Y,
        Speed = pad.Speed,
        TimeMs = pad.TimeMs,
    };
}

/// <summary>An updraft as saved: its emitter's centre and its hover height.</summary>
public sealed class DraftDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Height { get; set; }

    public Draft ToDraft() => new(new Vector2(X, Y), Height);

    public static DraftDocument From(in Draft draft) => new()
    {
        X = draft.Position.X,
        Y = draft.Position.Y,
        Height = draft.Height,
    };
}

/// <summary>A dash ender as saved: its centre.</summary>
public sealed class DashStopDocument
{
    public float X { get; set; }

    public float Y { get; set; }

    public DashStop ToDashStop() => new(new Vector2(X, Y));

    public static DashStopDocument From(in DashStop stop) => new() { X = stop.Position.X, Y = stop.Position.Y };
}

/// <summary>A note as saved: an action note at its time and place.</summary>
public sealed class NoteDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The action key the note asks for.</summary>
    public KeybindAction Target { get; set; } = KeybindAction.Action1;

    public NoteKind Kind { get; set; } = NoteKind.Tap;

    /// <summary>An orb's tail, ms.</summary>
    public long EndMs { get; set; }

    /// <summary>An orb's height path (<see cref="Note.Path"/>).</summary>
    public OrbKey[]? Path { get; set; }

    /// <summary>An entrance's onset.</summary>
    public EntranceExit Exit { get; set; }

    /// <summary>Whether this note carries an orb's tail and path: an orb, or an entrance that comes out on one.</summary>
    public bool HasOrb => Kind == NoteKind.Orb || (Kind == NoteKind.Entrance && Exit == EntranceExit.Orb);

    /// <summary>The note component this document makes.</summary>
    public Note ToNote() => new(TimeMs, new Vector2(X, Y), Target, Kind)
    {
        EndMs = HasOrb ? Math.Max(EndMs, TimeMs) : TimeMs,
        Path = Path,
        Exit = Exit,
    };

    /// <summary>The document a note component saves as.</summary>
    public static NoteDocument From(in Note note) => new()
    {
        TimeMs = note.TimeMs,
        X = note.Position.X,
        Y = note.Position.Y,
        Target = note.Target,
        Kind = note.Kind,
        EndMs = note.EndMs,
        Path = note.Path,
        Exit = note.Exit,
    };
}
