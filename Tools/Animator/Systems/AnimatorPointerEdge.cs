using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Turns a held pointer into one press edge, the way every animator modal reads it.</summary>
internal static class AnimatorPointerEdge
{
    public static bool Primary(ref bool held, PointerInput input)
    {
        bool primary = input.Pressed && !held;
        held = input.Pressed;
        return primary;
    }
}
