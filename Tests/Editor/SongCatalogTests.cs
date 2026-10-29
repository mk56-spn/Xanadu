using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Xanadu.Ecs.Storage;
using Xanadu.Levels;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class SongCatalogTests
{
    [Test]
    public void CatalogContainsEveryBundledSongAndItsMetadata()
    {
        string root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../"));
        string catalogPath = Path.Combine(root, "Songs", "catalog.json");
        SongDocument[] songs =
            JsonFileStore.Load<SongDocument[]>(catalogPath)!;

        Assert.That(songs, Has.Length.EqualTo(3));

        SongDocument mortal = Song(songs, "Mortal Coil");
        Assert.That(mortal.Artist, Is.EqualTo("Helblinde"));
        Assert.That(mortal.Bpm, Is.EqualTo(200));
        Assert.That(mortal.OffsetMs, Is.EqualTo(1211));

        SongDocument dream = Song(songs, "Our Everlasting Dream");
        Assert.That(dream.Artist, Is.EqualTo("Helblinde"));
        Assert.That(dream.Bpm, Is.EqualTo(175));
        Assert.That(dream.OffsetMs, Is.EqualTo(0));

        SongDocument unconquered = Song(songs, "Unconquered");
        Assert.That(unconquered.Artist, Is.EqualTo("Helblinde"));
        Assert.That(unconquered.Bpm, Is.EqualTo(140));
        Assert.That(unconquered.OffsetMs, Is.EqualTo(-11));

        foreach (SongDocument song in songs)
        {
            Assert.That(song.Id, Has.Length.EqualTo(32), song.Title);
            Assert.That(song.Slug, Does.StartWith(SlugRoot(song.Title)), song.Title);
            Assert.That(song.AudioPath, Does.StartWith("res://Assets/Songs/"), song.Title);
            Assert.That(song.ContentHash, Has.Length.EqualTo(64), song.Title);

            string audio = Path.Combine(root, song.AudioPath["res://".Length..]);
            Assert.That(File.Exists(audio), Is.True, song.Title);
            Assert.That(Hash(audio), Is.EqualTo(song.ContentHash), song.Title);
        }
    }

    private static SongDocument Song(SongDocument[] songs, string title) =>
        songs.Single(song => song.Title == title);

    private static string SlugRoot(string title) =>
        title.ToLowerInvariant().Replace(' ', '-') + "-";

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
