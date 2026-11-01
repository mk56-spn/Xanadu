namespace Xanadu.Systems.Input;

/// <summary>
/// The wheel notches of this frame. The UI gets the first look and takes them
/// when a scroll view is under the pointer; whatever is left goes to the host's
/// own wheel behaviour, so one notch only ever does one thing.
/// </summary>
public interface IWheelSource
{
    /// <summary>Notches not taken yet this frame; positive is up.</summary>
    int Notches { get; }

    /// <summary>Whether Ctrl or Meta was held for the latest notch.</summary>
    bool Control { get; }

    /// <summary>Whether Shift was held for the latest notch.</summary>
    bool Shift { get; }

    /// <summary>Returns the remaining notches and leaves none for later systems.</summary>
    int TakeWheel();
}
