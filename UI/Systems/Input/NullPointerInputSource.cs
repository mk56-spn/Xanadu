using Godot;

namespace Xanadu.Systems.Input;

/// <summary>Pointer source used by headless systems when no input exists.</summary>
public sealed class NullPointerInputSource : IPointerInputSource
{
    public PointerInput Read() => new(Vector2.Zero, false);
}
