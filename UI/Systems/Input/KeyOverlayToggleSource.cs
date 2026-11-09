using Godot;
using Xanadu.Systems;

namespace Xanadu.Systems.Input;

/// <summary>
/// Toggles a debug overlay with the key it is given; F3 by default. It reads
/// <paramref name="keyboard"/>, or the host's when none is given.
/// </summary>
public sealed class KeyOverlayToggleSource(Key key = DebugKeys.Ecs, IKeyboardState? keyboard = null) : IOverlayToggleSource
{
    public bool IsTogglePressed() => (keyboard ?? HostPlatform.Keyboard).IsPressed(key);
}
