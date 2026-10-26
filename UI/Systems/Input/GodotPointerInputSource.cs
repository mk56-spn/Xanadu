using Godot;

namespace Writer.Systems.Input;

/// <summary>Reads pointer state from the Godot Control that hosts the UI.</summary>
public sealed class GodotPointerInputSource(Control host) : IBlockingPointerInputSource
{
    private bool _latchedPress;

    /// <summary>Blocks pointer input while a modal editor dialog is open.</summary>
    public bool Blocked { get; set; }

    public void SetBlocked(bool blocked) => Blocked = blocked;

    /// <summary>Latches a click so a press/release between frames cannot be lost.</summary>
    public void LatchPress() => _latchedPress = true;

    public PointerInput Read()
    {
        Vector2 position = host.GetLocalMousePosition();
        bool physical = Godot.Input.IsMouseButtonPressed(MouseButton.Left);
        bool pressed = !Blocked && (physical || _latchedPress);

        _latchedPress = false;
        return new PointerInput(
            position,
            pressed,
            Godot.Input.IsKeyPressed(Key.Shift),
            Godot.Input.IsMouseButtonPressed(MouseButton.Right),
            Godot.Input.IsMouseButtonPressed(MouseButton.Middle));
    }
}
