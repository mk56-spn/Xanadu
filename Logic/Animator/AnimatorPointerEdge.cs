using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Animator;

/// <summary>Turns a held pointer into one press edge, the way every animator modal reads it.</summary>
public static class AnimatorPointerEdge
{
    public static bool Primary(ref bool held, PointerInput input)
    {
        bool primary = input.Pressed && !held;
        held = input.Pressed;
        return primary;
    }

    public static bool Secondary(ref bool held, PointerInput input)
    {
        bool secondary = input.SecondaryPressed && !held;
        held = input.SecondaryPressed;
        return secondary;
    }
}
