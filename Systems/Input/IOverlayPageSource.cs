namespace Writer.Systems.Input;

/// <summary>Page controls for multi-page debug overlays.</summary>
public interface IOverlayPageSource
{
    bool PreviousPressed();

    bool NextPressed();
}
