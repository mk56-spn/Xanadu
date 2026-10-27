using System.Collections.Generic;
using Godot;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.Support;

/// <summary>Deterministic keyboard state for input-system tests.</summary>
public sealed class FakeKeyboard : IKeyboardState
{
    private readonly HashSet<Key> _down = new();

    public bool IsPressed(Key key) => _down.Contains(key);
    public void Press(Key key) => _down.Add(key);
    public void Release(Key key) => _down.Remove(key);
    public void Clear() => _down.Clear();
}
