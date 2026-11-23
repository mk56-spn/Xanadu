using Xanadu.Ui.Systems.Input;

namespace Xanadu.Tools.Animator;

// Passes the mouse through untouched, standing in for it only in the perf probe.
#pragma warning disable RS0030

/// <summary>
/// The animator's pointer, which a set-up flag can take over: while
/// <see cref="Override"/> is set, the animator's systems read it instead of
/// the mouse (the generic UI still reads the mouse). Used by <c>--perf-drag</c>
/// to hold a joint and drag it round without a hand on the mouse.
/// </summary>
public sealed class AnimatorScriptedPointer(IPointerInputSource real) : IPointerInputSource
{
    public PointerInput? Override { get; set; }

    public PointerInput Read() => Override ?? real.Read();
}
