using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace Writer.Settings;

/// <summary>Persisted display options such as vertical sync.</summary>
public sealed class DisplaySettings
{
    private const string DefaultPath = "user://display.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly bool _persist;
    private readonly bool _apply;

    public DisplaySettings(
        string? path = null,
        bool persist = true,
        bool apply = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;
        _apply = apply;
        if (_persist)
            Load();
        Apply();
    }

    public bool VSyncEnabled { get; private set; } = true;

    public void SetVSync(bool enabled)
    {
        VSyncEnabled = enabled;
        Apply();
        Save();
    }

    public void ToggleVSync() => SetVSync(!VSyncEnabled);

    public void Load()
    {
        try
        {
            string path = ResolvePath();
            if (!File.Exists(path))
                return;

            DisplayData? data = JsonSerializer.Deserialize<DisplayData>(
                File.ReadAllText(path),
                JsonOptions);
            if (data is not null)
                VSyncEnabled = data.VSyncEnabled;
        }
        catch (Exception)
        {
            // Malformed display settings fall back to the default.
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
                    new DisplayData { VSyncEnabled = VSyncEnabled },
                    JsonOptions));
        }
        catch (Exception)
        {
            // Persistence is best-effort; the session still uses the value.
        }
    }

    public void Apply()
    {
        if (!_apply)
            return;

        try
        {
            DisplayServer.WindowSetVsyncMode(
                VSyncEnabled
                    ? DisplayServer.VSyncMode.Enabled
                    : DisplayServer.VSyncMode.Disabled);
        }
        catch (Exception)
        {
            // Headless test and export contexts may not expose a window.
        }
    }

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(_path)
            : _path;

    private sealed class DisplayData
    {
        public bool VSyncEnabled { get; set; } = true;
    }
}
