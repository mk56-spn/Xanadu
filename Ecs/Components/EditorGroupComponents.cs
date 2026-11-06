using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>One cell of the groups grid in the editor's sidebar.</summary>
public struct EditorGroupCell(int index) : IComponent
{
    public int Index = index;
}

/// <summary>
/// The group bar pinned at the top centre of the editor view: which group it
/// points at (a label, then − and +) and EDIT, which opens that group's panel.
/// </summary>
public struct EditorGroupBar(Entity label, Entity previous, Entity next, Entity edit) : IComponent
{
    public Entity Label = label;
    public Entity Previous = previous;
    public Entity Next = next;
    public Entity Edit = edit;
}

/// <summary>The groups grid in the sidebar: each cell puts the selection into its group, or takes it out.</summary>
public struct EditorGroupGrid(Entity label, Entity[] cells) : IComponent
{
    public Entity Label = label;
    public Entity[] Cells = cells;
}

/// <summary>What the group overlay is showing, on the editor entity.</summary>
public struct EditorGroupState : IComponent
{
    /// <summary>The group being edited, or -1 while the overlay is closed.</summary>
    public int Open;

    /// <summary>The group the top bar points at; EDIT opens it, and stepping switches an open panel to it.</summary>
    public int BarGroup;

    /// <summary>Times typed or dragged in the overlay snap to the beat grid.</summary>
    public bool Snap;

    /// <summary>The first second the overlay's timeline shows, and how many it spans.</summary>
    public float ViewStart;
    public float ViewSpan;

    /// <summary>The group and effect the overlay's selection bar acts on.</summary>
    public int SelectedGroup;
    public int SelectedEffect;

    /// <summary>While set, the selected move effect's offset is dragged on the playfield.</summary>
    public bool TargetEdit;

    /// <summary>The group soloed in the editor: the only one applied to the live items, or -1 for all groups.</summary>
    public int SoloGroup;

    /// <summary>Whether the colour palette is showing over the overlay.</summary>
    public bool PaletteOpen;

    /// <summary>The effect row being dragged on the timeline, or -1.</summary>
    public int DragRow;

    /// <summary>The instance uniform, and the component of it, the shader track edits.</summary>
    public int ShaderParam;
    public int ShaderChannel;

    /// <summary>Whether the shader row edits the item-shader track or the buffer track (its BUFFER tab).</summary>
    public bool BufferTrack;

    /// <summary>Whether the open palette sets the buffer's tint rather than the selected colour effect.</summary>
    public bool PaletteForBuffer;

    /// <summary>What a drag moves: the whole effect, or just its start or end.</summary>
    public EditorGroupDragMode DragMode;

    /// <summary>Seconds from the effect's start to where the pointer grabbed it, for a whole-effect drag.</summary>
    public float DragGrab;

    /// <summary>
    /// The moment of the selected key on the key lane (of the selected setting
    /// on the track being edited), or -1 for none. A key is found by its moment,
    /// so this follows it through retiming.
    /// </summary>
    public float SelectedKey;

    /// <summary>
    /// Whether the group panel is open on this editor. The panel is see-through
    /// and docked over the top of the view, so the panels that float there hide
    /// while it is open.
    /// </summary>
    public static bool IsOpen(Entity editor) =>
        !editor.IsNull && editor.TryGetComponent(out EditorGroupState state) && state.Open >= 0;

    public static EditorGroupState Closed => new()
    {
        Open = -1,
        Snap = true,
        ViewStart = 0f,
        ViewSpan = 6f,
        SelectedGroup = -1,
        SelectedEffect = -1,
        SoloGroup = -1,
        DragRow = -1,
        SelectedKey = -1f,
    };
}

public enum EditorGroupDragMode
{
    None,
    Whole,
    Start,
    End,
    Scrub,

    /// <summary>Retiming the selected key on the key lane.</summary>
    Key,
}

/// <summary>The pieces of the group overlay, so one system can find and place them.</summary>
public enum EditorGroupPart
{
    Scrim,
    Dialog,
    Title,
    NameField,
    Close,
    MemberText,
    AddSelected,
    InteriorLink,
    OutlineLink,
    RemoveSelected,
    SelectMembers,
    SnapToggle,
    AddMove,
    AddColor,
    SoloToggle,
    PlayToggle,
    ScrollLeft,
    ScrollRight,
    ZoomIn,
    ZoomOut,
    Hint,

    /// <summary>The selected-effect bar under the toolbar.</summary>
    SelectionText,
    SelectionTarget,
    SelectionDuplicate,
    SelectionMute,
    SelectionNudgeLeft,
    SelectionNudgeRight,
    SelectionPick,

    /// <summary>The timeline's playhead marker.</summary>
    PlayheadLine,

    /// <summary>Whether the group's members draw into one buffer (D12).</summary>
    BufferToggle,

    /// <summary>The group's item-shader track row: what it is and its controls.</summary>
    ShaderText,
    ShaderSlot,
    ShaderParam,
    ShaderChannel,
    ShaderDown,
    ShaderValue,
    ShaderUp,
    ShaderClear,
    /// <summary>The two tabs of the shader row: the group's item shader, or its buffer.</summary>
    TrackItem,
    TrackBuffer,
    BufferTint,
    BufferAlphaText,
    BufferAlphaDown,
    BufferAlphaUp,
    BufferBlend,

    /// <summary>The colour palette popover.</summary>
    PalettePanel,
    PaletteTitle,
    PaletteClose,
    PaletteSwatch,
    PaletteAlpha,

    /// <summary>Per-row pieces.</summary>
    RowIndex,
    RowKind,
    RowStart,
    RowEnd,
    RowA,
    RowB,
    RowEase,
    RowDelete,

    /// <summary>
    /// The key lane under the effect rows: the selected setting's name, and the
    /// selected key's moment, value, easing and delete.
    /// </summary>
    KeyName,
    KeyTime,
    KeyValue,
    KeyEase,
    KeyDelete,

    /// <summary>Pooled pieces of the timeline's ruler.</summary>
    TickLine,
    TickLabel,
}

/// <summary>Marks an entity as a piece of the group overlay; <see cref="Row"/> is the effect row for row pieces and the pool slot for ticks.</summary>
public struct EditorGroupPiece(EditorGroupPart part, int row = 0) : IComponent
{
    public EditorGroupPart Part = part;
    public int Row = row;
}

/// <summary>Marks the view bar button that shows or hides the group tags on items.</summary>
public struct GroupTagsToggle : ITag;

/// <summary>
/// The switch for the group tags drawn beside items (<c>EditorGroupTagsDrawer</c>).
/// The bar button's <see cref="Toggle"/> is the state, and it
/// starts off: tags only show while it's on.
/// </summary>
public static class GroupTagsSwitch
{
    public const string Label = "TAGS";

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, false));
        button.AddTag<GroupTagsToggle>();
        button.AddComponent(new UiTooltip("Show each item's groups beside it, as G1, G2 …"));
        return button;
    }

    /// <summary>Whether to draw the tags; an editor with no button draws none.</summary>
    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<GroupTagsToggle>()).Entities)
            return toggle.GetComponent<Toggle>().Value;

        return false;
    }
}
