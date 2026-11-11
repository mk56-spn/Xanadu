using System;
using Xanadu.Ecs.Storage;
using Xanadu.Systems;

namespace Xanadu.Settings;

/// <summary>Persisted general options: vertical sync, window mode and editor autosave.</summary>
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

    public WindowMode WindowMode { get; private set; } = WindowMode.Windowed;

    /// <summary>Whether the editor saves a level to its file as it is edited.</summary>
    public bool EditorAutosave { get; private set; } = true;

    public void SetVSync(bool enabled)
    {
        VSyncEnabled = enabled;
        Apply();
        Save();
    }

    public void SetWindowMode(WindowMode mode)
    {
        WindowMode = mode;
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
        WindowMode = Enum.IsDefined(data.WindowMode) ? data.WindowMode : WindowMode.Windowed;
        EditorAutosave = data.EditorAutosave;
    }

    public void Save() => _file.Save(new DisplayData
    {
        VSyncEnabled = VSyncEnabled,
        WindowMode = WindowMode,
        EditorAutosave = EditorAutosave,
    });

    public void Apply()
    {
        if (!_apply)
            return;

        try
        {
            HostPlatform.ApplyVSync(VSyncEnabled);
            HostPlatform.ApplyWindowMode(WindowMode);
        }
        catch (Exception)
        {
            // Headless test and export contexts may not expose a window.
        }
    }

    private sealed class DisplayData
    {
        public bool VSyncEnabled { get; set; } = true;
        public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
        public bool EditorAutosave { get; set; } = true;
    }
}
