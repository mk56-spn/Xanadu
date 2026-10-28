using System;
using Xanadu.Ecs.Storage;

namespace Xanadu.Settings;

/// <summary>Persisted global input/audio calibration offset.</summary>
public sealed class OffsetSettings
{
    private const string DefaultPath = "user://offset.json";
    private readonly PersistedFile<OffsetData> _file;

    public OffsetSettings(string? path = null, bool persist = true)
    {
        _file = new PersistedFile<OffsetData>(path, DefaultPath, persist);
        if (persist)
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
        OffsetData? data = _file.Load();
        if (data is not null)
            OffsetMs = Math.Clamp(data.OffsetMs, -300, 300);
    }

    public void Save() => _file.Save(new OffsetData { OffsetMs = OffsetMs });

    private sealed class OffsetData
    {
        public long OffsetMs { get; set; }
    }
}
