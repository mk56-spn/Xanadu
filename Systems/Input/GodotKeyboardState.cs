using Godot;

namespace Writer.Systems.Input;

/// <summary>Reads the live Godot keyboard.</summary>
public sealed class GodotKeyboardState : IKeyboardState
{
    public bool IsPressed(Key key) => Godot.Input.IsKeyPressed(key);
}
