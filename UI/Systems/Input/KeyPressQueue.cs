using System.Collections.Generic;

namespace Xanadu.Systems.Input;

/// <summary>One key press with the modifiers held for it; bindings match them exactly.</summary>
public readonly record struct KeyChord(Key Key, bool Ctrl = false, bool Shift = false);

/// <summary>The key presses waiting for a host's shortcut system, oldest first.</summary>
public interface IKeyPressSource
{
    bool TryTake(out KeyChord chord);
}

/// <summary>
/// Key presses the host feeds as they happen, so a tap between two frames is
/// not lost and keys a focused text input uses never become shortcuts. The
/// host leaves out key repeat.
/// </summary>
public sealed class KeyPressQueue : IKeyPressSource
{
    private readonly Queue<KeyChord> _presses = new();

    public void Press(KeyChord chord) => _presses.Enqueue(chord);

    public bool TryTake(out KeyChord chord) => _presses.TryDequeue(out chord);
}
