using Friflo.Engine.ECS;
using Xanadu.Core.History;
using Xanadu.Editor.History;

namespace Xanadu.Editor;

/// <summary>
///     Undo/redo for the level's editable items, on the shared
///     <see cref="UndoHistory{T}" />. Capturing, comparing and restoring snapshots
///     each live in their own class; this only keeps the history the editor asks for.
/// </summary>
public sealed class EditorHistory
{
    private readonly EditorSnapshotCapture _capture;
    private readonly UndoHistory<EditorSnapshot> _history;

    public EditorHistory(EntityStore entities)
    {
        _capture = new EditorSnapshotCapture(entities);
        var restore = new EditorSnapshotRestore(entities);
        _history = new UndoHistory<EditorSnapshot>(EditorSnapshotComparison.AreEqual, restore.Apply);
    }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public EditorSnapshot Capture() => _capture.Capture();

    /// <summary>Stores an edit; always true, kept for the callers that check it.</summary>
    public bool Record(EditorSnapshot before, EditorSnapshot after)
    {
        _history.Record(before, after);
        return true;
    }

    public bool Undo() => _history.Undo();

    public bool Redo() => _history.Redo();

    public void Clear() => _history.Clear();
}
