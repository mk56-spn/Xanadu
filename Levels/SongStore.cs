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
/// Owns the song library. Imported audio is copied into a per-song folder and
/// duplicate audio is rejected by content hash.
/// </summary>
public sealed class SongStore
{
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
        ProjectSettings.GlobalizePath("user://songs");

    public string CatalogPath => _catalogPath ?? CatalogFilePath ?? "";

    public IReadOnlyList<SongDocument> LoadAll()
    {
        if (!string.IsNullOrEmpty(CatalogPath) && File.Exists(CatalogPath))
        {
            try
            {
                SongDocument[]? catalog =
                    JsonFileStore.Load<SongDocument[]>(CatalogPath);
                if (catalog is not null)
                {
                    return catalog
                        .Where(song => !string.IsNullOrWhiteSpace(song.Id))
                        .OrderBy(song => song.Title, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
            }
            catch (Exception)
            {
                // Fall back to the per-song store for compatibility.
            }
        }

        if (!Directory.Exists(Root))
            return Array.Empty<SongDocument>();

        var songs = new List<SongDocument>();
        foreach (string path in Directory.EnumerateFiles(
                     Root, "song.json", SearchOption.AllDirectories))
        {
            try
            {
                SongDocument? song = JsonFileStore.Load<SongDocument>(path);
                if (song is not null && !string.IsNullOrWhiteSpace(song.Id))
                    songs.Add(song);
            }
            catch (Exception)
            {
                // A broken song should not take down the library.
            }
        }

        songs.Sort(static (a, b) =>
            string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return songs;
    }

    public string ResolveAudioPath(SongDocument song)
    {
        if (song.AudioPath.StartsWith("res://", StringComparison.Ordinal))
            return ProjectSettings.GlobalizePath(song.AudioPath);

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
            AudioPath = audioPath,
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

        if (!string.IsNullOrEmpty(CatalogPath) && File.Exists(CatalogPath))
        {
            SongDocument[] songs = LoadAll().ToArray();
            int index = Array.FindIndex(songs, item => item.Id == song.Id);
            if (index >= 0)
                songs[index] = song;
            else
                songs = songs.Append(song).ToArray();
            JsonFileStore.Save(CatalogPath, songs);
            return;
        }

        string directory = GetDirectory(song);
        Directory.CreateDirectory(directory);
        JsonFileStore.Save(Path.Combine(directory, "song.json"), song);
    }

    public void Delete(SongDocument song)
    {
        string directory = GetDirectory(song);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
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
