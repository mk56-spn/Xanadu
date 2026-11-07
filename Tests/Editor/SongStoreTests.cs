using System;
using System.IO;
using NUnit.Framework;
using Xanadu.Levels;

namespace Xanadu.Tests.Editor;

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
        Assert.That(song.AudioPath, Is.EqualTo("audio.wav"));
        Assert.That(File.Exists(_songs.ResolveAudioPath(song)), Is.True);
        Assert.That(_songs.ResolveAudioPath(song), Does.StartWith(Path.Combine(_root, song.Id)));
        Assert.That(File.Exists(Path.Combine(_root, "catalog.json")), Is.True);
    }

    [Test]
    public void CatalogResolvesExpectedAudioPresence()
    {
        string catalog = Path.Combine(_root, "catalog.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(catalog, """
        [
          { "Id": "present", "Slug": "present", "Title": "Present", "AudioPath": "audio.mp3" },
          { "Id": "missing", "Slug": "missing", "Title": "Missing", "AudioPath": "audio.mp3" }
        ]
        """);
        Directory.CreateDirectory(Path.Combine(_root, "present"));
        File.WriteAllText(Path.Combine(_root, "present", "audio.mp3"), "audio");

        var store = new SongStore(_root, catalog);
        SongDocument present = store.Find("present")!;
        SongDocument missing = store.Find("missing")!;

        Assert.That(store.AudioExists(present), Is.True);
        Assert.That(store.AudioExists(missing), Is.False);
        Assert.That(store.LoadAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public void ImportAddsAPermanentCatalogEntry()
    {
        var store = new SongStore(_root);

        SongDocument song = store.Import(
            CreateAudio("catalog-add.mp3", "catalog-audio"),
            "Catalog Song");

        Assert.That(File.Exists(Path.Combine(_root, "catalog.json")), Is.True);
        Assert.That(store.Find(song.Id)?.Title, Is.EqualTo("Catalog Song"));
        Assert.That(store.AudioExists(store.Find(song.Id)!), Is.True);
    }

    [Test]
    public void DeleteRemovesTheCatalogEntryAndAudio()
    {
        SongDocument song = _songs.Import(
            CreateAudio("delete.wav", "delete-audio"),
            "Doomed");

        _songs.Delete(song);

        Assert.That(_songs.Find(song.Id), Is.Null);
        Assert.That(Directory.Exists(Path.Combine(_root, song.Id)), Is.False);
    }

    [Test]
    public void ImportDoesNotHashMatchSongs()
    {
        string first = CreateAudio("first.wav", "same-audio");
        string second = CreateAudio("second.wav", "same-audio");

        SongDocument a = _songs.Import(first, "First");
        SongDocument b = _songs.Import(second, "Second");

        Assert.That(b.Id, Is.Not.EqualTo(a.Id));
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

    [Test]
    public void ImportReadsId3TitleAndArtist()
    {
        string path = Path.Combine(_root, "tagged.mp3");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(path, BuildId3Mp3("Tagged Title", "Tagged Artist"));

        SongDocument song = _songs.Import(path);

        Assert.That(song.Title, Is.EqualTo("Tagged Title"));
        Assert.That(song.Artist, Is.EqualTo("Tagged Artist"));
    }

    private static byte[] BuildId3Mp3(string title, string artist)
    {
        var frames = new System.Collections.Generic.List<byte>();
        AddFrame(frames, "TIT2", title);
        AddFrame(frames, "TPE1", artist);
        byte[] body = frames.ToArray();
        int size = body.Length;
        var file = new System.Collections.Generic.List<byte>
        {
            (byte)'I', (byte)'D', (byte)'3', 3, 0, 0,
            (byte)((size >> 21) & 0x7F),
            (byte)((size >> 14) & 0x7F),
            (byte)((size >> 7) & 0x7F),
            (byte)(size & 0x7F),
        };
        file.AddRange(body);
        return file.ToArray();
    }

    private static void AddFrame(
        System.Collections.Generic.List<byte> frames,
        string id,
        string value)
    {
        byte[] text = System.Text.Encoding.Latin1.GetBytes(value);
        int size = text.Length + 1;
        frames.AddRange(System.Text.Encoding.ASCII.GetBytes(id));
        frames.Add((byte)(size >> 24));
        frames.Add((byte)(size >> 16));
        frames.Add((byte)(size >> 8));
        frames.Add((byte)size);
        frames.Add(0);
        frames.Add(0);
        frames.Add(0);
        frames.AddRange(text);
    }

    private string CreateAudio(string name, string content)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
