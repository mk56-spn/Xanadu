using System.IO;
using System.Linq;
using NUnit.Framework;
using Writer.Ecs.Storage;
using Writer.Levels;
using Writer.Ecs;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class MortalCoilChartTests
{
    [Test]
    public void CharacterTestMapUsesTheBundledSongTiming()
    {
        string root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../"));
        LevelDocument level = JsonFileStore.Load<LevelDocument>(
            Path.Combine(root, "Levels", "mortal-coil-character-test.json"))!;
        SongDocument song = JsonFileStore.Load<SongDocument[]>(
            Path.Combine(root, "Songs", "catalog.json"))!.Single();

        Assert.That(level.SongId, Is.EqualTo(song.Id));
        Assert.That(level.StartPoints, Is.Empty);
        Assert.That(level.Notes, Has.Count.EqualTo(7));
        Assert.That(level.Notes.Count(note => note.Kind == NoteKind.Movement),
            Is.EqualTo(4));
        Assert.That(level.Notes.All(note =>
            (note.TimeMs - song.OffsetMs) % 300 == 0), Is.True);
        Assert.That(level.Notes[4].DashDirection,
            Is.EqualTo(DashDirection.DownRight));
    }
}
