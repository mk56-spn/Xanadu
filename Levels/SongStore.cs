using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace Writer.Levels;

/// <summary>
/// Owns the song library. Imported audio is copied into a per-song folder and
/// duplicate audio is rejected by content hash.
/// </summary>
public sealed class SongStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex SlugPattern = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled);

    private string? _root;

    public SongStore(string? root = null)
    {
        _root = root;
    }

    public string Root => _root ??=
        ProjectSettings.GlobalizePath("user://songs");

    public IReadOnlyList<SongDocument> LoadAll()
    {
        if (!Directory.Exists(Root))
            return Array.Empty<SongDocument>();

        var songs = new List<SongDocument>();
        foreach (string path in Directory.EnumerateFiles(
                     Root, "song.json", SearchOption.AllDirectories))
        {
            try
            {
                SongDocument? song = JsonSerializer.Deserialize<SongDocument>(
                    File.ReadAllText(path),
                    JsonOptions);
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
        SongDocument? existing = FindByHash(hash);
        if (existing is not null)
            return existing;

        string id = Guid.NewGuid().ToString("N");
        string fallbackTitle = Path.GetFileNameWithoutExtension(sourcePath);
        string resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? fallbackTitle
            : title.Trim();
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
            Artist = artist.Trim(),
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

        string directory = GetDirectory(song);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "song.json");
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(song, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
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
