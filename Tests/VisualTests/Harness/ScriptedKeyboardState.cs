using System.Collections.Generic;
using Godot;
using Writer.Systems.Input;

namespace Writer.Tests.VisualTests;

/// <summary>Frame-sampled keyboard state controlled by scene tests.</summary>
public sealed class ScriptedKeyboardState : IKeyboardState
{
    private readonly HashSet<Key> _down = new();

    public bool IsPressed(Key key) => _down.Contains(key);
    public void Press(Key key) => _down.Add(key);
    public void Release(Key key) => _down.Remove(key);
}
