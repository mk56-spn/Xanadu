using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.History;

namespace Xanadu.Editor;

/// <summary>
/// Undo/redo for the level's editable items, on the shared
/// <see cref="UndoHistory{T}"/>. Capturing, comparing and restoring snapshots
/// each live in their own class; this only keeps the history the editor asks for.
/// <para>
/// It is also where the buffer lock (docs/render-architecture.md D16a) is
/// enforced for every edit: an edit that newly breaks a buffered group's rules
/// is put back and not recorded, and the rule is shown as a warning. The
/// commands still check first where they can, so they refuse without a
/// rebuild; this is the net that a new command can't forget.
/// </para>
/// </summary>
public sealed class EditorHistory
{
    private readonly EntityStore _entities;
    private readonly EditorSnapshotCapture _capture;
    private readonly EditorSnapshotRestore _restore;
    private readonly UndoHistory<EditorSnapshot> _history;

    /// <summary>The buffered groups already broken when a snapshot was captured, keyed by its note list (each capture makes a new one).</summary>
    private readonly ConditionalWeakTable<List<NoteSnapshot>, Dictionary<int, string>> _brokenAt = new();

    public EditorHistory(EntityStore entities)
    {
        _entities = entities;
        _capture = new EditorSnapshotCapture(entities);
        _restore = new EditorSnapshotRestore(entities);
        _history = new UndoHistory<EditorSnapshot>(
            EditorSnapshotComparison.AreEqual,
            _restore.Apply);
    }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public EditorSnapshot Capture()
    {
        EditorSnapshot snapshot = _capture.Capture();
        if (snapshot.Notes is not null)
            _brokenAt.AddOrUpdate(snapshot.Notes, Broken());
        return snapshot;
    }

    /// <summary>
    /// Stores an edit, unless it newly broke a buffered group: then the level
    /// goes back to <paramref name="before"/>, nothing is recorded, a warning
    /// says why, and the result is false.
    /// </summary>
    public bool Record(EditorSnapshot before, EditorSnapshot after)
    {
        if (before.Notes is not null && _brokenAt.TryGetValue(before.Notes, out Dictionary<int, string>? was))
        {
            foreach ((int group, string problem) in Broken())
            {
                if (was.ContainsKey(group))
                    continue;

                _restore.Apply(before);
                Warn(problem);
                return false;
            }
        }

        _history.Record(before, after);
        return true;
    }

    public bool Undo() => _history.Undo();

    public bool Redo() => _history.Redo();

    public void Clear() => _history.Clear();

    private Dictionary<int, string> Broken()
    {
        foreach (Entity level in _entities.Query<LevelGroups>().Entities)
        {
            GroupDefinition[]? groups = level.GetComponent<LevelGroups>().Groups;
            return groups is null ? new Dictionary<int, string>() : GroupBufferRules.BrokenGroups(_entities, groups);
        }

        return new Dictionary<int, string>();
    }

    private void Warn(string problem)
    {
        foreach (Entity editor in _entities.Query<NoteSelection>().ToEntityList())
        {
            if (editor.HasComponent<WarningRequest>())
                editor.GetComponent<WarningRequest>().Message = problem;
            else
                editor.AddComponent(new WarningRequest(problem));
            return;
        }
    }
}
