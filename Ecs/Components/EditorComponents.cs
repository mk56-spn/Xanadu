using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Settings;

namespace Writer.Ecs;

/// <summary>The editor's current note selection, including marquee selections.</summary>
public struct NoteSelection(Entity note) : IComponent
{
    private Entity _note = note;
    public readonly List<Entity> Notes = new();

    public Entity Note
    {
        readonly get => _note;
        set
        {
            _note = value;
            Notes.Clear();
            if (!_note.IsNull)
                Notes.Add(_note);
        }
    }

    public readonly bool Contains(Entity entity) =>
        Notes.Count > 0
            ? Notes.Contains(entity)
            : !_note.IsNull && _note.Id == entity.Id;

    public readonly IReadOnlyList<Entity> AllNotes =>
        Notes.Count > 0
            ? Notes
            : !_note.IsNull
                ? new[] { _note }
                : System.Array.Empty<Entity>();

    public void SetMany(IEnumerable<Entity> notes)
    {
        Notes.Clear();
        foreach (Entity entity in notes)
        {
            if (!entity.IsNull && !Notes.Contains(entity))
                Notes.Add(entity);
        }

        _note = Notes.Count > 0 ? Notes[0] : default;
    }

    public void Clear()
    {
        Notes.Clear();
        _note = default;
    }
}

/// <summary>Screen-space marquee currently being dragged in the editor.</summary>
public struct EditorSelectionBox(
    Vector2 start,
    Vector2 current,
    bool active) : IComponent
{
    public Vector2 Start = start;
    public Vector2 Current = current;
    public bool Active = active;
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

/// <summary>Whether clicking the field places mines instead of notes.</summary>
public struct EditorMineMode(bool active) : IComponent
{
    public bool Active = active;
}

/// <summary>Cached waveform peaks for the editor timeline preview.</summary>
public struct WaveformPreview(
    string path,
    float[] left,
    float[] right,
    long durationMs) : IComponent
{
    public string Path = path;
    public float[] Left = left;
    public float[] Right = right;
    public long DurationMs = durationMs;
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
    AddMine,
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

/// <summary>Editor-specific binding for a generic dropdown.</summary>
public struct EditorDropdownControl(EditorDropdownKind kind) : IComponent
{
    public EditorDropdownKind Kind = kind;
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
