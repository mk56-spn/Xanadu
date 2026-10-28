namespace Xanadu.Systems.Input;

/// <summary>
/// Show/hide state shared by every debug overlay and panel: one press of the
/// toggle flips it, holding the key does not, and it can be forced closed.
/// </summary>
public sealed class OverlayLatch(IOverlayToggleSource source)
{
    private bool _wasPressed;

    public bool Visible { get; private set; }

    /// <summary>
    /// Reads the toggle. While not <paramref name="allowed"/> the overlay is
    /// closed and presses are ignored. Returns true when visibility changed.
    /// </summary>
    public bool Update(bool allowed = true)
    {
        bool pressed = source.IsTogglePressed();
        bool before = Visible;
        if (!allowed)
            Visible = false;
        else if (pressed && !_wasPressed)
            Visible = !Visible;

        _wasPressed = pressed;
        return Visible != before;
    }

    /// <summary>Closes the overlay, e.g. when it could not open.</summary>
    public void Hide() => Visible = false;
}
