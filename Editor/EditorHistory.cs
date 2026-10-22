using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Editor;

/// <summary>Snapshot-based undo/redo for note and movement-queue edits.</summary>
public sealed class EditorHistory
{
    private const int MaxEntries = 200;

    private readonly EntityStore _entities;
    private readonly Stack<SnapshotPair> _undo = new();
    private readonly Stack<SnapshotPair> _redo = new();

    public EditorHistory(EntityStore entities)
    {
        _entities = entities;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public EditorSnapshot Capture()
    {
        var notes = new List<NoteSnapshot>();
        var movementTimes = new List<long>();
        var mines = new List<MineSnapshot>();

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            notes.Add(new NoteSnapshot(
                note.TimeMs,
                note.Position,
                note.FadeInMs,
                note.FadeOutMs,
                note.Target));
        }

        notes.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            if (time != 0)
                return time;

            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        foreach (Entity entity in _entities.Query<MovementEvent>().ToEntityList())
            movementTimes.Add(entity.GetComponent<MovementEvent>().TimeMs);

        movementTimes.Sort();

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

        return new EditorSnapshot(notes, movementTimes, mines);
    }

    public void Record(EditorSnapshot before, EditorSnapshot after)
    {
        if (before.Notes.SequenceEqual(after.Notes) &&
            before.MovementTimes.SequenceEqual(after.MovementTimes) &&
            before.Mines.SequenceEqual(after.Mines))
        {
            return;
        }

        _undo.Push(new SnapshotPair(before, after));
        _redo.Clear();

        if (_undo.Count > MaxEntries)
        {
            SnapshotPair[] keep = _undo.Take(MaxEntries).Reverse().ToArray();
            _undo.Clear();
            foreach (SnapshotPair pair in keep)
                _undo.Push(pair);
        }
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
            return false;

        SnapshotPair pair = _undo.Pop();
        Apply(pair.Before);
        _redo.Push(pair);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
            return false;

        SnapshotPair pair = _redo.Pop();
        Apply(pair.After);
        _undo.Push(pair);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void Apply(EditorSnapshot snapshot)
    {
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<MovementEvent>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        foreach (NoteSnapshot note in snapshot.Notes)
        {
            _entities.CreateEntity(new Note(
                note.TimeMs,
                note.Position,
                note.FadeInMs,
                note.FadeOutMs,
                note.Target));
        }

        foreach (long timeMs in snapshot.MovementTimes)
            _entities.CreateEntity(new MovementEvent(timeMs));

        foreach (MineSnapshot mine in snapshot.Mines)
        {
            _entities.CreateEntity(new Mine(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs));
        }
    }

    private readonly record struct SnapshotPair(
        EditorSnapshot Before,
        EditorSnapshot After);
}

/// <summary>Stable editable-state snapshot used by undo/redo.</summary>
public readonly record struct EditorSnapshot(
    List<NoteSnapshot> Notes,
    List<long> MovementTimes,
    List<MineSnapshot> Mines);

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
    long FadeInMs,
    long FadeOutMs,
    KeybindAction Target);
