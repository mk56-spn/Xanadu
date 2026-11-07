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
/// Owns the song library. The catalog is a permanent fixture of the project
/// (<c>res://Songs/catalog.json</c>); the audio lives outside the codebase in
/// <c>user://songs</c>, one folder per song id.
/// </summary>
public sealed class SongStore
{
    public const string LibraryFolder = "user://songs";
    public const string CatalogFile = "res://Songs/catalog.json";

    private static readonly Regex SlugPattern = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled);

    private string? _root;
    private readonly string? _catalogPath;

    /// <summary>Set by the game bootstrap once Godot paths are available.</summary>
    public static string? CatalogFilePath { get; set; }

    public SongStore(string? root = null, string? catalogPath = null)
    {
        _root = root;
        _catalogPath = catalogPath;
    }

    public string Root => _root ??=
        ProjectSettings.GlobalizePath(LibraryFolder);

    public string CatalogPath =>
        _catalogPath ?? CatalogFilePath ?? Path.Combine(Root, "catalog.json");

    public IReadOnlyList<SongDocument> LoadAll()
    {
        if (!File.Exists(CatalogPath))
            return Array.Empty<SongDocument>();

        try
        {
            SongDocument[]? catalog =
                JsonFileStore.Load<SongDocument[]>(CatalogPath);
            if (catalog is null)
                return Array.Empty<SongDocument>();
            return catalog
                .Where(song => !string.IsNullOrWhiteSpace(song.Id))
                .OrderBy(song => song.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception)
        {
            // A broken catalog should not take down the game.
            return Array.Empty<SongDocument>();
        }
    }

    public string ResolveAudioPath(SongDocument song)
    {
        return Path.IsPathRooted(song.AudioPath)
            ? song.AudioPath
            : Path.Combine(Root, song.Id, song.AudioPath);
    }

    public bool AudioExists(SongDocument song) =>
        File.Exists(ResolveAudioPath(song));

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

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the song library. If the same
    /// audio was imported before, the existing song is returned instead.
    /// </summary>
    public SongDocument Import(
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
            AudioPath = Path.GetFileName(audioPath),
            Bpm = Math.Clamp(bpm, 1, 1000),
            OffsetMs = offsetMs,
            ContentHash = hash,
        };
        Save(song);
        return song;
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
        WriteCatalog(songs);
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
