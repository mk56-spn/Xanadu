namespace Writer.Levels;

/// <summary>A loaded level and the program-owned song it references.</summary>
public sealed record LevelEntry(
    string Path,
    LevelDocument Document,
    SongDocument? Song = null)
{
    public bool HasSong => Song is not null;
    public bool IsBlocked =>
        !string.IsNullOrWhiteSpace(Document.SongId) && Song is null;
}
