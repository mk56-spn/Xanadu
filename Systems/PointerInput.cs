using Godot;

namespace Writer.Systems;

/// <summary>One pointer sample for an input system update.</summary>
public readonly record struct PointerInput(Vector2 Position, bool Pressed);
