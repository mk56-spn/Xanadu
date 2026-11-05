using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

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

/// <summary>Current level file path for Save (empty until saved).</summary>
public struct LevelFile(string path) : IComponent
{
    public string Path = path;
}

/// <summary>What clicking the field places: a note kind, or another playfield item.</summary>
public enum EditorPlacementMode
{
    Notes,
    Mines,
    Dash,
    Start,
    Contact,
    CharacterSwitch,
    Blocks,
    Spikes,
    Pair,
    Momentum,
    WeaponSwitch,
    Channel,
    FormStep,
    Text,
}

/// <summary>
/// The editor's placement brush: what clicking the field places, and everything
/// a newly placed item gets.
/// </summary>
public struct EditorPlacement() : IComponent
{
    public EditorPlacementMode Mode = EditorPlacementMode.Notes;

    /// <summary>The note type new notes get.</summary>
    public KeybindAction Note = KeybindAction.M;

    /// <summary>The character new character-switch notes switch to.</summary>
    public CharacterKind Character = CharacterKind.Contact;

    /// <summary>The turn given to new blocks and spikes, in degrees clockwise from up.</summary>
    public float Degrees;

    public DashDirection Direction = DashDirection.UpRight;

    /// <summary>The dash strength new movement notes get.</summary>
    public DashStrength Strength = DashStrength.Single;

    /// <summary>The left and right charge new contact barriers ask for.</summary>
    public int ContactLeft = 1;
    public int ContactRight = 1;

    /// <summary>The hold length new rail notes get, in milliseconds.</summary>
    public long RailHoldMs;

    /// <summary>The Form construction shape new steps are authored into, or 0 for none.</summary>
    public int ShapeId;
}

/// <summary>A part of the left panel that only shows for the tools that use it.</summary>
public struct EditorToolSection(EditorPlacementMode[] modes) : IComponent
{
    public EditorPlacementMode[] Modes = modes;
}

/// <summary>Where the pointer would place the current tool's item this frame, if anywhere.</summary>
public struct EditorPlacementGhost : IComponent
{
    public bool Visible;
    public Vector2 Screen;
}

/// <summary>The song's loudness by frequency band, for the editor timeline; null without a song.</summary>
public struct WaveformPreview(string path, WaveformData? data = null) : IComponent
{
    public string Path = path;
    public WaveformData? Data = data;
}

/// <summary>Screen-space region occupied by the editor's side controls.</summary>
public struct EditorSidebar(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

public enum EditorDropdownKind
{
    Snap,
    Character,
}

public enum EditorButtonKind
{
    None,
    Play,
    Stop,
    Preview,
    PreviewHere,
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
    BackgroundColor,
    ResetBackgroundColor,
    OutsideColor,
    ResetOutsideColor,
    FrameColor,
    ResetFrameColor,

    // Sidebar buttons; their argument is given beside each.
    /// <summary>Picks the placement tool; the argument is an <see cref="EditorPlacementMode"/>.</summary>
    Place,
    /// <summary>A note-type bar swatch; the argument is the <see cref="KeybindAction"/> new notes get.</summary>
    NoteType,
    /// <summary>Picks the strength new movement notes get; the argument is a <see cref="DashStrength"/>.</summary>
    DashStrength,
    /// <summary>Steps the left contact cost; the argument is the step.</summary>
    ContactLeft,
    /// <summary>Steps the right contact cost; the argument is the step.</summary>
    ContactRight,
    /// <summary>Places a rail note at the playhead; the argument is its side <see cref="KeybindAction"/>.</summary>
    RailTap,
    /// <summary>Cycles the hold length new rail notes get.</summary>
    RailHold,
    /// <summary>Places a channel end at the playhead; the argument is its <see cref="ChannelSide"/>.</summary>
    ChannelEnd,
    /// <summary>Attaches the selected blocks to a new Form shape at the playhead.</summary>
    ShapeAttach,
    /// <summary>Makes the selected texts bigger or smaller; the argument is the step.</summary>
    TextSize,
    /// <summary>Thickens or thins the selected outlines; the argument is the step in whole units.</summary>
    OutlineStep,
}

/// <summary>An ECS editor button: what it does, and the value it does it with.</summary>
public struct EditorButton(EditorButtonKind kind, int arg = 0) : IComponent
{
    public EditorButtonKind Kind = kind;

    /// <summary>The kind's value, such as a placement mode, note key or step; see <see cref="EditorButtonKind"/>.</summary>
    public int Arg = arg;

    public static EditorButton Place(EditorPlacementMode mode) => new(EditorButtonKind.Place, (int)mode);

    public static EditorButton NoteType(KeybindAction action) => new(EditorButtonKind.NoteType, (int)action);

    public static EditorButton Strength(DashStrength strength) => new(EditorButtonKind.DashStrength, (int)strength);

    public static EditorButton RailTap(KeybindAction action) => new(EditorButtonKind.RailTap, (int)action);

    public static EditorButton ChannelEnd(ChannelSide side) => new(EditorButtonKind.ChannelEnd, (int)side);

    /// <summary>The editor buttons of one kind, hidden ones included.</summary>
    public static List<Entity> All(EntityStore store, EditorButtonKind kind)
    {
        var found = new List<Entity>();
        foreach (Entity entity in store.Query<EditorButton>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorButton>().Kind == kind)
                found.Add(entity);
        }

        return found;
    }
}

public enum EditorReadoutKind
{
    ContactCost,
    RailHold,
    Shape,
}

/// <summary>A text that shows one of the placement brush's values.</summary>
public struct EditorReadout(EditorReadoutKind kind) : IComponent
{
    public EditorReadoutKind Kind = kind;

    /// <summary>Sets every readout of a kind to a value, marking the ones that changed.</summary>
    public static void Show(EntityStore store, EditorReadoutKind kind, string value, CommandBuffer buffer)
    {
        foreach (Entity entity in store.Query<EditorReadout, Text>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorReadout>().Kind != kind)
                continue;

            ref Text text = ref entity.GetComponent<Text>();
            if (text.Value == value)
                continue;

            text.Value = value;
            buffer.AddTag<Dirty>(entity.Id);
        }
    }
}

public enum EditorColorTarget
{
    Background,
    Outside,
    Frame,
}

/// <summary>
/// Part of a metadata colour row: the swatch that shows, and opens, one of the
/// map's colours, or the text beside it with the stored colour or DEFAULT.
/// </summary>
public struct EditorColorRow(EditorColorTarget target, bool hex) : IComponent
{
    public EditorColorTarget Target = target;
    public bool Hex = hex;
}

public enum EditorPanelKind
{
    /// <summary>The level metadata panel; it starts hidden.</summary>
    Metadata,
    /// <summary>The start-point property panel below the metadata.</summary>
    Start,
    /// <summary>The F2 editor-binding reference overlay.</summary>
    Bindings,
}

/// <summary>A floating editor panel, on the panel itself, with the place it opens at.</summary>
public struct EditorPanel(EditorPanelKind kind, Vector2 home) : IComponent
{
    public EditorPanelKind Kind = kind;
    public Vector2 Home = home;

    /// <summary>The panel of a kind, or null when there is none.</summary>
    public static Entity Find(EntityStore store, EditorPanelKind kind)
    {
        foreach (Entity entity in store.Query<EditorPanel>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorPanel>().Kind == kind)
                return entity;
        }

        return default;
    }
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

/// <summary>ECS editor HUD text entities updated from editor state.</summary>
public struct EditorHud(
    Entity level,
    Entity song,
    ShellStatusLabels status,
    Entity time,
    Entity notes) : IComponent
{
    public Entity Level = level;
    public Entity Song = song;

    /// <summary>The header's status message and unsaved flag, shared with the animator.</summary>
    public ShellStatusLabels Status = status;
    public Entity Time = time;
    public Entity Notes = notes;
    public string StatusText = "";
}

/// <summary>Entities in the editor sidebar that flow around open dropdowns.</summary>
public struct EditorSidebarLayout(
    Entity panel,
    Entity characterLabel,
    Entity characterButton) : IComponent
{
    public Entity Panel = panel;
    public Entity CharacterLabel = characterLabel;
    public Entity CharacterButton = characterButton;
}

/// <summary>
/// The note-key bar inside the left panel. It shows while placing notes or dash
/// notes, the two kinds that carry a key, and leaves the layout otherwise.
/// </summary>
public struct EditorNoteBarPanel : IComponent;

/// <summary>Left-side menu for snap and level file actions.</summary>
public struct EditorLeftMenu(
    Entity panel,
    Entity snapLabel,
    Entity snapButton) : IComponent
{
    public Entity Panel = panel;
    public Entity SnapLabel = snapLabel;
    public Entity SnapButton = snapButton;
}

/// <summary>
/// Binds the generic Xanadu.UI wheel to the editor's dash direction. The wheel
/// owns presentation and pointer handling; this adapter only maps wedge indices
/// to <see cref="DashDirection"/> and owns the floating widget's visibility.
/// </summary>
public struct EditorDirectionWheel(Entity wheel, Entity label)
    : IComponent
{
    public Entity Wheel = wheel;
    public Entity Label = label;
}

/// <summary>Saved practice start state: time, position, momentum, and facing.</summary>
public struct StartPoint(
    string name,
    long timeMs,
    Vector2 position,
    System.Numerics.Vector2 momentum,
    Vector2 direction,
    uint groups = 0) : IComponent
{
    public string Name = name;
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public System.Numerics.Vector2 Momentum = momentum;
    public Vector2 Direction = direction;

    /// <summary>Groups this start point belongs to, copied so a play launch can offset the spawn.</summary>
    public uint Groups = groups;
}

/// <summary>
/// Moving the editor's view of the playfield. Input asks for a zoom or a fit
/// here; <c>EditorViewSystem</c> applies it to the <see cref="PlayfieldView"/>,
/// pans while the middle button is held, and reopens the view when the field
/// changes size (a level with a different field was loaded).
/// </summary>
public struct EditorViewNavigation(Vector2 fieldSize) : IComponent
{
    /// <summary>The field size the view was opened for.</summary>
    public Vector2 FieldSize = fieldSize;

    /// <summary>Zoom notches asked for since the last frame, positive in and negative out, about the pointer.</summary>
    public int ZoomSteps;

    /// <summary>Asks for the whole field to be fitted in view.</summary>
    public bool FitRequested;

    /// <summary>Where the pointer was last frame while panning, or null when not panning.</summary>
    public Vector2? PanFrom;
}

/// <summary>Where the player's hitbox was at one moment of an editor preview, in field units.</summary>
/// <param name="Position">The hitbox's centre.</param>
/// <param name="TimeMs">The song time.</param>
/// <param name="Jump">Whether it got here instantly, by a dash or a wrap, rather than by moving.</param>
/// <param name="Hit">Whether a note was hit on this frame.</param>
public readonly record struct PlayerPathSample(Vector2 Position, long TimeMs, bool Jump, bool Hit = false);

/// <summary>Records the player's hitbox during an editor preview, for the editor to show afterwards.</summary>
public struct PlayerPathRecorder() : IComponent
{
    public List<PlayerPathSample> Samples = new();

    /// <summary>The score's hit count at the last look, to spot the frame a note is hit.</summary>
    public int NotesHit;
}

/// <summary>The path the player's hitbox took in the last preview, shown over the editor's field.</summary>
public struct PlayerPath(List<PlayerPathSample> samples, Vector2 hitboxSize) : IComponent
{
    public List<PlayerPathSample> Samples = samples;

    /// <summary>The hitbox's size in field units.</summary>
    public Vector2 HitboxSize = hitboxSize;
}

/// <summary>Marks the header button that shows or hides the preview's path.</summary>
public struct PlayerPathToggle : ITag;

/// <summary>
/// The switch for the preview's path. The header button's <see cref="Toggle"/>
/// is the state; it starts on, so coming back from a preview always shows the
/// path just made.
/// </summary>
public static class PlayerPathSwitch
{
    public const string Label = "PATH";
    public const float ToggleWidth = 64f;

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, true));
        button.AddTag<PlayerPathToggle>();
        button.AddComponent(new UiTooltip("Show or hide the player's path from the last preview"));
        return button;
    }

    /// <summary>Whether to draw the path; an editor with no button draws it.</summary>
    public static bool IsVisible(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<PlayerPathToggle>()).Entities)
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }
}
