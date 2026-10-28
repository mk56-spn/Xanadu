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
/// The editor's groups: the sidebar grid that opens a group, and the overlay
/// screen where its members and timed effects are set. Effects are rows in a
/// table and bars on a timeline that snaps to the beat grid; every change is
/// one undo step.
/// </summary>
public sealed partial class EditorGroupSystem : QuerySystem<EditorGroupState>
{
    private static readonly Color MoveColor = Palette.Info;
    private static readonly Color DefaultTint = Palette.Danger;
    private static readonly Color CellEmpty = SceneColors.Muted;
    private static readonly Color CellUsed = Palette.PrimaryDeep;
    private static readonly Color CellSelected = Palette.Accent;
    private static readonly Color CellOpen = Palette.Cyan;

    private const float EdgeGrab = 7f;

    private readonly EntityStore _entities;
    private readonly EditorHistory _history;
    private readonly IPointerInputSource _pointer;
    private readonly EditorCommandBus _commands;
    private readonly HashSet<int> _held = new();
    private readonly HashSet<int> _focused = new();
    private bool _pointerHeld;
    private EditorSnapshot _dragBefore;
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

        HashSet<int> selectedGroups = GroupsOfSelection(editor);
        UpdateGrid(editor, state, levelGroups, selectedGroups, buffer);

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
        Vector2 origin = (screen - dialog) / 2;

        HandleButtons(editor, ref state, group, playhead);
        if (state.Open < 0)
        {
            HideOverlay(buffer);
            return;
        }

        HandlePointer(ref state, group, ref playhead, dialog, origin);
        ShowOverlay(editor, state, group, playhead, dialog, origin, buffer);
        CommitFields(group, state, playhead);
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

    private bool JustPressed(Entity entity)
    {
        if (entity.IsNull)
            return false;

        bool pressed = entity.Tags.Has<Pressed>();
        bool was = _held.Contains(entity.Id);
        if (pressed)
            _held.Add(entity.Id);
        else
            _held.Remove(entity.Id);
        return pressed && !was;
    }

    // -------------------------------------------------------------------- grid

    /// <summary>The groups the selected items belong to, so their cells can be marked.</summary>
    private static HashSet<int> GroupsOfSelection(Entity editor)
    {
        var groups = new HashSet<int>();
        if (!editor.HasComponent<NoteSelection>())
            return groups;

        foreach (Entity item in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (item.IsNull || !item.TryGetComponent(out ItemGroups mask))
                continue;
            foreach (int group in ItemGroups.ToList(mask.Mask))
                groups.Add(group);
        }

        return groups;
    }

    private void UpdateGrid(
        Entity editor,
        EditorGroupState state,
        LevelGroups levelGroups,
        HashSet<int> selectedGroups,
        CommandBuffer buffer)
    {
        if (!editor.HasComponent<EditorGroupGrid>())
            return;

        EditorGroupGrid grid = editor.GetComponent<EditorGroupGrid>();
        foreach (Entity cell in grid.Cells)
        {
            if (cell.IsNull || !cell.HasComponent<EditorGroupCell>())
                continue;

            int index = cell.GetComponent<EditorGroupCell>().Index;
            if (JustPressed(cell))
            {
                state = Reopen(editor, index);
                editor.GetComponent<EditorGroupState>() = state;
            }

            Color color = index == state.Open ? CellOpen
                : selectedGroups.Contains(index) ? CellSelected
                : !levelGroups.Groups[index].IsEmpty ? CellUsed
                : CellEmpty;
            Tint(cell, color, buffer);
        }

        if (JustPressed(grid.Preview))
        {
            state.Preview = !state.Preview;
            editor.GetComponent<EditorGroupState>() = state;
        }

        UiText.Set(grid.Preview, state.Preview ? "PREVIEW ON" : "PREVIEW OFF", buffer);
        Tint(grid.Preview, state.Preview ? CellUsed : CellEmpty, buffer);
    }

    private static EditorGroupState Reopen(Entity editor, int index)
    {
        EditorGroupState state = editor.GetComponent<EditorGroupState>();
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

    /// <summary>Puts the selected notes, mines, blocks and start points into the group, or takes them out.</summary>
    private static void SetMembership(Entity editor, int group, bool add)
    {
        uint bit = ItemGroups.Bit(group);
        foreach (Entity item in new List<Entity>(editor.GetComponent<NoteSelection>().AllNotes))
        {
            if (item.IsNull || !(item.HasComponent<Note>() ||
                                 item.HasComponent<Mine>() ||
                                 item.HasComponent<Block>() ||
                                 item.HasComponent<StartPoint>()))
            {
                continue;
            }

            uint mask = item.TryGetComponent(out ItemGroups current) ? current.Mask : 0u;
            uint next = add ? mask | bit : mask & ~bit;
            if (next == mask)
                continue;

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
