using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Ecs.Storage;

namespace Xanadu.Tests.Timing;

/// <summary>The song timing screen's edits: songs are timed once, globally.</summary>
[TestFixture]
public sealed class SongTempoEditsTests
{
    private static SongDocument Song() => new() { Id = "song", Bpm = 120, OffsetMs = 0 };

    [Test]
    public void AChangeIsAddedFourBarsAfterTheLastSection()
    {
        SongDocument song = Song();

        int added = SongTempoEdits.Add(song);

        Assert.That(added, Is.EqualTo(0));
        Assert.That(song.TempoChanges[0].TimeMs, Is.EqualTo(8_000), "16 beats at 120 BPM");
        Assert.That(song.TempoChanges[0].Bpm, Is.EqualTo(120));
    }

    [Test]
    public void EditsAreStoredTheWayTheMapReadsThem()
    {
        SongDocument song = Song();
        SongTempoEdits.Add(song);
        SongTempoEdits.Add(song);

        Assert.That(SongTempoEdits.Set(song, 1, timeMs: 1_100, bpm: 128.5), Is.True);

        Assert.That(song.TempoChanges[0].TimeMs, Is.EqualTo(1_500), "moved onto a whole beat and sorted first");
        Assert.That(song.TempoChanges[0].Bpm, Is.EqualTo(128.5));
        Assert.That(SongTempoEdits.SetBase(song, bpm: 0.1), Is.True);
        Assert.That(song.Bpm, Is.EqualTo(TempoMap.MinBpm), "clamped");
        Assert.That(SongTempoEdits.Remove(song, 0), Is.True);
        Assert.That(song.TempoChanges, Has.Count.EqualTo(1));
    }

    [Test]
    public void ThePositionReadsAsBeatAndBar()
    {
        TempoMap map = TempoMap.Single(120, 0);

        Assert.That(SongTempoEdits.Position(map, 8_000), Is.EqualTo("BEAT 17 · BAR 5.1"));
        Assert.That(SongTempoEdits.Position(map, 8_500), Is.EqualTo("BEAT 18 · BAR 5.2"));
    }

    [Test]
    public void TempoChangesSurviveTheSongFile()
    {
        SongDocument song = Song();
        song.Bpm = 97.5;
        song.TempoChanges = new List<TempoChangeDocument> { new() { TimeMs = 4_000, Bpm = 140, BeatsPerBar = 3 } };
        string path = Path.Combine(Path.GetTempPath(), $"xanadu-song-{System.Guid.NewGuid():N}.json");
        try
        {
            JsonFileStore.Save(path, song);
            SongDocument read = JsonFileStore.Load<SongDocument>(path)!;

            Assert.That(read.Bpm, Is.EqualTo(97.5));
            Assert.That(read.TempoChanges[0].Bpm, Is.EqualTo(140));
            Assert.That(read.TempoChanges[0].BeatsPerBar, Is.EqualTo(3));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
