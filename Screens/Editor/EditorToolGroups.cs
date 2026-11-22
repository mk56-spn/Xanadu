using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;

namespace Xanadu.Screens.Editor;

/// <summary>One placement tool: its mode, its icon (a letter where no symbol fits) and its hover name.</summary>
public readonly record struct EditorTool(EditorPlacementMode Mode, string Icon, string Name);

/// <summary>A tool-strip button: one tool, or several that open in a flyout beside it.</summary>
/// <param name="Icon">The strip button's own icon, whatever tool is out; null shows the current tool's.</param>
public readonly record struct EditorToolGroupInfo(string Name, EditorTool[] Tools, string? Icon = null)
{
    public bool IsGroup => Tools.Length > 1;
}

/// <summary>The tool strip's buttons, top to bottom, and the tools each one holds.</summary>
public static class EditorToolGroups
{
    /// <summary>The strip the editor shows: notes, chords, dashes, the entrance, orbs, pads and updrafts.</summary>
    public static readonly EditorToolGroupInfo[] All =
    {
        new("Notes", new EditorTool[]
        {
            new(EditorPlacementMode.Note, "N", "Notes: click to place one at the snapped playhead, in the key picked; it lands on that key's line of the chord"),
        }),
        new("Chords", new EditorTool[]
        {
            new(EditorPlacementMode.Chord, "C", "Chords: click where the first line meets the judgement line; the chord starts at the snapped playhead and lasts until the next one"),
        }),
        new("Dashes", new EditorTool[]
        {
            new(EditorPlacementMode.Dash, "D", "Dashes: click to place one at the snapped playhead; it crosses the whole chord, and the player dashes whichever way they press"),
            new(EditorPlacementMode.DashStop, "S", "Dash enders: click to place one; a dash that enters it ends there, stopped on its centre, instead of running on to a wall"),
        }),
        new("Entrance", new EditorTool[]
        {
            new(EditorPlacementMode.Entrance, "E", "Entrance: click where the player comes in; it releases them at the snapped playhead, and any key hits it. Still selected, click again to set its exit: at its time, on the rift falls and off to a side dashes that way; later on, it comes out riding an orb, each click a height key"),
        }),
        new("Orbs", new EditorTool[]
        {
            new(EditorPlacementMode.Orb, "O", "Orb holds: click to place one at the snapped playhead, two beats long; with it still selected, move the playhead on and click to add a height key (past the tail, it stretches)"),
        }),
        new("Pads", new EditorTool[]
        {
            new(EditorPlacementMode.Pad, "P", "Pads: click to place one; touching it launches the player upward, and a dash that reaches it ends there and launches"),
            new(EditorPlacementMode.TimedPad, "T", "Timed pads: click to place one firing at the snapped playhead; at that moment it stands the player on it and launches, wherever they fell from"),
        }),
        new("Drafts", new EditorTool[]
        {
            new(EditorPlacementMode.Draft, "W", "Updrafts: click to place an emitter, one fixed width; above it the player is held at its hover height instead of falling. Still selected, click above it to set that height; set several in a row for a long stretch"),
        }),
    };

    /// <summary>The icon the strip shows for a group: its own when it has one, else its current tool's when that is in it, else its first tool's.</summary>
    public static string IconFor(EditorToolGroupInfo group, EditorPlacementMode current)
    {
        if (group.Icon is { } own)
            return own;

        foreach (EditorTool tool in group.Tools)
        {
            if (tool.Mode == current)
                return tool.Icon;
        }

        return group.Tools[0].Icon;
    }

    public static bool Holds(EditorToolGroupInfo group, EditorPlacementMode mode)
    {
        foreach (EditorTool tool in group.Tools)
        {
            if (tool.Mode == mode)
                return true;
        }

        return false;
    }
}

/// <summary>A tool-strip button that opens a flyout of tools; <see cref="Index"/> is its place in <see cref="EditorToolGroups.All"/>.</summary>
public struct EditorToolGroup(int index, Entity flyout, Entity icon) : IComponent
{
    public int Index = index;

    /// <summary>The row of the group's tool buttons, beside the strip.</summary>
    public Entity Flyout = flyout;

    /// <summary>The button's icon text, which follows the group's current tool.</summary>
    public Entity Icon = icon;

    public bool Open;
}

/// <summary>
/// Opens and closes the tool groups' flyouts. A group's button toggles its
/// flyout and closes any other. Picking a tool in an open flyout leaves it
/// open, so its tools can be swapped between; moving to a tool outside the
/// group, from another button or a key, closes it. The button lights up while
/// one of its tools is the current one, and shows that tool's icon.
/// </summary>
public sealed class EditorToolGroupSystem : QuerySystem<EditorToolGroup>
{
    private readonly EntityStore _entities;
    private readonly ButtonPress _presses = new();
    private EditorPlacementMode? _lastMode;

    public EditorToolGroupSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement>(_entities);

        if (editor.IsNull)
            return;

        EditorPlacementMode mode = editor.GetComponent<EditorPlacement>().Mode;
        bool picked = _lastMode is { } last && last != mode;
        _lastMode = mode;
        CommandBuffer buffer = CommandBuffer;
        EntityList groups = Query.ToEntityList();

        foreach (Entity entity in groups)
        {
            if (!_presses.Started(entity))
                continue;

            bool open = !entity.GetComponent<EditorToolGroup>().Open;
            foreach (Entity other in groups)
                other.GetComponent<EditorToolGroup>().Open = false;
            entity.GetComponent<EditorToolGroup>().Open = open;
        }

        foreach (Entity entity in groups)
        {
            ref EditorToolGroup group = ref entity.GetComponent<EditorToolGroup>();
            EditorToolGroupInfo info = EditorToolGroups.All[group.Index];
            if (picked && !EditorToolGroups.Holds(info, mode))
                group.Open = false;

            UiLayout.SetHidden(group.Flyout, buffer, !group.Open);
            EditorButtons.Tint(entity, EditorToolGroups.Holds(info, mode), buffer);
            UiText.Set(group.Icon, EditorToolGroups.IconFor(info, mode), buffer);
        }
    }
}
