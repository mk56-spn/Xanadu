using System.Collections.Generic;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.Support;

/// <summary>Deterministic keyboard state for input-system tests.</summary>
public sealed class FakeKeyboard : IKeyboardState, IKeyPressSource
{
    private readonly HashSet<Key> _down = new();
    private readonly Queue<KeyChord> _presses = new();

    public bool IsPressed(Key key) => _down.Contains(key);

    /// <summary>Holds a key; going down also queues one press with the modifiers held so far.</summary>
    public void Press(Key key)
    {
        if (_down.Add(key) && key is not (Key.Ctrl or Key.Shift))
            _presses.Enqueue(new KeyChord(key, _down.Contains(Key.Ctrl), _down.Contains(Key.Shift)));
    }

    public bool TryTake(out KeyChord chord) => _presses.TryDequeue(out chord);

    public void Release(Key key) => _down.Remove(key);
    public void Clear() => _down.Clear();
}
