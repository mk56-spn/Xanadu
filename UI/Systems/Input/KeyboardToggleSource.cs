using Godot;

namespace Xanadu.Systems.Input;

/// <summary>Adapts a key on an <see cref="IKeyboardState"/> to an overlay toggle.</summary>
public sealed class KeyboardToggleSource(IKeyboardState keyboard, Key key) : IOverlayToggleSource
{
    public bool IsTogglePressed() => keyboard.IsPressed(key);
}
