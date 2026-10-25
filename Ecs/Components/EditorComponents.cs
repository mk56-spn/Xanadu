using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Settings;
using Writer.Systems.Ui;

namespace Writer.Ecs;

/// <summary>Marks an editable playfield entity. Editor-only; never serialized.</summary>
public struct EditorItem : ITag;

/// <summary>Editor selection backed by the shared UI selection set.</summary>
public struct NoteSelection : IComponent
{
    private readonly UiSelectionSet _selection = new();

    public NoteSelection(Entity note)
    {
        _selection = new UiSelectionSet();
        Note = note;
    }

    public Entity Note
    {
        readonly get => _selection.Primary;
        set
        {
            if (!value.IsNull && !value.Tags.Has<EditorItem>())
                value.AddTag<EditorItem>();
            _selection.Set(value);
        }
    }

    public readonly IReadOnlyList<Entity> Notes => _selection.All;

    public readonly IReadOnlyList<Entity> AllNotes => _selection.All;

    public readonly bool Contains(Entity entity) => _selection.Contains(entity);

    public void SetMany(IEnumerable<Entity> notes)
    {
        foreach (Entity entity in notes)
        {
            if (!entity.IsNull && !entity.Tags.Has<EditorItem>())
                entity.AddTag<EditorItem>();
        }

        _selection.SetMany(notes);
    }

    public void Toggle(Entity entity)
    {
        if (!entity.IsNull && !entity.Tags.Has<EditorItem>())
            entity.AddTag<EditorItem>();
        _selection.Toggle(entity);
    }

    public void Clear() => _selection.Clear();
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

/// <summary>The editor's currently selected note type for new notes.</summary>
public struct EditorNoteTarget(KeybindAction target) : IComponent
{
    public KeybindAction Target = target;
}

/// <summary>Character selected for newly placed character-switch notes.</summary>
public struct EditorCharacterTarget(CharacterKind target) : IComponent
{
    public CharacterKind Target = target;
}

/// <summary>Whether clicking the field places mines instead of notes.</summary>
public enum EditorPlacementMode
{
    Notes,
    Mines,
    Movement,
    Start,
    Contact,
    CharacterSwitch,
}

public struct EditorPlacementModeState(EditorPlacementMode mode) : IComponent
{
    public EditorPlacementMode Mode = mode;
}

public struct EditorDashDirectionState(DashDirection direction) : IComponent
{
    public DashDirection Direction = direction;
}

public struct EditorPlacementButton(EditorPlacementMode mode) : IComponent
{
    public EditorPlacementMode Mode = mode;
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
    Character,
}

public enum EditorButtonKind
{
    None,
    Play,
    Stop,
    Preview,
    Metronome,
    ZoomIn,
    ZoomOut,
    ToggleMetadata,
    Save,
    SaveAs,
    LoadLevel,
    LoadSong,
    Undo,
    Redo,
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

public enum EditorFieldKind
{
    LevelName,
    Artist,
    Difficulty,
    Bpm,
    Offset,
    StartTime,
    StartMomentumX,
    StartMomentumY,
}

/// <summary>Binds an ECS text field to an editor metadata property.</summary>
public struct EditorField(EditorFieldKind kind) : IComponent
{
    public EditorFieldKind Kind = kind;
}

/// <summary>Editor metadata panel visibility and home position.</summary>
public struct EditorMetadataPanel(Entity panel, Vector2 home) : IComponent
{
    public Entity Panel = panel;
    public Vector2 Home = home;
    public bool Visible;
}

/// <summary>ECS editor HUD text entities updated from editor state.</summary>
public struct EditorHud(
    Entity level,
    Entity song,
    Entity status,
    Entity time,
    Entity notes,
    Entity preview) : IComponent
{
    public Entity Level = level;
    public Entity Song = song;
    public Entity Status = status;
    public Entity Time = time;
    public Entity Notes = notes;
    public Entity Preview = preview;
    public Vector2 PreviewHome = new(0, 24);
    public string StatusText = "";
}

/// <summary>Entities in the editor sidebar that flow around open dropdowns.</summary>
public struct EditorSidebarLayout(
    Entity panel,
    Entity typeLabel,
    Entity typeButton,
    Entity characterLabel,
    Entity characterButton,
    Entity tools,
    float top) : IComponent
{
    public Entity Panel = panel;
    public Entity TypeLabel = typeLabel;
    public Entity TypeButton = typeButton;
    public Entity CharacterLabel = characterLabel;
    public Entity CharacterButton = characterButton;
    public Entity Tools = tools;
    public float Top = top;
}

/// <summary>Left-side menu for snap and level file actions.</summary>
public struct EditorLeftMenu(
    Entity panel,
    Entity snapLabel,
    Entity snapButton,
    float top) : IComponent
{
    public Entity Panel = panel;
    public Entity SnapLabel = snapLabel;
    public Entity SnapButton = snapButton;
    public float Top = top;
}

/// <summary>
/// Binds the generic Writer.UI wheel to the editor's dash direction. The wheel
/// owns presentation and pointer handling; this adapter only maps wedge indices
/// to <see cref="DashDirection"/> and owns the floating widget's visibility.
/// </summary>
public struct EditorDirectionWheel(Entity wheel, Entity label, Vector2 home)
    : IComponent
{
    public Entity Wheel = wheel;
    public Entity Label = label;
    public Vector2 Home = home;
}

/// <summary>F2 editor-binding reference overlay.</summary>
public struct EditorBindingsHelp(Entity panel, Vector2 home) : IComponent
{
    public Entity Panel = panel;
    public Vector2 Home = home;
    public bool Visible;
}

/// <summary>Saved practice start state: time, position, momentum, and facing.</summary>
public struct StartPoint(
    string name,
    long timeMs,
    Vector2 position,
    System.Numerics.Vector2 momentum,
    Vector2 direction) : IComponent
{
    public string Name = name;
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public System.Numerics.Vector2 Momentum = momentum;
    public Vector2 Direction = direction;
}

/// <summary>Separate start-point property panel shown below level metadata.</summary>
public struct EditorStartPanel(Entity panel, Vector2 home) : IComponent
{
    public Entity Panel = panel;
    public Vector2 Home = home;
    public bool Visible;
}
