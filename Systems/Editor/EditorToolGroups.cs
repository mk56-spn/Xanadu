using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>One placement tool: its mode, its icon (a letter where no symbol fits) and its hover name.</summary>
public readonly record struct EditorTool(EditorPlacementMode Mode, string Icon, string Name);

/// <summary>A tool-strip button: one tool, or several that open in a flyout beside it.</summary>
public readonly record struct EditorToolGroupInfo(string Name, EditorTool[] Tools)
{
    public bool IsGroup => Tools.Length > 1;
}

/// <summary>The tool strip's buttons, top to bottom, and the tools each one holds.</summary>
public static class EditorToolGroups
{
    public static readonly EditorToolGroupInfo[] All =
    {
        new("Notes", new EditorTool[]
        {
            new(EditorPlacementMode.Notes, "🎵", "Normal notes"),
            new(EditorPlacementMode.Pair, "P", "Pair notes"),
            new(EditorPlacementMode.Dash, "➜", "Dash notes"),
            new(EditorPlacementMode.Momentum, "M", "Momentum notes"),
            new(EditorPlacementMode.Contact, "◎", "Contact notes"),
            new(EditorPlacementMode.WeaponSwitch, "W", "Weapon switches"),
            new(EditorPlacementMode.Channel, "◑", "Channel starts"),
            new(EditorPlacementMode.FormStep, "F", "Form steps"),
        }),
        new("Characters and start points", new EditorTool[]
        {
            new(EditorPlacementMode.CharacterSwitch, "⇄", "Character switches"),
            new(EditorPlacementMode.Start, "⚑", "Start points"),
        }),
        new("Blocks", new EditorTool[] { new(EditorPlacementMode.Blocks, "■", "Blocks") }),
        new("Spikes and mines", new EditorTool[]
        {
            new(EditorPlacementMode.Spikes, "▲", "Spikes"),
            new(EditorPlacementMode.Mines, "✹", "Mines"),
        }),
        new("Text", new EditorTool[] { new(EditorPlacementMode.Text, "T", "Text") }),
        new("Grass", new EditorTool[] { new(EditorPlacementMode.Grass, "🌿", "Grass") }),
    };

    /// <summary>The icon the strip shows for a group: its current tool's when that is in it, else its first tool's.</summary>
    public static string IconFor(EditorToolGroupInfo group, EditorPlacementMode current)
    {
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
/// flyout and closes any other; picking a tool, from a flyout or a key,
/// closes them all. The button lights up while one of its tools is the
/// current one, and shows that tool's icon.
/// </summary>
public sealed class EditorToolGroupSystem : QuerySystem<EditorToolGroup>
{
    private readonly EntityStore _entities;
    private readonly HashSet<int> _held = new();
    private EditorPlacementMode? _lastMode;

    public EditorToolGroupSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities.Query<EditorPlacement>().ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        EditorPlacementMode mode = editor.GetComponent<EditorPlacement>().Mode;
        bool picked = _lastMode is { } last && last != mode;
        _lastMode = mode;
        CommandBuffer buffer = CommandBuffer;
        EntityList groups = Query.ToEntityList();

        foreach (Entity entity in groups)
        {
            bool pressed = entity.Tags.Has<Pressed>();
            bool was = !_held.Add(entity.Id);
            if (!pressed)
                _held.Remove(entity.Id);
            if (!pressed || was)
                continue;

            bool open = !entity.GetComponent<EditorToolGroup>().Open;
            foreach (Entity other in groups)
                other.GetComponent<EditorToolGroup>().Open = false;
            entity.GetComponent<EditorToolGroup>().Open = open;
        }

        foreach (Entity entity in groups)
        {
            ref EditorToolGroup group = ref entity.GetComponent<EditorToolGroup>();
            if (picked || PickedIn(group.Flyout))
                group.Open = false;

            EditorToolGroupInfo info = EditorToolGroups.All[group.Index];
            UiLayout.SetHidden(group.Flyout, buffer, !group.Open);
            EditorPlacementSystem.SetColor(entity, EditorToolGroups.Holds(info, mode), buffer);
            UiText.Set(group.Icon, EditorToolGroups.IconFor(info, mode), buffer);
        }
    }

    /// <summary>Whether a tool in the flyout is being pressed, even the current one again.</summary>
    private static bool PickedIn(Entity flyout)
    {
        if (flyout.IsNull)
            return false;

        foreach (Entity child in flyout.ChildEntities)
        {
            if (child.Tags.Has<Pressed>() && child.HasComponent<EditorButton>())
                return true;
        }

        return false;
    }
}
