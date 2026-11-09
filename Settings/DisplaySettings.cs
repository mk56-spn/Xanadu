using System;
using Godot;
using Xanadu.Ecs.Storage;

namespace Xanadu.Settings;

/// <summary>Persisted general options: vertical sync and editor autosave.</summary>
public sealed class DisplaySettings
{
    private const string DefaultPath = "user://display.json";
    private readonly PersistedFile<DisplayData> _file;
    private readonly bool _apply;

    public DisplaySettings(
        string? path = null,
        bool persist = true,
        bool apply = true)
    {
        _file = new PersistedFile<DisplayData>(path, DefaultPath, persist);
        _apply = apply;
        if (persist)
            Load();
        Apply();
    }

    public bool VSyncEnabled { get; private set; } = true;

    /// <summary>Whether the editor saves a level to its file as it is edited.</summary>
    public bool EditorAutosave { get; private set; } = true;

    public void SetVSync(bool enabled)
    {
        VSyncEnabled = enabled;
        Apply();
        Save();
    }

    public void SetEditorAutosave(bool enabled)
    {
        EditorAutosave = enabled;
        Save();
    }

    public void Load()
    {
        DisplayData? data = _file.Load();
        if (data is null)
            return;

        VSyncEnabled = data.VSyncEnabled;
        EditorAutosave = data.EditorAutosave;
    }

    public void Save() => _file.Save(new DisplayData
    {
        VSyncEnabled = VSyncEnabled,
        EditorAutosave = EditorAutosave,
    });

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

    private sealed class DisplayData
    {
        public bool VSyncEnabled { get; set; } = true;
        public bool EditorAutosave { get; set; } = true;
    }
}
