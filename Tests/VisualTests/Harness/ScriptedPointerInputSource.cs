using Godot;
using Writer.Systems.Input;

namespace Writer.Tests.VisualTests;

/// <summary>Deterministic pointer source used by scene tests.</summary>
public sealed class ScriptedPointerInputSource : IBlockingPointerInputSource
{
    public Vector2 Position;

    public bool Pressed;

    public bool Blocked { get; private set; }

    public void SetBlocked(bool blocked) => Blocked = blocked;

    public void Click(Vector2 position)
    {
        Position = position;
        Pressed = true;
    }

    public void Release() => Pressed = false;

    public PointerInput Read() => new(Position, Pressed && !Blocked);
}
