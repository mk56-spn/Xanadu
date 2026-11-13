namespace Xanadu.Levels;

/// <summary>
/// A loaded level and its song, opened from the library. <see cref="Song"/> is
/// null when the level names no song, or names one the library cannot play.
/// </summary>
public sealed record LevelEntry(
    string Path,
    LevelDocument Document,
    LoadedSong? Song = null,
    SongDocument? Known = null)
{
    public bool HasSong => Song is not null;

    /// <summary>
    /// A song the catalog knows but whose audio isn't downloaded, listed with
    /// no level file of its own (<see cref="Path"/> is empty) so it shows greyed out.
    /// </summary>
    public bool IsCatalogOnly => string.IsNullOrEmpty(Path);

    /// <summary>The song's title: from its audio when it's there, else from the catalog.</summary>
    public string? SongTitle => Song?.Title ?? Known?.Title;

    /// <summary>The song's artist: from its audio when it's there, else from the catalog.</summary>
    public string? SongArtist => Song?.Artist ?? Known?.Artist;

    /// <summary>The level names a song the library cannot play: it is missing or has no audio.</summary>
    public bool IsBlocked =>
        !string.IsNullOrWhiteSpace(Document.SongId) && Song is null;
}
