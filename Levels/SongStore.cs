using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using Xanadu.Ecs.Storage;

namespace Xanadu.Levels;

/// <summary>
/// The song library, and the only code that touches it on disk. The catalog is
/// a permanent fixture of the project (<c>res://Songs/catalog.json</c>); the
/// audio lives outside the codebase in <c>user://songs</c>, one folder per song
/// id. The catalog is read once and kept; the store's own writes update it.
/// Everything else asks <see cref="Shared"/>: <see cref="Open"/> for a song to
/// play, <see cref="Find"/> for its details.
/// </summary>
public sealed class SongStore
{
    public const string LibraryFolder = "user://songs";
    public const string CatalogFile = "res://Songs/catalog.json";

    private static readonly Regex SlugPattern = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled);

    private static SongStore? _shared;

    private string? _root;
    private readonly string? _catalogPath;
    private SongDocument[]? _catalog;

    /// <summary>
    /// The game's library. The bootstrap sets it once Godot paths are known;
    /// tests may swap in one of their own.
    /// </summary>
    public static SongStore Shared
    {
        get => _shared ??= new SongStore();
        set => _shared = value;
    }

    public SongStore(string? root = null, string? catalogPath = null)
    {
        _root = root;
        _catalogPath = catalogPath;
    }

    public string Root => _root ??=
        ProjectSettings.GlobalizePath(LibraryFolder);

    public string CatalogPath =>
        _catalogPath ?? Path.Combine(Root, "catalog.json");

    /// <summary>Every song in the catalog, by title, whether or not its audio is there.</summary>
    public IReadOnlyList<SongDocument> LoadAll() => _catalog ??= ReadCatalog();

    /// <summary>The song with this id, ready to play; null when the catalog lacks it or its audio is gone.</summary>
    public LoadedSong? Open(string id) =>
        Find(id) is { } song ? Open(song) : null;

    /// <summary>Every song whose audio is there, ready to play, by title.</summary>
    public IReadOnlyList<LoadedSong> Playable()
    {
        var songs = new List<LoadedSong>();
        foreach (SongDocument song in LoadAll())
        {
            if (Open(song) is { } loaded)
                songs.Add(loaded);
        }

        return songs;
    }

    /// <summary>A catalog song's details, for showing or retiming it; <see cref="Open"/> it to play it.</summary>
    public SongDocument? Find(string id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : LoadAll().FirstOrDefault(song => song.Id == id);

    public SongDocument? FindByHash(string contentHash) =>
        string.IsNullOrWhiteSpace(contentHash)
            ? null
            : LoadAll().FirstOrDefault(song =>
                string.Equals(song.ContentHash, contentHash,
                    StringComparison.OrdinalIgnoreCase));

    private LoadedSong? Open(SongDocument song)
    {
        string file = AudioFile(song);
        return File.Exists(file) ? new LoadedSong(song, file) : null;
    }

    private string AudioFile(SongDocument song) =>
        Path.IsPathRooted(song.AudioFileName)
            ? song.AudioFileName
            : Path.Combine(GetDirectory(song), song.AudioFileName);

    private SongDocument[] ReadCatalog()
    {
        if (!File.Exists(CatalogPath))
            return Array.Empty<SongDocument>();

        try
        {
            SongDocument[]? catalog =
                JsonFileStore.Load<SongDocument[]>(CatalogPath);
            if (catalog is null)
                return Array.Empty<SongDocument>();
            return Sorted(catalog.Where(song => !string.IsNullOrWhiteSpace(song.Id)));
        }
        catch (Exception)
        {
            // A broken catalog should not take down the game.
            return Array.Empty<SongDocument>();
        }
    }

    private static SongDocument[] Sorted(IEnumerable<SongDocument> songs) =>
        songs.OrderBy(song => song.Title, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the song library. If the same
    /// audio was imported before, the existing song is returned instead.
    /// </summary>
    public LoadedSong Import(
        string sourcePath,
        string title = "",
        string artist = "",
        int bpm = 120,
        long offsetMs = 0)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Song audio was not found.", sourcePath);

        string hash = HashFile(sourcePath);

        string id = Guid.NewGuid().ToString("N");
        AudioTagReader.TryRead(sourcePath, out string tagTitle, out string tagArtist);
        string fallbackTitle = Path.GetFileNameWithoutExtension(sourcePath);
        string resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? (string.IsNullOrWhiteSpace(tagTitle) ? fallbackTitle : tagTitle)
            : title.Trim();
        string resolvedArtist = string.IsNullOrWhiteSpace(artist)
            ? tagArtist
            : artist.Trim();
        string slug = Slugify(resolvedTitle) + "-" + id[..8];
        string directory = Path.Combine(Root, id);
        Directory.CreateDirectory(directory);

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".audio";

        string audioPath = Path.Combine(directory, "audio" + extension);
        string temporaryPath = audioPath + ".tmp";
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, audioPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }

        var song = new SongDocument
        {
            Id = id,
            Slug = slug,
            Title = resolvedTitle,
            Artist = resolvedArtist,
            AudioFileName = Path.GetFileName(audioPath),
            Bpm = Math.Clamp(bpm, 1, 1000),
            OffsetMs = offsetMs,
            ContentHash = hash,
        };
        Save(song);
        return new LoadedSong(song, audioPath);
    }

    public void Save(SongDocument song)
    {
        if (string.IsNullOrWhiteSpace(song.Id))
            throw new InvalidOperationException("Song is missing its id.");

        SongDocument[] songs = LoadAll().ToArray();
        int index = Array.FindIndex(songs, item => item.Id == song.Id);
        if (index >= 0)
            songs[index] = song;
        else
            songs = songs.Append(song).ToArray();
        WriteCatalog(Sorted(songs));
    }

    public void Delete(SongDocument song)
    {
        WriteCatalog(LoadAll().Where(item => item.Id != song.Id).ToArray());
        string directory = GetDirectory(song);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private void WriteCatalog(SongDocument[] songs)
    {
        string? directory = Path.GetDirectoryName(CatalogPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        JsonFileStore.Save(CatalogPath, songs);
        _catalog = songs;
    }

    private string GetDirectory(SongDocument song) =>
        Path.Combine(Root, song.Id);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Slugify(string value)
    {
        string slug = SlugPattern.Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "song";
        return slug.Length > 48 ? slug[..48].Trim('-') : slug;
    }
}
