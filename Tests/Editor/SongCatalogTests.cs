using System.IO;
using System.Linq;
using NUnit.Framework;
using Writer.Ecs.Storage;
using Writer.Levels;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class SongCatalogTests
{
    [Test]
    public void CatalogContainsTheBundledTestSong()
    {
        string root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../"));
        string catalogPath = Path.Combine(root, "Songs", "catalog.json");
        SongDocument[] songs =
            JsonFileStore.Load<SongDocument[]>(catalogPath)!;

        Assert.That(songs, Has.Length.EqualTo(1));
        SongDocument song = songs.Single();
        Assert.That(song.Title, Is.EqualTo("Mortal Coil"));
        Assert.That(song.Artist, Is.EqualTo("Helblinde"));
        Assert.That(song.Bpm, Is.EqualTo(200));
        Assert.That(song.OffsetMs, Is.EqualTo(1211));
        Assert.That(song.AudioPath, Does.StartWith("res://Assets/Songs/"));

        string audio = Path.Combine(
            root,
            song.AudioPath["res://".Length..]);
        Assert.That(File.Exists(audio), Is.True);
    }
}
