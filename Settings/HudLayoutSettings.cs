using System;
using System.Collections.Generic;
using Xanadu.Ecs.Storage;

namespace Xanadu.Settings;

/// <summary>Persisted visibility and normalized position for play HUD items.</summary>
public sealed class HudLayoutSettings
{
    private const string DefaultPath = "user://hud-layout.json";
    private readonly PersistedFile<HudLayoutData> _file;
    private readonly Dictionary<string, HudLayoutEntry> _items = new();

    public HudLayoutSettings(string? path = null, bool persist = true)
    {
        _file = new PersistedFile<HudLayoutData>(path, DefaultPath, persist);
        if (persist)
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
        HudLayoutData? data = _file.Load();
        if (data?.Items is null)
            return;

        _items.Clear();
        foreach ((string id, HudLayoutEntry entry) in data.Items)
            _items[id] = entry;
    }

    public void Save() => _file.Save(new HudLayoutData { Items = _items });

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
