using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Levels;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// Keeps the sidebar's GROUPS section (<see cref="EditorGroupPanel"/>) on the
/// level's groups: a named row per group with the picked one lit, the hint
/// while there are none, the picked group's name, loop and field sliders at the
/// playhead's values, and the lit state of EASE and the one-member escape
/// hatch. A slider dragged keys its field there; the name is renamed when its
/// field lets go of focus; the shared ease picker sets the keys' curve.
/// </summary>
public sealed class EditorGroupPanelSystem : QuerySystem<UiCommand>
{
    private readonly EntityStore _entities;
    private readonly HashSet<int> _heard = [];
    private readonly float[] _one = new float[1];
    private Entity _picker;
    private bool _nameFocused;
    private int _lastPicked = -1;

    public EditorGroupPanelSystem(EntityStore entities)
    {
        _entities = entities;

        // Hidden rows still have to be found to be shown again.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<Playhead, EditorGroupEdit>(_entities);
        if (editor.IsNull || !editor.HasComponent<LevelGroups>())
            return;

        if (_picker.IsNull && !editor.Parent.IsNull)
        {
            UiNode layer = UiNode.Layer(_entities, editor.Parent, UiLayers.EditorEasePicker, null, UiStage.Full);
            _picker = EditorEasePicker.Build(layer, _entities);
        }

        LevelGroups groups = editor.GetComponent<LevelGroups>();
        EditorGroupEdit edit = KeepWindow(editor, groups.Groups.Count);
        (GroupDocument? current, string? node) = EditorGroupTarget.Of(editor);
        double beat = EditorGroupCommands.PlayheadBeat(editor);
        CommandBuffer buffer = CommandBuffer;

        foreach ((Entity entity, EditorButtonKind kind, UiCommand command) in UiCommand.All<EditorButtonKind>(_entities))
        {
            switch (kind)
            {
                case EditorButtonKind.GroupPick:
                    ShowRow(entity, command, groups, edit, buffer);
                    break;
                case EditorButtonKind.GroupPage:
                    ShowPage(entity, command.Arg, groups.Groups.Count, edit.FirstRow, buffer);
                    break;
                case EditorButtonKind.GroupField:
                    ShowField(entity, command.Arg, current, node, beat, buffer);
                    break;
                case EditorButtonKind.GroupLoop when command.Arg == 0 && current is not null:
                    SetText(entity, current.Clip.Loop ? $"{current.Clip.LoopEnd - current.Clip.LoopStart:0.##} beats" : "off", buffer);
                    break;
                case EditorButtonKind.GroupName when current is not null:
                    ShowName(entity, current);
                    break;
                case EditorButtonKind.GroupOneMember:
                    EditorButtons.Tint(entity, edit.OneMember, buffer);
                    break;
                case EditorButtonKind.GroupEase:
                    EditorButtons.Tint(entity, edit.Easing, buffer);
                    break;
            }
        }
    }

    /// <summary>
    /// Keeps the rows' window on the groups: inside the list, and moved to show
    /// the picked group whenever the pick changes (a new group lands at the end).
    /// </summary>
    private EditorGroupEdit KeepWindow(Entity editor, int count)
    {
        ref EditorGroupEdit edit = ref editor.GetComponent<EditorGroupEdit>();
        if (edit.Picked != _lastPicked && edit.Picked >= 0)
        {
            if (edit.Picked < edit.FirstRow)
                edit.FirstRow = edit.Picked;
            else if (edit.Picked >= edit.FirstRow + EditorGroupPanel.Rows)
                edit.FirstRow = edit.Picked - EditorGroupPanel.Rows + 1;
        }
        _lastPicked = edit.Picked;
        edit.FirstRow = System.Math.Clamp(edit.FirstRow, 0, System.Math.Max(0, count - EditorGroupPanel.Rows));
        return edit;
    }

    /// <summary>The ▲ ▼ row shows only when the groups overflow the rows; its readout says which are showing.</summary>
    private static void ShowPage(Entity entity, int arg, int count, int first, CommandBuffer buffer)
    {
        if (arg == -2)
            UiLayout.SetHidden(entity, buffer, count <= EditorGroupPanel.Rows);
        else if (arg == 0)
            SetText(entity, $"{first + 1}–{System.Math.Min(count, first + EditorGroupPanel.Rows)} of {count}", buffer);
    }

    private static void ShowRow(Entity row, UiCommand command, LevelGroups groups, EditorGroupEdit edit, CommandBuffer buffer)
    {
        // The row with no text is the hint, shown only while there is nothing to pick.
        if (command.Text.Length == 0)
        {
            UiLayout.SetHidden(row, buffer, groups.Groups.Count > 0);
            return;
        }

        // Each row shows the group its place in the window points at, and picks that one.
        int index = edit.FirstRow + int.Parse(command.Text);
        row.GetComponent<UiCommand>().Arg = index;
        int picked = edit.Picked;

        bool shown = index < groups.Groups.Count;
        UiLayout.SetHidden(row, buffer, !shown);
        if (!shown)
            return;

        GroupDocument group = groups.Groups[index];
        SetText(row, $"{group.Name}  ({group.Members.Count})", buffer);
        EditorButtons.Tint(row, index == picked, buffer);
    }

    /// <summary>The editor the panel belongs to; null when the scene has none.</summary>
    internal static Entity Editor(EntityStore store) => EditorSceneQuery.First<Playhead, EditorGroupEdit>(store);

    /// <summary>The name shows the group's own while unfocused; letting go of focus renames it.</summary>
    private void ShowName(Entity entity, GroupDocument group)
    {
        ref TextField field = ref entity.GetComponent<TextField>();
        bool focused = entity.Tags.Has<TextFieldFocused>();
        if (!focused && _nameFocused && field.Value.Trim() != group.Name && field.Value.Trim().Length > 0)
            Editor(_entities).EmitOnEditor(new RenameGroup(field.Value));
        else if (!focused && field.Value != group.Name)
            field.Value = group.Name;
        _nameFocused = focused;
    }

    private void ShowField(Entity field, int index, GroupDocument? current, string? node, double beat, CommandBuffer buffer)
    {
        // -1 is the column holding the picked group's controls: there only for a picked group.
        if (index == -1)
        {
            UiLayout.SetHidden(field, buffer, current is null);
            return;
        }

        // -2 says what the sliders key.
        if (index == -2)
        {
            SetText(field, node switch
            {
                "" => "AT THE PLAYHEAD (DRAG TO KEY)",
                null => "ONE MEMBER: SELECT ONE OF THE GROUP",
                _ => $"KEYING {node.ToUpperInvariant()} ONLY",
            }, buffer);
            return;
        }

        if (_heard.Add(field.Id))
        {
            int at = index;
            field.OnValueChanged((values, live) =>
                Editor(_entities).EmitOnEditor(new KeyGroup(at, EditorGroupPanel.Kept(at, values[0]), live)));
        }

        UiLayout.SetHidden(field, buffer, node is null);

        // A slider being dragged shows the drag, not the keys it is writing.
        if (current is null || node is null || !field.TryGetComponent(out UiValueField state) || state.Live)
            return;

        _one[0] = EditorGroupPanel.Shown(index, current.ValueAt(node, LevelItemFields.All[index].Name, beat));
        if (state.Values[0] != _one[0])
            UiValueFields.Set(field, _one);
    }

    private static void SetText(Entity entity, string label, CommandBuffer buffer)
    {
        if (!entity.TryGetComponent(out Text text) || text.Value == label)
            return;
        entity.GetComponent<Text>().Value = label;
        buffer.AddTag<Dirty>(entity.Id);
    }
}
