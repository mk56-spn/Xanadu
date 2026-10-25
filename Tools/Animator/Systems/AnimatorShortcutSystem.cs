using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>Keyboard shortcuts: Ctrl+Z undo, Ctrl+Y or Ctrl+Shift+Z redo, Ctrl+S save, Delete removes the selected key.</summary>
public sealed class AnimatorShortcutSystem(IKeyboardState keyboard)
    : QuerySystem<AnimatorDocument>
{
    private bool _undoHeld;
    private bool _redoHeld;
    private bool _deleteHeld;
    private bool _saveHeld;

    protected override void OnUpdate()
    {
        // Typing a clip name must not trigger shortcuts such as Delete.
        if (Query.Store.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).Count > 0)
        {
            _undoHeld = _redoHeld = _deleteHeld = _saveHeld = true;
            return;
        }

        bool control = keyboard.IsPressed(Key.Ctrl);
        bool shift = keyboard.IsPressed(Key.Shift);
        bool undo = control && !shift && keyboard.IsPressed(Key.Z);
        bool redo = control && (keyboard.IsPressed(Key.Y) ||
                                (shift && keyboard.IsPressed(Key.Z)));
        bool delete = keyboard.IsPressed(Key.Delete);
        bool save = control && keyboard.IsPressed(Key.S);

        foreach (Entity session in Query.ToEntityList())
        {
            if (undo && !_undoHeld)
                AnimatorHistoryCommands.Undo(session);
            if (redo && !_redoHeld)
                AnimatorHistoryCommands.Redo(session);
            if (save && !_saveHeld)
                AnimatorFileCommands.Save(session);
            if (delete && !_deleteHeld)
                AnimatorKeyCommands.DeleteSelected(session);
        }

        _undoHeld = undo;
        _redoHeld = redo;
        _deleteHeld = delete;
        _saveHeld = save;
    }
}
