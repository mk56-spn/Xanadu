namespace Xanadu.Levels;

/// <summary>
/// A loaded level and its song, opened from the library. <see cref="Song"/> is
/// null when the level names no song, or names one the library cannot play.
/// </summary>
public sealed record LevelEntry(
    string Path,
    LevelDocument Document,
    LoadedSong? Song = null)
{
    public bool HasSong => Song is not null;

    /// <summary>The level names a song the library cannot play: it is missing or has no audio.</summary>
    public bool IsBlocked =>
        !string.IsNullOrWhiteSpace(Document.SongId) && Song is null;
}
