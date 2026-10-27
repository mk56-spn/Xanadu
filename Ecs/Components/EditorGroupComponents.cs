using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>One cell of the groups grid in the editor's sidebar.</summary>
public struct EditorGroupCell(int index) : IComponent
{
    public int Index = index;
}

/// <summary>Where the groups grid sits in the sidebar, so its cells can follow the open dropdown above it.</summary>
public struct EditorGroupGrid(Entity label, Entity[] cells, Entity preview) : IComponent
{
    public Entity Label = label;
    public Entity[] Cells = cells;
    public Entity Preview = preview;
}

/// <summary>What the group overlay is showing, on the editor entity.</summary>
public struct EditorGroupState : IComponent
{
    /// <summary>The group being edited, or -1 while the overlay is closed.</summary>
    public int Open;

    /// <summary>Times typed or dragged in the overlay snap to the beat grid.</summary>
    public bool Snap;

    /// <summary>The first second the overlay's timeline shows, and how many it spans.</summary>
    public float ViewStart;
    public float ViewSpan;

    /// <summary>Whether the editor draws ghosts of where the groups put their members.</summary>
    public bool Preview;

    /// <summary>The group and effect the overlay's selection bar acts on.</summary>
    public int SelectedGroup;
    public int SelectedEffect;

    /// <summary>While set, the selected move effect's offset is dragged on the playfield.</summary>
    public bool TargetEdit;

    /// <summary>The group soloed in the editor preview, or -1 for all groups.</summary>
    public int SoloGroup;

    /// <summary>Whether the colour palette is showing over the overlay.</summary>
    public bool PaletteOpen;

    /// <summary>The effect row being dragged on the timeline, or -1.</summary>
    public int DragRow;

    /// <summary>What a drag moves: the whole effect, or just its start or end.</summary>
    public EditorGroupDragMode DragMode;

    /// <summary>Seconds from the effect's start to where the pointer grabbed it, for a whole-effect drag.</summary>
    public float DragGrab;

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
    };
}

public enum EditorGroupDragMode
{
    None,
    Whole,
    Start,
    End,
    Scrub,
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

    /// <summary>The live preview panel and its playhead marker.</summary>
    PreviewPanel,
    PlayheadLine,

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
    RowTrack,
    RowBar,

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
