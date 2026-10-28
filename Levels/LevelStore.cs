using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Xanadu.Levels;

/// <summary>Reads and writes XMF level maps below user://levels.</summary>
public static class LevelStore
{
    public const string LevelsFolder = "user://levels";
    private const string FallbackFolder = "res://Levels";

    private static string? _directory;

    public static string Directory => _directory ??= ResolveDirectory();

    private static string ResolveDirectory()
    {
        string userDirectory = ProjectSettings.GlobalizePath(LevelsFolder);
        if (IsWritable(userDirectory))
            return userDirectory;

        string fallbackDirectory = ProjectSettings.GlobalizePath(FallbackFolder);
        System.IO.Directory.CreateDirectory(fallbackDirectory);
        return fallbackDirectory;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".write-probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string DefaultPath(string name)
    {
        string safe = new(name
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray());

        safe = safe.Trim();
        if (string.IsNullOrEmpty(safe))
            safe = "untitled";

        return Path.Combine(Directory, safe + XmfBinary.Extension);
    }

    public static string NextUnnamedPath() => NextUnnamedPath(Directory);

    public static string NextUnnamedPath(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        for (int index = 1; ; index++)
        {
            string path = Path.Combine(
                directory,
                $"unnamed-{index}{XmfBinary.Extension}");
            if (!File.Exists(path))
                return path;
        }
    }

    public static void EnsureDirectory() =>
        System.IO.Directory.CreateDirectory(Directory);

    /// <summary>
    /// Saves a level as the one variant of it in <paramref name="path"/>. If
    /// the mapset there already has other variants, they are kept; only the
    /// variant with this level's identity is replaced or added.
    /// </summary>
    public static void Save(LevelDocument document, string path)
    {
        XmfMapset mapset = TryLoadMapset(path) ?? new XmfMapset();
        mapset.SongId = document.SongId;
        mapset.Shaders = document.Shaders ?? new List<ShaderDocument>();

        XmfVariant variant = ToVariant(document);
        int index = mapset.Variants.FindIndex(v => v.Id == variant.Id);
        if (index >= 0)
            mapset.Variants[index] = variant;
        else
            mapset.Variants.Add(variant);

        SaveMapset(mapset, path);
    }

    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Loads a level from its mapset's first variant.</summary>
    public static LevelDocument Load(string path)
    {
        XmfMapset mapset = LoadMapset(path);
        XmfVariant variant = mapset.Variants.Count > 0 ? mapset.Variants[0] : new XmfVariant();
        return ToLevelDocument(mapset, variant);
    }

    public static XmfMapset LoadMapset(string path) =>
        LevelSerializer.NormalizeMapset(XmfBinary.ReadMapset(File.ReadAllBytes(path)));

    public static void SaveMapset(XmfMapset mapset, string path) =>
        XmfBinary.SaveMapset(path, mapset);

    private static XmfMapset? TryLoadMapset(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return LoadMapset(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The variant a level document's own chart makes.</summary>
    public static XmfVariant ToVariant(LevelDocument document) => new()
    {
        Id = document.Id,
        Slug = document.Slug,
        Name = document.Name,
        SnapDivision = document.SnapDivision,
        PlayfieldWidth = document.PlayfieldWidth,
        PlayfieldHeight = document.PlayfieldHeight,
        Notes = document.Notes,
        Mines = document.Mines,
        StartPoints = document.StartPoints,
        Blocks = document.Blocks,
        Spikes = document.Spikes,
        Groups = document.Groups,
        PlayerShader = document.PlayerShader,
        FieldShader = document.FieldShader,
    };

    /// <summary>The level document a variant makes, carrying its mapset's song and shaders.</summary>
    public static LevelDocument ToLevelDocument(XmfMapset mapset, XmfVariant variant) => new()
    {
        Version = mapset.Version,
        Id = variant.Id,
        Slug = variant.Slug,
        SongId = mapset.SongId,
        Name = variant.Name,
        SnapDivision = variant.SnapDivision,
        PlayfieldWidth = variant.PlayfieldWidth,
        PlayfieldHeight = variant.PlayfieldHeight,
        Notes = variant.Notes,
        Mines = variant.Mines,
        StartPoints = variant.StartPoints,
        Blocks = variant.Blocks,
        Spikes = variant.Spikes,
        Groups = variant.Groups,
        Shaders = mapset.Shaders,
        PlayerShader = variant.PlayerShader,
        FieldShader = variant.FieldShader,
    };

    /// <summary>Loads every valid level from user data and the bundled Levels folder.</summary>
    public static List<LevelEntry> List()
    {
        var songs = new SongStore();
        var entries = new List<LevelEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddFrom(ProjectSettings.GlobalizePath(LevelsFolder));
        AddFrom(ProjectSettings.GlobalizePath(FallbackFolder));

        entries.Sort(static (left, right) =>
        {
            int byAvailability = left.IsBlocked.CompareTo(right.IsBlocked);
            if (byAvailability != 0)
                return byAvailability;

            int byName = string.Compare(
                left.Document.Name,
                right.Document.Name,
                StringComparison.OrdinalIgnoreCase);

            return byName != 0
                ? byName
                : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        });

        return entries;

        void AddFrom(string directory)
        {
            if (!System.IO.Directory.Exists(directory))
                return;

            foreach (string path in System.IO.Directory.EnumerateFiles(
                         directory,
                         "*" + XmfBinary.Extension).OrderBy(
                         static file => file,
                         StringComparer.Ordinal))
            {
                string fullPath = Path.GetFullPath(path);
                if (!seen.Add(fullPath))
                    continue;

                try
                {
                    LevelDocument document = Load(fullPath);
                    SongDocument? song = songs.Find(document.SongId);
                    entries.Add(new LevelEntry(
                        fullPath,
                        document,
                        song,
                        song is not null && songs.AudioExists(song)));
                }
                catch (Exception)
                {
                    // Ignore malformed level files and keep the rest of the carousel usable.
                }
            }
        }
    }
}
