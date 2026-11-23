
namespace Xanadu.Ui.Systems.Input;

/// <summary>Frame-sampled keyboard state, abstracted for tests.</summary>
public interface IKeyboardState
{
    bool IsPressed(Key key);

    /// <summary>
    /// When <paramref name="key"/> was last pressed or let go, as a
    /// <see cref="System.Diagnostics.Stopwatch"/> timestamp; null when the source
    /// only knows the frame. State is read once a frame, but a press is timed by
    /// this, so it counts when it happened.
    /// </summary>
    long? ChangedAt(Key key) => null;
}
