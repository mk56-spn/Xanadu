using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xanadu.Systems.Scenes;
using Xanadu.Scenes;

namespace Xanadu.Profiles;

/// <summary>A saved local player profile and their level scores.</summary>
public sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Player";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, ScoreRecord> Scores { get; set; } = new();
}

/// <summary>One play result attached to a profile and level path.</summary>
public sealed class ScoreRecord
{
    public long Score { get; set; }
    public double Accuracy { get; set; }
    public int MaxCombo { get; set; }
    public int NotesHit { get; set; }
    public int TotalNotes { get; set; }
    public string Rank { get; set; } = "FAIL";
    public DateTime PlayedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Persists local profiles, the active selection, and best scores.</summary>
public sealed class ProfileStore
{
    private const string DefaultPath = "user://profiles.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly bool _persist;

    public ProfileStore(string? path = null, bool persist = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;
        if (_persist)
            Load();
    }

    public List<UserProfile> Profiles { get; private set; } = new();

    public string ActiveProfileId { get; private set; } = "";

    [JsonIgnore]
    public UserProfile? ActiveProfile =>
        Profiles.FirstOrDefault(profile => profile.Id == ActiveProfileId);

    public UserProfile Create(string? name = null)
    {
        string baseName = string.IsNullOrWhiteSpace(name)
            ? $"Player {Profiles.Count + 1}"
            : name.Trim();
        string candidate = baseName;
        int suffix = 2;

        while (Profiles.Any(profile =>
                   string.Equals(
                       profile.Name,
                       candidate,
                       StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }

        var profile = new UserProfile
        {
            Name = candidate,
        };
        Profiles.Add(profile);
        ActiveProfileId = profile.Id;
        Save();
        return profile;
    }

    public bool Select(string id)
    {
        if (Profiles.All(profile => profile.Id != id))
            return false;

        ActiveProfileId = id;
        Save();
        return true;
    }

    public ScoreRecord? Best(string levelPath) =>
        ActiveProfile?.Scores.TryGetValue(NormalizeKey(levelPath), out ScoreRecord? score) == true
            ? score
            : null;

    /// <summary>All profile scores for a level, highest first.</summary>
    public IReadOnlyList<ScoreEntry> AllScores(string levelPath)
    {
        string key = NormalizeKey(levelPath);
        var entries = new List<ScoreEntry>();
        foreach (UserProfile profile in Profiles)
        {
            if (profile.Scores.TryGetValue(key, out ScoreRecord? score))
                entries.Add(new ScoreEntry(profile.Name, score));
        }

        entries.Sort(static (a, b) => b.Record.Score.CompareTo(a.Record.Score));
        return entries;
    }

    /// <summary>Stores a result if it beats the active profile's previous best.</summary>
    public ScoreRecord? RecordScore(string levelPath, PlayResults result)
    {
        UserProfile? profile = ActiveProfile;
        if (profile is null)
            return null;

        string key = NormalizeKey(levelPath);
        if (profile.Scores.TryGetValue(key, out ScoreRecord? current) &&
            current.Score > result.Score)
        {
            return current;
        }

        var record = new ScoreRecord
        {
            Score = result.Score,
            Accuracy = result.Accuracy,
            MaxCombo = result.MaxCombo,
            NotesHit = result.NotesHit,
            TotalNotes = result.TotalNotes,
            Rank = Xanadu.Scoring.ScoreRules.RankFor(result.Accuracy),
            PlayedUtc = DateTime.UtcNow,
        };
        profile.Scores[key] = record;
        Save();
        return record;
    }

    public void Load()
    {
        try
        {
            string path = ResolvePath();
            if (!File.Exists(path))
                return;

            ProfileData? data = JsonSerializer.Deserialize<ProfileData>(
                File.ReadAllText(path),
                JsonOptions);
            if (data is null)
                return;

            Profiles = data.Profiles ?? new List<UserProfile>();
            foreach (UserProfile profile in Profiles)
                profile.Scores ??= new Dictionary<string, ScoreRecord>();

            ActiveProfileId = Profiles.Any(profile => profile.Id == data.ActiveProfileId)
                ? data.ActiveProfileId
                : "";
        }
        catch (Exception)
        {
            Profiles = new List<UserProfile>();
            ActiveProfileId = "";
        }
    }

    public void Save()
    {
        if (!_persist)
            return;

        try
        {
            string path = ResolvePath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    new ProfileData
                    {
                        ActiveProfileId = ActiveProfileId,
                        Profiles = Profiles,
                    },
                    JsonOptions));
        }
        catch (Exception)
        {
            // Keep the in-memory session usable if persistence fails.
        }
    }

    private static string NormalizeKey(string levelPath)
    {
        if (string.IsNullOrWhiteSpace(levelPath))
            return "";

        try
        {
            return Path.GetFullPath(levelPath);
        }
        catch (Exception)
        {
            return levelPath;
        }
    }

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? Godot.ProjectSettings.GlobalizePath(_path)
            : _path;

    private sealed class ProfileData
    {
        public string ActiveProfileId { get; set; } = "";
        public List<UserProfile> Profiles { get; set; } = new();
    }
}

/// <summary>A profile score on one level.</summary>
public sealed record ScoreEntry(string ProfileName, ScoreRecord Record);
