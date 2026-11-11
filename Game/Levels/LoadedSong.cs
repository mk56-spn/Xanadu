namespace Xanadu.Levels;

/// <summary>
/// A song ready to play: its catalog entry and the audio file it plays. The
/// library hands these out from <see cref="SongStore.Open"/>, having found the
/// file; nothing else works out where a song's audio lives.
/// </summary>
public sealed record LoadedSong(SongDocument Info, string AudioFile)
{
    public string Id => Info.Id;
    public string Slug => Info.Slug;
    public string Title => Info.Title;
    public string Artist => Info.Artist;
    public string Genre => Info.Genre;
    public double Bpm => Info.Bpm;
    public long OffsetMs => Info.OffsetMs;

    /// <summary>The song's whole timing as one map.</summary>
    public TempoMap Tempo() => Info.Tempo();
}
