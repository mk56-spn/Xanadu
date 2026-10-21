using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Editor;

/// <summary>Snapshot-based undo/redo for note edits only.</summary>
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

    public List<NoteSnapshot> Capture()
    {
        var notes = new List<NoteSnapshot>();

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

        return notes;
    }

    public void Record(List<NoteSnapshot> before, List<NoteSnapshot> after)
    {
        if (before.SequenceEqual(after))
            return;

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

    private void Apply(List<NoteSnapshot> notes)
    {
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (NoteSnapshot note in notes)
        {
            _entities.CreateEntity(new Note(
                note.TimeMs,
                note.Position,
                note.FadeInMs,
                note.FadeOutMs,
                note.Target));
        }
    }

    private readonly record struct SnapshotPair(
        List<NoteSnapshot> Before,
        List<NoteSnapshot> After);
}

/// <summary>Stable serializable note snapshot used by undo/redo.</summary>
public readonly record struct NoteSnapshot(
    long TimeMs,
    Vector2 Position,
    long FadeInMs,
    long FadeOutMs,
    KeybindAction Target);
