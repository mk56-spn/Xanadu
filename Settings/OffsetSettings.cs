using System;
using System.IO;
using System.Text.Json;

namespace Xanadu.Settings;

/// <summary>Persisted global input/audio calibration offset.</summary>
public sealed class OffsetSettings
{
    private const string DefaultPath = "user://offset.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly bool _persist;

    public OffsetSettings(string? path = null, bool persist = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;
        if (_persist)
            Load();
    }

    public long OffsetMs { get; private set; }

    public void Set(long offsetMs)
    {
        OffsetMs = Math.Clamp(offsetMs, -300, 300);
        Save();
    }

    public void Reset()
    {
        OffsetMs = 0;
        Save();
    }

    public void Load()
    {
        try
        {
            string path = ResolvePath();
            if (!File.Exists(path))
                return;

            OffsetData? data = JsonSerializer.Deserialize<OffsetData>(
                File.ReadAllText(path),
                JsonOptions);
            if (data is not null)
                OffsetMs = Math.Clamp(data.OffsetMs, -300, 300);
        }
        catch (Exception)
        {
            // A malformed calibration file falls back to zero.
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
                    new OffsetData { OffsetMs = OffsetMs },
                    JsonOptions));
        }
        catch (Exception)
        {
            // Persistence is best-effort; the session still uses the value.
        }
    }

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? Godot.ProjectSettings.GlobalizePath(_path)
            : _path;

    private sealed class OffsetData
    {
        public long OffsetMs { get; set; }
    }
}
