using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// The audio's file name inside the song's library folder. Only
    /// <see cref="SongStore"/> turns it into a file: a <see cref="LoadedSong"/> carries the path.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("AudioPath")]
    internal string AudioFileName { get; set; } = "";

    /// <summary>The tempo from the offset on, until the first change.</summary>
    public double Bpm { get; set; } = 120;

    /// <summary>Where beat 0 falls in the audio.</summary>
    public long OffsetMs { get; set; }

    /// <summary>Beats to a bar from the offset on, until the first change.</summary>
    public int BeatsPerBar { get; set; } = 4;

    /// <summary>Later tempo sections, each from its moment on (the song timing screen edits them).</summary>
    public List<TempoChangeDocument> TempoChanges { get; set; } = new();

    /// <summary>SHA-256 of the source audio, used to reject duplicate imports.</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>The song's whole timing as one map.</summary>
    public TempoMap Tempo() => TempoMap.From(
        new TempoPoint(OffsetMs, Bpm, BeatsPerBar),
        (TempoChanges ?? new List<TempoChangeDocument>()).Select(change => change.Point));
}

/// <summary>A BPM change: from <see cref="TimeMs"/> on, a new tempo and bar length.</summary>
public sealed class TempoChangeDocument
{
    public double TimeMs { get; set; }

    public double Bpm { get; set; } = 120;

    public int BeatsPerBar { get; set; } = 4;

    public TempoPoint Point => new(TimeMs, Bpm, BeatsPerBar);
}
