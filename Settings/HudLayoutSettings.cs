using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Xanadu.Settings;

/// <summary>Persisted visibility and normalized position for play HUD items.</summary>
public sealed class HudLayoutSettings
{
    private const string DefaultPath = "user://hud-layout.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly bool _persist;
    private readonly Dictionary<string, HudLayoutEntry> _items = new();

    public HudLayoutSettings(string? path = null, bool persist = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;
        if (_persist)
            Load();
    }

    public bool TryGet(string id, out HudLayoutEntry entry) =>
        _items.TryGetValue(id, out entry!);

    public void Set(string id, float normalizedX, float normalizedY, bool visible)
    {
        _items[id] = new HudLayoutEntry
        {
            X = Math.Clamp(normalizedX, 0f, 1f),
            Y = Math.Clamp(normalizedY, 0f, 1f),
            Visible = visible,
        };
        Save();
    }

    public void SetVisible(string id, bool visible)
    {
        if (!_items.TryGetValue(id, out HudLayoutEntry? entry))
            return;

        entry.Visible = visible;
        Save();
    }

    public void Load()
    {
        try
        {
            string path = ResolvePath();
            if (!File.Exists(path))
                return;

            HudLayoutData? data = JsonSerializer.Deserialize<HudLayoutData>(
                File.ReadAllText(path),
                JsonOptions);
            if (data?.Items is null)
                return;

            _items.Clear();
            foreach ((string id, HudLayoutEntry entry) in data.Items)
                _items[id] = entry;
        }
        catch (Exception)
        {
            // Malformed HUD layout falls back to defaults.
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
                    new HudLayoutData { Items = _items },
                    JsonOptions));
        }
        catch (Exception)
        {
            // Persistence is best-effort; the session still uses the layout.
        }
    }

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? Godot.ProjectSettings.GlobalizePath(_path)
            : _path;

    public sealed class HudLayoutEntry
    {
        public float X { get; set; }
        public float Y { get; set; }
        public bool Visible { get; set; } = true;
    }

    private sealed class HudLayoutData
    {
        public Dictionary<string, HudLayoutEntry>? Items { get; set; }
    }
}
