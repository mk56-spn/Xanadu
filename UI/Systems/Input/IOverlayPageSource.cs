namespace Xanadu.Systems.Input;

/// <summary>Page, selection, and sort controls for multi-page debug overlays.</summary>
public interface IOverlayPageSource
{
    bool PreviousPressed();

    bool NextPressed();

    /// <summary>Moves the list selection up. Unused by overlays without a selection.</summary>
    bool SelectionUpPressed() => false;

    /// <summary>Moves the list selection down. Unused by overlays without a selection.</summary>
    bool SelectionDownPressed() => false;

    /// <summary>Cycles the list sort order. Unused by overlays without sort modes.</summary>
    bool SortTogglePressed() => false;
}
