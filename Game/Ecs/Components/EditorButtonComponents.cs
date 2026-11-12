using System.Collections.Generic;
using Friflo.Engine.ECS;
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

    /// <summary>The song picker asks the overlay to leave the editor, as Escape on the field does.</summary>
    Leave,

    /// <summary>The autosave asks the overlay to save the level to its file without a word.</summary>
    QuietSave,

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
    /// <summary>
    /// Steps one bar of the contact cost; the argument is the bar plus one,
    /// negative for less.
    /// </summary>
    ContactCost,
    /// <summary>Turns the rhombus (45° clockwise) on or off for new directional notes.</summary>
    Diagonal,
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

/// <summary>
/// Editor buttons: a <see cref="UiCommand"/> whose id is an <see cref="EditorButtonKind"/>
/// and whose <see cref="UiCommand.Arg"/> is the value it acts with, such as a
/// placement mode, note key or step.
/// </summary>
public static class EditorButton
{
    public static UiCommand Of(EditorButtonKind kind, int arg = 0) => UiCommand.Of(kind, arg);

    public static UiCommand Place(EditorPlacementMode mode) => Of(EditorButtonKind.Place, (int)mode);

    public static UiCommand NoteType(KeybindAction action) => Of(EditorButtonKind.NoteType, (int)action);

    public static UiCommand Strength(DashStrength strength) => Of(EditorButtonKind.DashStrength, (int)strength);

    /// <summary>Steps <paramref name="bar"/>'s contact cost by <paramref name="step"/> (+1 or -1).</summary>
    public static UiCommand ContactCost(int bar, int step) => Of(EditorButtonKind.ContactCost, (bar + 1) * step);

    /// <summary>The editor buttons of one kind, hidden ones included.</summary>
    public static List<Entity> All(EntityStore store, EditorButtonKind kind)
    {
        var found = new List<Entity>();
        foreach ((Entity entity, EditorButtonKind each, _) in UiCommand.All<EditorButtonKind>(store))
        {
            if (each == kind)
                found.Add(entity);
        }

        return found;
    }
}

public enum EditorReadoutKind
{
    /// <summary>The first, second and third bar's cost for new barriers.</summary>
    ContactFirst,
    ContactSecond,
    ContactThird,
    /// <summary>Whether new directional notes are rhombuses.</summary>
    Diagonal,
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
