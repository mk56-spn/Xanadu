using System;
using Godot;
using Writer.Ecs;
using Writer.Systems.Editor;
using Writer.Editor.Commands;

namespace Writer.Editor;

/// <summary>Editor overlay keyboard shortcuts and transport actions.</summary>
public partial class EditorOverlay : CanvasLayer
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_closed)
            return;

        if (@event is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
            } wheel)
        {
            if (_editor.IsNull ||
                AnyDialogVisible() ||
                IsWarningOpen() ||
                GetViewport().GuiGetFocusOwner() is LineEdit)
            {
                return;
            }

            ref Playhead playhead = ref _editor.GetComponent<Playhead>();
            int direction = wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
            _commands.Enqueue(EditorCommand.Seek(
                EditorScroll.Target(
                    playhead,
                    direction,
                    wheel.ShiftPressed)));
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (AnyDialogVisible())
            return;

        if (key.Keycode == Key.Escape && IsPreviewing())
        {
            EndPreview();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.S)
        {
            SaveCurrent();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.Z)
        {
            Undo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.Y)
        {
            Redo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            return;

        if (IsWarningOpen())
            return;

        if (key.Keycode == Key.Escape)
        {
            _onBack?.Invoke();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            _onForward?.Invoke();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.ShiftPressed &&
            key.Keycode is Key.Left or Key.Right)
        {
            ref Playhead playhead = ref _editor.GetComponent<Playhead>();
            long delta = key.Keycode == Key.Left ? -1_000 : 1_000;
            _commands.Enqueue(EditorCommand.Seek(
                Math.Clamp(
                    playhead.TimeMs + delta,
                    0,
                    Math.Max(0, playhead.DurationMs))));
            GetViewport().SetInputAsHandled();
            return;
        }

        switch (key.Keycode)
        {
            case Key.T:
                TapTempo();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Space:
                TogglePlayback();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Home:
                StopPlayback();
                GetViewport().SetInputAsHandled();
                break;
            case Key.L:
                OpenSongDialog();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Delete:
            case Key.Backspace:
                DeleteSelectedNote();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Left:
                _commands.Enqueue(EditorCommand.MoveSelection(-1));
                GetViewport().SetInputAsHandled();
                break;
            case Key.Right:
                _commands.Enqueue(EditorCommand.MoveSelection(1));
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void TogglePlayback()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.TogglePlayback());
    }

    private void StopPlayback()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.StopPlayback());
    }

    private void DeleteSelectedNote()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.DeleteSelection());
    }

    private void Undo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Undo());
        SetStatus("Undo");
    }

    private void Redo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Redo());
        SetStatus("Redo");
    }

    private void OnZoom(int direction)
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.ZoomTimeline(direction));
    }

    private void OnAddMovement()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.AddMovementEvent());
    }
}
