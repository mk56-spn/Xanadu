using Godot;

namespace Xanadu.Systems.Input;

/// <summary>Toggles a debug overlay with the key it is given; F3 by default.</summary>
public sealed class GodotOverlayToggleSource(Key key = DebugKeys.Ecs) : IOverlayToggleSource
{
    public bool IsTogglePressed() => Godot.Input.IsKeyPressed(key);
}
