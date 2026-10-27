using Godot;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator;

/// <summary>Mouse-wheel input for the timeline, consumed once per frame.</summary>
public interface IAnimatorWheel
{
    /// <summary>Wheel notches since the last call; positive is up. Resets to zero.</summary>
    int TakeWheel();

    /// <summary>Whether Ctrl was held for the latest wheel input.</summary>
    bool Control { get; }
}

/// <summary>
/// Pointer source fed from Godot input events. A press is held for one extra
/// frame so a click shorter than a frame is still seen by every system, which
/// may each read the pointer.
/// </summary>
public sealed class AnimatorPointer : IPointerInputSource, IAnimatorWheel
{
    public Vector2 Position;
    public bool Pressed;
    public bool ClickPending;
    public bool Secondary;
    public bool SecondaryPending;
    public bool Middle;
    public bool Shift;
    public int Wheel;
    public bool Control { get; set; }

    public PointerInput Read() =>
        new(Position, Pressed || ClickPending, Shift, Secondary || SecondaryPending, Middle);

    public int TakeWheel()
    {
        int wheel = Wheel;
        Wheel = 0;
        return wheel;
    }

    public void EndFrame()
    {
        ClickPending = false;
        SecondaryPending = false;
    }
}
