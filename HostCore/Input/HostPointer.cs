using MoonWorks.Input;
using Xanadu.Systems.Input;

namespace Xanadu.Host.Input;

/// <summary>
/// The mouse in the game's 1920×1080 design space, with the same latching as
/// Godot's pointer so a click between two frames is not lost.
/// </summary>
internal sealed class HostPointer(Mouse mouse, IKeyboardState keyboard) : IBlockingPointerInputSource, IWheelSource
{
    private readonly PointerLatch _latch = new();

    /// <summary>Window pixels to design pixels; the host sets it when the window size is known.</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    public bool Blocked => _latch.Blocked;

    /// <summary>Where the pointer is in design pixels, without touching the press latch.</summary>
    public Vector2 Position => new(mouse.X * Scale.X, mouse.Y * Scale.Y);

    public void SetBlocked(bool blocked) => _latch.Blocked = blocked;

    public int Notches => _latch.Notches;

    public bool Control => _latch.Control;

    public bool Shift => _latch.Shift;

    public int TakeWheel() => _latch.TakeWheel();

    /// <summary>Latches this frame's presses and wheel; call once per update, before the systems.</summary>
    public void Poll()
    {
        if (mouse.LeftButton.IsPressed)
            _latch.LatchPrimary();
        if (mouse.RightButton.IsPressed)
            _latch.LatchSecondary();
        if (mouse.MiddleButton.IsPressed)
            _latch.LatchMiddle();
        if (mouse.Wheel != 0)
            _latch.AddWheel(
                mouse.Wheel,
                keyboard.IsPressed(Key.Ctrl) || keyboard.IsPressed(Key.Meta),
                keyboard.IsPressed(Key.Shift));
    }

    public void EndFrame() => _latch.EndFrame();

    /// <summary>Whether the left button is down right now, without touching the press latch.</summary>
    public bool LeftDown => mouse.LeftButton.IsDown;

    public PointerInput Read() => _latch.Sample(
        new Vector2(mouse.X * Scale.X, mouse.Y * Scale.Y),
        mouse.LeftButton.IsDown,
        mouse.RightButton.IsDown,
        mouse.MiddleButton.IsDown,
        keyboard.IsPressed(Key.Shift),
        keyboard.IsPressed(Key.Ctrl) || keyboard.IsPressed(Key.Meta));
}
