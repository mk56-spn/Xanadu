using System;
using System.IO;
using NUnit.Framework;
using Writer.Levels;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class SongStoreTests
{
    private string _root = null!;
    private SongStore _songs = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "writer-song-tests-" + Guid.NewGuid().ToString("N"));
        _songs = new SongStore(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void ImportCopiesAudioIntoTheSongFolder()
    {
        string source = CreateAudio("copy.wav", "audio-one");

        SongDocument song = _songs.Import(source, "My Song", "Artist", 174, -35);

        Assert.That(song.Id, Is.Not.Empty);
        Assert.That(song.Slug, Does.StartWith("my-song-"));
        Assert.That(song.Title, Is.EqualTo("My Song"));
        Assert.That(song.Artist, Is.EqualTo("Artist"));
        Assert.That(song.Bpm, Is.EqualTo(174));
        Assert.That(song.OffsetMs, Is.EqualTo(-35));
        Assert.That(File.Exists(song.AudioPath), Is.True);
        Assert.That(song.AudioPath, Does.StartWith(Path.Combine(_root, song.Id)));
        Assert.That(File.Exists(Path.Combine(_root, song.Id, "song.json")), Is.True);
    }

    [Test]
    public void ImportRejectsDuplicateContentByHash()
    {
        string first = CreateAudio("first.wav", "same-audio");
        string second = CreateAudio("second.wav", "same-audio");

        SongDocument a = _songs.Import(first, "First");
        SongDocument b = _songs.Import(second, "Second");

        Assert.That(b.Id, Is.EqualTo(a.Id));
        Assert.That(_songs.LoadAll(), Has.Count.EqualTo(1));
    }

    [Test]
    public void LoadAllReadsSavedSongs()
    {
        SongDocument song = _songs.Import(
            CreateAudio("load.wav", "load-audio"),
            "Loadable",
            "Person");

        SongDocument loaded = _songs.Find(song.Id)!;

        Assert.That(loaded.Title, Is.EqualTo("Loadable"));
        Assert.That(loaded.Artist, Is.EqualTo("Person"));
        Assert.That(loaded.ContentHash, Is.EqualTo(song.ContentHash));
    }

    private string CreateAudio(string name, string content)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
