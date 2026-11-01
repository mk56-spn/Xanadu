using Godot;

namespace Xanadu.Systems.Input;

/// <summary>
/// One frame of pointer state. A press is kept until <see cref="EndFrame"/>, so a
/// click shorter than a frame is seen by every system that reads the pointer, not
/// only the first. Blocking hides every button and drops the wheel.
/// </summary>
public sealed class PointerLatch : IWheelSource
{
    private bool _primary;
    private bool _secondary;
    private bool _middle;

    public bool Blocked { get; set; }

    public int Notches { get; private set; }

    public bool Control { get; private set; }

    public bool Shift { get; private set; }

    public void LatchPrimary() => _primary = true;

    public void LatchSecondary() => _secondary = true;

    public void LatchMiddle() => _middle = true;

    /// <summary>Counts one wheel notch; positive is up.</summary>
    public void AddWheel(int notches, bool control, bool shift)
    {
        if (Blocked)
            return;

        Notches += notches;
        Control = control;
        Shift = shift;
    }

    public int TakeWheel()
    {
        int notches = Notches;
        Notches = 0;
        return notches;
    }

    /// <summary>Combines what is physically held with what was latched this frame.</summary>
    public PointerInput Sample(
        Vector2 position,
        bool primary,
        bool secondary,
        bool middle,
        bool shift) =>
        new(
            position,
            !Blocked && (primary || _primary),
            shift,
            !Blocked && (secondary || _secondary),
            !Blocked && (middle || _middle));

    public void EndFrame()
    {
        _primary = false;
        _secondary = false;
        _middle = false;
        Notches = 0;
    }
}
