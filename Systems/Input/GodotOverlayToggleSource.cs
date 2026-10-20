using Godot;

namespace Writer.Systems.Input;

/// <summary>Toggles the debug overlay with F3.</summary>
public sealed class GodotOverlayToggleSource : IOverlayToggleSource
{
    public bool IsTogglePressed() => Godot.Input.IsKeyPressed(Key.F3);
}
