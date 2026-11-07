using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

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
    LoadSong,
    Undo,
    Redo,
    BackgroundColor,
    ResetBackgroundColor,
    OutsideColor,
    ResetOutsideColor,
    FrameColor,
    ResetFrameColor,

    /// <summary>The shader dialog asks the overlay for a file to import into its picked slot.</summary>
    ImportShader,

    // Sidebar buttons; their argument is given beside each.
    /// <summary>Picks the placement tool; the argument is an <see cref="EditorPlacementMode"/>.</summary>
    Place,
    /// <summary>Picks the size of new blocks; the argument is a <see cref="BlockBrushSize"/>.</summary>
    BlockSize,
    /// <summary>Opens or closes a tool group's flyout; the argument is its place in the tool strip.</summary>
    ToolGroup,
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
    /// <summary>
    /// Steps a setting of the selected grass, or of the grass brush; the
    /// argument is the <see cref="GrassSetting"/> plus one, negative for less.
    /// </summary>
    GrassStep,
    /// <summary>Moves the selection one layer back or forward; the argument is the step.</summary>
    LayerStep,
    /// <summary>Lowers or raises the selection's z; the argument is the step.</summary>
    ZStep,
    /// <summary>Steps where the selection's shader comes from; the argument is the step.</summary>
    ShaderSourceStep,
    /// <summary>
    /// Steps a setting of the selected flares, or of the flare brush; the
    /// argument is the <see cref="FlareSetting"/> plus one, negative for less.
    /// </summary>
    FlareStep,
    /// <summary>Lines the selection up; the argument is an <see cref="Xanadu.Editor.AlignEdge"/>.</summary>
    Align,
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
