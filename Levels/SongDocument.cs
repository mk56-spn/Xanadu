namespace Xanadu.Levels;

/// <summary>Program-owned song metadata and shared timing.</summary>
public sealed class SongDocument
{
    public int Version { get; set; } = 1;

    /// <summary>Stable identity. Never derived from the title.</summary>
    public string Id { get; set; } = "";

    /// <summary>Stable human-readable slug generated once on import.</summary>
    public string Slug { get; set; } = "";

    public string Title { get; set; } = "";

    public string Artist { get; set; } = "";

    /// <summary>The song's genre, shown on song select. Empty when unknown.</summary>
    public string Genre { get; set; } = "";

    /// <summary>Absolute path to the copied audio inside the song store.</summary>
    public string AudioPath { get; set; } = "";

    public int Bpm { get; set; } = 120;

    public long OffsetMs { get; set; }

    /// <summary>SHA-256 of the source audio, used to reject duplicate imports.</summary>
    public string ContentHash { get; set; } = "";
}
