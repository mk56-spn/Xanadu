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

/// <summary>
/// A note as saved: an action note at its head, and for a route note the
/// turn times and speed of the route it sends the body down once the head is hit;
/// a dash note's length, or a hold note's tail.
/// </summary>
public sealed class NoteDocument
{
    public long TimeMs { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The action key the head asks for.</summary>
    public KeybindAction Target { get; set; } = KeybindAction.Action1;

    /// <summary>A route note's turn times, ms after the head, the last its end; null on a plain note.</summary>
    public List<long>? Route { get; set; }

    /// <summary>Which of the three fixed speeds a route note runs at.</summary>
    public DashStrength RouteStrength { get; set; }

    /// <summary>A dash note's teleport distance, playfield units; 0 on every other note.</summary>
    public float DashLength { get; set; }

    /// <summary>A hold note's tail time, ms after the head; 0 on every other note.</summary>
    public long HoldTailMs { get; set; }

    /// <summary>Where a hold note's tail sits from the head, playfield units.</summary>
    public float HoldX { get; set; }

    public float HoldY { get; set; }

    /// <summary>The hold as the note component carries it; null without one.</summary>
    public NoteHold? BuildHold() =>
        HoldTailMs > 0 ? new NoteHold(HoldTailMs, new Vector2(HoldX, HoldY)) : null;

    /// <summary>The route as the note component carries it; null without one.</summary>
    public NoteRoute? BuildRoute() =>
        Route is { Count: >= NoteRoute.MinTimes } times ? new NoteRoute(RouteStrength, times.ToArray()) : null;

    /// <summary>The note component this document makes.</summary>
    public Note ToNote() => new(TimeMs, new Vector2(X, Y), Target, BuildRoute(), DashLength, BuildHold());

    /// <summary>The document a note component saves as.</summary>
    public static NoteDocument From(in Note note) => new()
    {
        TimeMs = note.TimeMs,
        X = note.Position.X,
        Y = note.Position.Y,
        Target = note.Target,
        Route = note.Route is { } route ? new List<long>(route.TimesMs.ToArray()) : null,
        RouteStrength = note.Route?.Strength ?? DashStrength.Single,
        DashLength = note.DashLength,
        HoldTailMs = note.Hold?.TailMs ?? 0,
        HoldX = note.Hold?.Tail.X ?? 0f,
        HoldY = note.Hold?.Tail.Y ?? 0f,
    };
}
