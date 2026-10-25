using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.History;
using Writer.Settings;

namespace Writer.Editor;

/// <summary>Undo/redo for note, mine and start-point edits, on the shared <see cref="UndoHistory{T}"/>.</summary>
public sealed class EditorHistory
{
    private readonly EntityStore _entities;
    private readonly UndoHistory<EditorSnapshot> _history;

    public EditorHistory(EntityStore entities)
    {
        _entities = entities;
        _history = new UndoHistory<EditorSnapshot>(AreEqual, Apply);
    }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public EditorSnapshot Capture()
    {
        var notes = new List<NoteSnapshot>();
        var mines = new List<MineSnapshot>();
        var starts = new List<StartPointSnapshot>();

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            notes.Add(new NoteSnapshot(
                note.TimeMs,
                note.Position,
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character));
        }

        notes.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            if (time != 0)
                return time;

            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            mines.Add(new MineSnapshot(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs));
        }

        mines.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            if (time != 0)
                return time;

            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            starts.Add(new StartPointSnapshot(
                start.Name,
                start.TimeMs,
                start.Position,
                start.Momentum,
                start.Direction));
        }

        starts.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            if (time != 0)
                return time;
            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        return new EditorSnapshot(notes, mines, starts);
    }

    public void Record(EditorSnapshot before, EditorSnapshot after) =>
        _history.Record(before, after);

    public bool Undo() => _history.Undo();

    public bool Redo() => _history.Redo();

    public void Clear() => _history.Clear();

    private static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Notes.SequenceEqual(after.Notes) &&
        before.Mines.SequenceEqual(after.Mines) &&
        before.StartPoints.SequenceEqual(after.StartPoints);

    private void Apply(EditorSnapshot snapshot)
    {
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
            entity.DeleteEntity();

        foreach (NoteSnapshot note in snapshot.Notes)
        {
            EditorItems.Create(_entities, new Note(
                note.TimeMs,
                note.Position,
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character));
        }

        foreach (MineSnapshot mine in snapshot.Mines)
        {
            EditorItems.Create(_entities, new Mine(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs));
        }

        foreach (StartPointSnapshot start in snapshot.StartPoints)
        {
            EditorItems.Create(_entities, new StartPoint(
                start.Name,
                start.TimeMs,
                start.Position,
                start.Momentum,
                start.Direction));
        }
    }
}

/// <summary>Stable editable-state snapshot used by undo/redo.</summary>
public readonly record struct EditorSnapshot(
    List<NoteSnapshot> Notes,
    List<MineSnapshot> Mines,
    List<StartPointSnapshot> StartPoints);

/// <summary>Stable serializable start-point snapshot used by undo/redo.</summary>
public readonly record struct StartPointSnapshot(
    string Name,
    long TimeMs,
    Vector2 Position,
    System.Numerics.Vector2 Momentum,
    Vector2 Direction);

/// <summary>Stable serializable mine snapshot used by undo/redo.</summary>
public readonly record struct MineSnapshot(
    long TimeMs,
    Vector2 Position,
    long FadeInMs,
    long FadeOutMs);

/// <summary>Stable serializable note snapshot used by undo/redo.</summary>
public readonly record struct NoteSnapshot(
    long TimeMs,
    Vector2 Position,
    KeybindAction Target,
    NoteKind Kind,
    DashDirection DashDirection,
    int ContactRequired,
    CharacterKind Character);
