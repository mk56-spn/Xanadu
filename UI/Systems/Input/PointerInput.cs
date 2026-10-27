using Godot;

namespace Xanadu.Systems.Input;

/// <summary>One pointer sample for an input system update.</summary>
public readonly record struct PointerInput(
    Vector2 Position,
    bool Pressed,
    bool Shift = false,
    bool SecondaryPressed = false,
    bool Middle = false);
