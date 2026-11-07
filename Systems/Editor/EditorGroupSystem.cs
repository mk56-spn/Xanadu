using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The editor's groups: the sidebar grid that puts the selection into groups,
/// the bar at the top of the view that opens one, and the overlay
/// screen where its members and timed effects are set. Effects are rows in a
/// table and bars on a timeline that snaps to the beat grid; every change is
/// one undo step.
/// </summary>
public sealed partial class EditorGroupSystem : QuerySystem<EditorGroupState>
{
    private static readonly Color MoveColor = Palette.Info;
    private static readonly Color DefaultTint = Palette.Danger;

    /// <summary>The overlay's buttons: muted blue at rest, bright blue only while on, slate when off or unusable.</summary>
    private static readonly Color ActionIdle = Palette.PrimaryMuted;
    private static readonly Color ToggleOn = Palette.Primary;
    private static readonly Color Unavailable = SceneColors.Muted;

    private const float EdgeGrab = 7f;

    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly IPointerInputSource _pointer;
    private readonly EditorCommandBus _commands;
    private readonly ButtonPress _presses = new();
    private readonly HashSet<int> _focused = new();
    private bool _pointerHeld;
    private EditorSnapshot _dragBefore;
    private bool _keyDragMoved;
    private Dictionary<(EditorGroupPart, int), Entity> _pieces = new();

    public EditorGroupSystem(
        EntityStore entities,
        EditorHistory history,
        IPointerInputSource pointer,
        EditorCommandBus commands)
    {
        _entities = entities;
        _history = history;
        _pointer = pointer;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        CommandBuffer buffer = CommandBuffer;
        ref EditorGroupState state = ref editor.GetComponent<EditorGroupState>();
        LevelGroups levelGroups = editor.GetComponent<LevelGroups>();
        Playhead playhead = editor.GetComponent<Playhead>();
        Vector2 screen = editor.GetComponent<PlayfieldView>().ScreenSize;
        CollectPieces();

        UpdateGrid(editor, levelGroups, buffer);
        UpdateBar(editor, ref state, levelGroups, buffer);

        if (state.Open < 0 || state.Open >= levelGroups.Groups.Length)
        {
            state.Open = -1;
            state.DragRow = -1;
            HideOverlay(buffer);
            _pointerHeld = _pointer.Read().Pressed;
            return;
        }

        GroupDefinition group = levelGroups.Groups[state.Open];
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Vector2 origin = EditorGroupLayout.Origin(screen);

        HandleButtons(editor, ref state, group, playhead);
        if (state.Open < 0)
        {
            HideOverlay(buffer);
            return;
        }

        HandlePointer(editor, ref state, group, ref playhead, dialog, origin);

        // Before showing: a field that just lost focus is refilled from the
        // data once it shows, so its typed text has to be read first.
        CommitFields(group, state, playhead);
        CommitKeyFields(editor, ref state, group, playhead);
        CommitCycleFields(group, state, playhead);
        ShowOverlay(editor, state, group, playhead, dialog, origin, buffer);
    }

    // ---------------------------------------------------------------- finding

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities
                     .Query<EditorGroupState, LevelGroups, Playhead, PlayfieldView>()
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }

    private void CollectPieces()
    {
        _pieces = new Dictionary<(EditorGroupPart, int), Entity>();
        foreach (Entity entity in _entities
                     .Query<EditorGroupPiece>()
                     .WithDisabled()
                     .ToEntityList())
        {
            EditorGroupPiece piece = entity.GetComponent<EditorGroupPiece>();
            _pieces[(piece.Part, piece.Row)] = entity;
        }
    }

    private Entity Piece(EditorGroupPart part, int row = 0) =>
        _pieces.TryGetValue((part, row), out Entity entity) ? entity : default;

    private bool JustPressed(Entity entity) =>
        !entity.IsNull && _presses.Started(entity);

    // -------------------------------------------------------------------- grid

    /// <summary>
    /// The sidebar grid assigns groups directly: a cell puts every selected item
    /// into its group, or takes them all out when they're all in it already.
    /// A cell is bright blue when the whole selection is in it, teal when part
    /// of it is, muted blue when the level uses the group and slate when empty.
    /// </summary>
    private void UpdateGrid(Entity editor, LevelGroups levelGroups, CommandBuffer buffer)
    {
        if (!editor.HasComponent<EditorGroupGrid>())
            return;

        EditorGroupGrid grid = editor.GetComponent<EditorGroupGrid>();
        foreach (Entity cell in grid.Cells)
        {
            if (cell.IsNull || !cell.HasComponent<EditorGroupCell>())
                continue;

            int index = cell.GetComponent<EditorGroupCell>().Index;
            (int inGroup, int groupable) = Coverage(editor, index);
            if (JustPressed(cell) && groupable > 0)
            {
                bool add = inGroup < groupable;
                Membership(editor, index, add);
                inGroup = add ? groupable : 0;
            }

            Color color = groupable > 0 && inGroup == groupable ? ToggleOn
                : inGroup > 0 ? Palette.Selected
                : !levelGroups.Groups[index].IsEmpty ? ActionIdle
                : Unavailable;
            Tint(cell, color, buffer);
        }
    }

    /// <summary>How many selected items can join a group, and how many of them are in it.</summary>
    private static (int InGroup, int Groupable) Coverage(Entity editor, int group)
    {
        int inGroup = 0;
        int groupable = 0;
        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!IsGroupable(item))
                continue;
            groupable++;
            if (item.TryGetComponent(out ItemGroups mask) && mask.Has(group))
                inGroup++;
        }

        return (inGroup, groupable);
    }

    /// <summary>
    /// The bar at the top centre of the view: − and + step the group it points
    /// at (switching an open panel along with it) and EDIT opens or closes that
    /// group's panel, lit while it's open.
    /// </summary>
    private void UpdateBar(Entity editor, ref EditorGroupState state, LevelGroups levelGroups, CommandBuffer buffer)
    {
        if (!editor.HasComponent<EditorGroupBar>())
            return;

        EditorGroupBar bar = editor.GetComponent<EditorGroupBar>();
        if (state.Open >= 0)
            state.BarGroup = state.Open;

        int step = (JustPressed(bar.Previous) ? -1 : 0) + (JustPressed(bar.Next) ? 1 : 0);
        if (step != 0)
        {
            state.BarGroup = Math.Clamp(state.BarGroup + step, 0, GroupLimits.Count - 1);
            if (state.Open >= 0)
                state = Reopen(state, state.BarGroup);
        }

        if (JustPressed(bar.Edit))
        {
            if (state.Open >= 0)
            {
                state.Open = -1;
                state.PaletteOpen = false;
                state.DragRow = -1;
            }
            else
            {
                state = Reopen(state, state.BarGroup);
            }
        }

        string name = levelGroups.Groups[state.BarGroup].Name;
        if (name.Length > 10)
            name = name[..10];
        UiText.Set(bar.Label, name.Length > 0 ? $"GROUP {state.BarGroup + 1} · {name}" : $"GROUP {state.BarGroup + 1}", buffer);
        Tint(bar.Previous, state.BarGroup > 0 ? ActionIdle : Unavailable, buffer);
        Tint(bar.Next, state.BarGroup < GroupLimits.Count - 1 ? ActionIdle : Unavailable, buffer);
        Tint(bar.Edit, state.Open >= 0 ? ToggleOn : ActionIdle, buffer);
    }

    private static EditorGroupState Reopen(EditorGroupState state, int index)
    {
        if (state.SelectedGroup != index)
        {
            state.SelectedGroup = index;
            state.SelectedEffect = -1;
            state.SoloGroup = -1;
        }

        state.Open = index;
        state.TargetEdit = false;
        state.PaletteOpen = false;
        state.DragRow = -1;
        return state;
    }

    // ------------------------------------------------------------------ buttons

    private static GroupEffect Toggled(GroupEffect effect) =>
        effect.Kind == GroupEffectKind.Move
            ? effect with { Kind = GroupEffectKind.Color, Tint = DefaultTint }
            : effect with { Kind = GroupEffectKind.Move, Offset = new Vector2(0, -80) };

    /// <summary>The short easing name shown in a row's EASE button.</summary>
    public static string EaseLabel(GroupEase ease) => ease switch
    {
        GroupEase.In => "IN",
        GroupEase.Out => "OUT",
        GroupEase.InOut => "IN OUT",
        _ => "LIN",
    };

    /// <summary>Runs a change as one undo step.</summary>
    private void Edit(Action change)
    {
        EditorSnapshot before = _history.Capture();
        change();
        _history.Record(before, _history.Capture());
    }

    /// <summary>Whether an item can be in groups: every placed kind is.</summary>
    private static bool IsGroupable(Entity item) =>
        !item.IsNull && (item.HasComponent<Note>() ||
                         item.HasComponent<Mine>() ||
                         item.HasComponent<Block>() ||
                         item.HasComponent<Spike>() ||
                         item.HasComponent<LevelText>() ||
                         item.HasComponent<GrassPatch>() ||
                         item.HasComponent<LevelPolygon>() ||
                         item.HasComponent<StartPoint>() ||
                         item.HasComponent<ScreenReadRegion>());

    /// <summary>
    /// Adds or removes the selection as one undo step, refusing an add that
    /// would break a locked buffer with the reason as a warning (D16a).
    /// </summary>
    private void Membership(Entity editor, int group, bool add)
    {
        string? refused = add
            ? EditorBufferGuard.AddingToGroup(
                editor.Store,
                editor.GetComponent<NoteSelection>().AllNotes,
                group)
            : null;
        if (refused is { } problem)
        {
            CommandBuffer.AddComponent(editor.Id, new WarningRequest(problem));
            return;
        }

        Edit(() => SetMembership(editor, group, add));
    }

    /// <summary>
    /// Puts the selected notes, mines, blocks, spikes and start points into the
    /// group, or takes them out. A block joining a group follows its colour with
    /// both its interior and its outline until told otherwise.
    /// </summary>
    private static void SetMembership(Entity editor, int group, bool add)
    {
        uint bit = ItemGroups.Bit(group);
        foreach (Entity item in new List<Entity>(editor.GetComponent<NoteSelection>().AllNotes))
        {
            if (!IsGroupable(item))
                continue;

            uint mask = item.TryGetComponent(out ItemGroups current) ? current.Mask : 0u;
            uint next = add ? mask | bit : mask & ~bit;
            if (next == mask)
                continue;

            if (add && item.HasComponent<Block>())
            {
                ref Block block = ref item.GetComponent<Block>();
                block.FillGroups |= bit;
                block.OutlineGroups |= bit;
            }

            // An item leaving the group its shader came from goes back to its own (step 8).
            if (!add && ItemShaderSource.Of(item) == group)
                ItemShaderSource.Put(item, ItemShaderSource.Own);

            if (next == 0)
                item.RemoveComponent<ItemGroups>();
            else if (item.HasComponent<ItemGroups>())
                item.GetComponent<ItemGroups>().Mask = next;
            else
                item.AddComponent(new ItemGroups(next));

            if (item.HasComponent<StartPoint>())
                item.GetComponent<StartPoint>().Groups = next;
        }
    }

    private void SelectMembers(Entity editor, int group)
    {
        var members = new List<Entity>();
        foreach (Entity item in _entities.Query<ItemGroups>().ToEntityList())
        {
            if (item.GetComponent<ItemGroups>().Has(group))
                members.Add(item);
        }

        editor.GetComponent<NoteSelection>().SetMany(members);
    }

    private int CountMembers(int group)
    {
        int count = 0;
        foreach (Entity item in _entities.Query<ItemGroups>().ToEntityList())
        {
            if (item.GetComponent<ItemGroups>().Has(group))
                count++;
        }

        return count;
    }
}

/// <summary>The editor's muted button colour, named once for the systems that tint with it.</summary>
internal static class SceneColors
{
    public static readonly Color Muted = Palette.Muted;
}
