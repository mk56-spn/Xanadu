using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.History;
using Xanadu.Settings;

namespace Xanadu.Editor;

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
        var blocks = new List<BlockSnapshot>();

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
                note.Character,
                MaskOf(entity)));
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
                mine.FadeOutMs,
                MaskOf(entity)));
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
                start.Direction,
                MaskOf(entity)));
        }

        starts.Sort(static (a, b) =>
        {
            int time = a.TimeMs.CompareTo(b.TimeMs);
            if (time != 0)
                return time;
            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            blocks.Add(new BlockSnapshot(block.Position, block.Size, MaskOf(entity)));
        }

        blocks.Sort(static (a, b) =>
        {
            int x = a.Position.X.CompareTo(b.Position.X);
            return x != 0 ? x : a.Position.Y.CompareTo(b.Position.Y);
        });

        return new EditorSnapshot(notes, mines, starts, blocks, CaptureGroups());
    }

    private static uint MaskOf(Entity entity) =>
        entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u;

    private GroupDefinition[]? CaptureGroups()
    {
        foreach (Entity entity in _entities.Query<LevelGroups>().ToEntityList())
            return entity.GetComponent<LevelGroups>().Clone().Groups;
        return null;
    }

    private static void Subscribe(Entity item, uint mask)
    {
        if (mask != 0)
            item.AddComponent(new ItemGroups(mask));
    }

    public void Record(EditorSnapshot before, EditorSnapshot after) =>
        _history.Record(before, after);

    public bool Undo() => _history.Undo();

    public bool Redo() => _history.Redo();

    public void Clear() => _history.Clear();

    private static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Notes.SequenceEqual(after.Notes) &&
        before.Mines.SequenceEqual(after.Mines) &&
        before.StartPoints.SequenceEqual(after.StartPoints) &&
        before.Blocks.SequenceEqual(after.Blocks) &&
        SameGroups(before.Groups, after.Groups);

    private static bool SameGroups(GroupDefinition[]? a, GroupDefinition[]? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return new LevelGroups { Groups = a }.SameAs(new LevelGroups { Groups = b });
    }

    private void Apply(EditorSnapshot snapshot)
    {
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
            entity.DeleteEntity();

        if (snapshot.Groups is not null)
        {
            foreach (Entity entity in _entities.Query<LevelGroups>().ToEntityList())
            {
                entity.GetComponent<LevelGroups>() = new LevelGroups { Groups = snapshot.Groups }.Clone();
                break;
            }
        }

        foreach (NoteSnapshot note in snapshot.Notes)
        {
            Entity created = EditorItems.Create(_entities, new Note(
                note.TimeMs,
                note.Position,
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character));
            Subscribe(created, note.Groups);
        }

        foreach (MineSnapshot mine in snapshot.Mines)
        {
            Entity created = EditorItems.Create(_entities, new Mine(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs));
            Subscribe(created, mine.Groups);
        }

        foreach (StartPointSnapshot start in snapshot.StartPoints)
        {
            Entity created = EditorItems.Create(_entities, new StartPoint(
                start.Name,
                start.TimeMs,
                start.Position,
                start.Momentum,
                start.Direction,
                start.Groups));
            Subscribe(created, start.Groups);
        }

        foreach (BlockSnapshot block in snapshot.Blocks)
        {
            Entity created = EditorItems.Create(_entities, new Block(block.Position, block.Size));
            Subscribe(created, block.Groups);
        }
    }
}

/// <summary>Stable editable-state snapshot used by undo/redo.</summary>
public readonly record struct EditorSnapshot(
    List<NoteSnapshot> Notes,
    List<MineSnapshot> Mines,
    List<StartPointSnapshot> StartPoints,
    List<BlockSnapshot> Blocks,
    GroupDefinition[]? Groups = null);

/// <summary>Stable serializable start-point snapshot used by undo/redo.</summary>
public readonly record struct StartPointSnapshot(
    string Name,
    long TimeMs,
    Vector2 Position,
    System.Numerics.Vector2 Momentum,
    Vector2 Direction,
    uint Groups = 0);

/// <summary>Stable serializable block snapshot used by undo/redo.</summary>
public readonly record struct BlockSnapshot(
    Vector2 Position,
    Vector2 Size,
    uint Groups = 0);

/// <summary>Stable serializable mine snapshot used by undo/redo.</summary>
public readonly record struct MineSnapshot(
    long TimeMs,
    Vector2 Position,
    long FadeInMs,
    long FadeOutMs,
    uint Groups = 0);

/// <summary>Stable serializable note snapshot used by undo/redo.</summary>
public readonly record struct NoteSnapshot(
    long TimeMs,
    Vector2 Position,
    KeybindAction Target,
    NoteKind Kind,
    DashDirection DashDirection,
    int ContactRequired,
    CharacterKind Character,
    uint Groups = 0);
