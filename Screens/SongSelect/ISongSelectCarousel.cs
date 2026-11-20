namespace Xanadu.Screens.SongSelect;

/// <summary>What the song-select list's input systems may ask of whoever runs the scene.</summary>
public interface ISongSelectCarousel
{
    /// <summary>Moves the selection by this many levels; false when it could not move.</summary>
    bool TryStepCarousel(int direction);

    /// <summary>True while a level's right-click dialog is open over the list.</summary>
    bool IsLevelDialogOpen { get; }
}
