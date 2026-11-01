using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Input;

/// <summary>One key press with the modifiers held for it; bindings match them exactly.</summary>
public readonly record struct KeyChord(Key Key, bool Ctrl = false, bool Shift = false);

/// <summary>The key presses waiting for a host's shortcut system, oldest first.</summary>
public interface IKeyPressSource
{
    bool TryTake(out KeyChord chord);
}

/// <summary>
/// Key presses fed from a host's <c>_UnhandledInput</c>, so a tap between two
/// frames is not lost and keys a focused text input uses never become shortcuts.
/// Key repeat is ignored.
/// </summary>
public sealed class KeyPressQueue : IKeyPressSource
{
    private readonly Queue<KeyChord> _presses = new();

    public void Feed(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        Press(new KeyChord(
            key.Keycode != Key.None ? key.Keycode : key.PhysicalKeycode,
            key.CtrlPressed,
            key.ShiftPressed));
    }

    public void Press(KeyChord chord) => _presses.Enqueue(chord);

    public bool TryTake(out KeyChord chord) => _presses.TryDequeue(out chord);
}
