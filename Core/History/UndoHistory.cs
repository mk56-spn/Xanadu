using System;
using System.Collections.Generic;

namespace Xanadu.History;

/// <summary>
/// Snapshot-based undo/redo shared by every editor. Callers capture a state
/// before and after an edit and <see cref="Record"/> the pair; undo and redo
/// hand the stored snapshot back through the <c>apply</c> callback. Snapshots
/// must be independent copies, because the history keeps them.
/// </summary>
public sealed class UndoHistory<TSnapshot>
{
    private readonly List<Step> _undo = new();
    private readonly List<Step> _redo = new();
    private readonly Func<TSnapshot, TSnapshot, bool> _areEqual;
    private readonly Action<TSnapshot> _apply;
    private readonly int _maxEntries;

    public UndoHistory(
        Func<TSnapshot, TSnapshot, bool> areEqual,
        Action<TSnapshot> apply,
        int maxEntries = 200)
    {
        _areEqual = areEqual;
        _apply = apply;
        _maxEntries = Math.Max(1, maxEntries);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    /// <summary>Stores an edit. Edits that changed nothing are ignored.</summary>
    public void Record(TSnapshot before, TSnapshot after)
    {
        if (_areEqual(before, after))
            return;

        _undo.Add(new Step(before, after));
        _redo.Clear();
        if (_undo.Count > _maxEntries)
            _undo.RemoveAt(0);
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
            return false;

        Step step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _apply(step.Before);
        _redo.Add(step);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
            return false;

        Step step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _apply(step.After);
        _undo.Add(step);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private readonly record struct Step(TSnapshot Before, TSnapshot After);
}
