using Godot;
using Xanadu.Systems;

namespace Xanadu.Systems.Input;

/// <summary>Toggles a debug overlay with the key it is given; F3 by default.</summary>
public sealed class KeyOverlayToggleSource(Key key = DebugKeys.Ecs) : IOverlayToggleSource
{
    public bool IsTogglePressed() => HostPlatform.Keyboard.IsPressed(key);
}
