using Godot;

namespace Xanadu.Systems.Input;

/// <summary>
/// The pointer and wheel for every host. The host forwards each event from its
/// <c>_Input</c> to <see cref="Feed"/> and calls <see cref="EndFrame"/> once its
/// systems have run. Position and held buttons come from Godot's live state.
/// </summary>
public sealed class GodotPointerInputSource(Control host) : IBlockingPointerInputSource, IWheelSource
{
    private readonly PointerLatch _latch = new();

    /// <summary>Blocks pointer input while a modal editor dialog is open.</summary>
    public bool Blocked => _latch.Blocked;

    public void SetBlocked(bool blocked) => _latch.Blocked = blocked;

    public int Notches => _latch.Notches;

    public bool Control => _latch.Control;

    public bool Shift => _latch.Shift;

    public int TakeWheel() => _latch.TakeWheel();

    public void EndFrame() => _latch.EndFrame();

    /// <summary>Records the presses and wheel notches a poll could miss between frames.</summary>
    public void Feed(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } button)
            return;

        switch (button.ButtonIndex)
        {
            case MouseButton.Left:
                _latch.LatchPrimary();
                break;
            case MouseButton.Right:
                _latch.LatchSecondary();
                break;
            case MouseButton.Middle:
                _latch.LatchMiddle();
                break;
            case MouseButton.WheelUp:
                _latch.AddWheel(1, button.CtrlPressed || button.MetaPressed, button.ShiftPressed);
                break;
            case MouseButton.WheelDown:
                _latch.AddWheel(-1, button.CtrlPressed || button.MetaPressed, button.ShiftPressed);
                break;
        }
    }

    public PointerInput Read() => _latch.Sample(
        host.GetLocalMousePosition(),
        Godot.Input.IsMouseButtonPressed(MouseButton.Left),
        Godot.Input.IsMouseButtonPressed(MouseButton.Right),
        Godot.Input.IsMouseButtonPressed(MouseButton.Middle),
        Godot.Input.IsKeyPressed(Key.Shift),
        Godot.Input.IsKeyPressed(Key.Ctrl) || Godot.Input.IsKeyPressed(Key.Meta));
}
