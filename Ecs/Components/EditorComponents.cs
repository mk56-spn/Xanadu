using Friflo.Engine.ECS;
using Godot;
using Writer.Settings;

namespace Writer.Ecs;

/// <summary>Currently selected editor note.</summary>
public struct NoteSelection(Entity note) : IComponent
{
    public Entity Note = note;
}

/// <summary>Current level file path for Save (empty until saved).</summary>
public struct LevelFile(string path) : IComponent
{
    public string Path = path;
}

/// <summary>Horizontal zoom for the editor timeline.</summary>
public struct EditorTimeline(float zoom) : IComponent
{
    public float Zoom = zoom;
    public bool AutoZoomed;
}

/// <summary>The editor's currently selected note type for new notes.</summary>
public struct EditorNoteTarget(KeybindAction target) : IComponent
{
    public KeybindAction Target = target;
}

/// <summary>Screen-space region occupied by the editor's side controls.</summary>
public struct EditorSidebar(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

public enum EditorDropdownKind
{
    Snap,
    NoteType,
}

public enum EditorButtonKind
{
    None,
    Play,
    Stop,
    Preview,
    Metronome,
    AddMove,
    ZoomIn,
    ZoomOut,
    ToggleMetadata,
    Save,
    SaveAs,
    LoadLevel,
    LoadSong,
}

/// <summary>An ECS editor toolbar button.</summary>
public struct EditorButton(EditorButtonKind kind) : IComponent
{
    public EditorButtonKind Kind = kind;
}

/// <summary>A native editor panel/dialog request raised by an ECS button.</summary>
public struct EditorUiRequest(EditorButtonKind kind = EditorButtonKind.None) : IComponent
{
    public EditorButtonKind Kind = kind;
}

/// <summary>An ECS dropdown button and its option list.</summary>
public struct EditorDropdown(
    Entity list,
    EditorDropdownKind kind,
    int selected) : IComponent
{
    public Entity List = list;
    public EditorDropdownKind Kind = kind;
    public int Selected = selected;
    public bool Open;
}

/// <summary>One selectable option inside an editor dropdown.</summary>
public struct EditorDropdownOption(Entity owner, int value) : IComponent
{
    public Entity Owner = owner;
    public int Value = value;
}

/// <summary>Metronome toggle for the editor.</summary>
public struct EditorMetronome(bool enabled) : IComponent
{
    public bool Enabled = enabled;
}

/// <summary>Editor preview state and the state to restore on exit.</summary>
public struct EditorPreview(bool active, long savedTimeMs, bool savedPlaying) : IComponent
{
    public bool Active = active;
    public long SavedTimeMs = savedTimeMs;
    public bool SavedPlaying = savedPlaying;
}

/// <summary>Pending warning message to show in the editor warning popup.</summary>
public struct WarningRequest(string message) : IComponent
{
    public string Message = message;
}

/// <summary>State and child entities of the ECS warning popup.</summary>
public struct WarningPopup(Entity message, Entity button) : IComponent
{
    public Entity Message = message;
    public Entity Button = button;
    public bool Visible;
}
