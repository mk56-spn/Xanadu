namespace Xanadu.Levels;

/// <summary>A loaded level and the program-owned song it references.</summary>
public sealed record LevelEntry(
    string Path,
    LevelDocument Document,
    SongDocument? Song = null,
    bool? SongAvailable = null)
{
    public bool HasSong => Song is not null;
    public bool IsSongAvailable =>
        Song is not null && (SongAvailable ?? true);
    public bool IsBlocked =>
        !string.IsNullOrWhiteSpace(Document.SongId) &&
        (!HasSong || !IsSongAvailable);
}
