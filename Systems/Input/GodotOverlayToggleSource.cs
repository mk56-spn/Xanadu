using Godot;

namespace Writer.Systems.Input;

/// <summary>Toggles the debug overlay with F3.</summary>
public sealed class GodotOverlayToggleSource(Key key = Key.F3) : IOverlayToggleSource
{
    public bool IsTogglePressed() => Godot.Input.IsKeyPressed(key);
}
