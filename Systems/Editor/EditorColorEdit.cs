using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// One sidebar colour field driving the selected items it accepts. The targets
/// are taken when editing begins, so a typed colour goes to what was selected
/// then, since clicking another item is what ends typing. A dragged picker
/// colours them as it moves, and the whole drag is one undo step. The brush,
/// when there is one, always takes the colour too, so the next item matches.
/// </summary>
internal sealed class EditorColorEdit(
    EditorHistory history,
    Func<Entity, bool> accepts,
    Action<Entity, Color> put,
    Action<Entity, Color>? brush = null)
{
    private readonly List<Entity> _targets = new();
    private EditorSnapshot? _before;
    private bool _wasEditing;

    /// <summary>Runs one frame of the field; true while it is being edited.</summary>
    public bool Update(Entity editor, Entity field, NoteSelection selection)
    {
        bool editing = UiColorField.IsEditing(field);
        if (editing && !_wasEditing)
        {
            EditorSelectionScan.Collect(selection, _targets, accepts);
            _before = history.Capture();
        }
        _wasEditing = editing;

        if (UiColorField.TakeLive(field, out Color live))
            Put(editor, live);
        if (UiColorField.TakeCommit(field, out Color color))
            Apply(editor, color);
        if (!editing)
            _before = null;

        return editing;
    }

    /// <summary>Records against the state from before the edit began, so colours shown live are part of the step.</summary>
    private void Apply(Entity editor, Color color)
    {
        _targets.RemoveAll(item => !accepts(item));
        if (_targets.Count == 0)
        {
            Put(editor, color);
            return;
        }

        EditorSnapshot before = _before ?? history.Capture();
        Put(editor, color);
        history.Record(before, history.Capture());
    }

    private void Put(Entity editor, Color color)
    {
        brush?.Invoke(editor, color);
        _targets.RemoveAll(item => !accepts(item));
        foreach (Entity item in _targets)
            put(item, color);
    }
}
