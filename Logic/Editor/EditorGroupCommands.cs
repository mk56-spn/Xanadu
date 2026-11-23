using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Logic.Editor.Commands;
using Xanadu.Logic.Editor.History;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Levels;

namespace Xanadu.Logic.Editor;

/// <summary>
/// The level's groups as the editor changes them: made from the selected
/// decos, picked, renamed, given or relieved of members, looped, deleted, and
/// keyed at the playhead on the song's beats. Keys go to the whole group; the
/// one-member escape hatch sends them to one selected member instead
/// (<see cref="EditorGroupTarget"/>). Every change is one undo step; a keying
/// drag is one step from its first move to its release.
/// </summary>
public sealed class EditorGroupCommands(EditorCommandContext context)
{
    /// <summary>The loop lengths a group steps through, in beats; 0 is no loop.</summary>
    public static readonly int[] LoopLengths = [0, 1, 2, 4, 8, 16, 32];

    private EditorSnapshot? _dragBefore;

    public bool TryApply(EditorCommand command, Entity editor)
    {
        if (command.Type is not (EditorCommandType.NewGroup or EditorCommandType.PickGroup or
            EditorCommandType.AddToGroup or EditorCommandType.RemoveFromGroup or EditorCommandType.DeleteGroup or
            EditorCommandType.KeyGroup or EditorCommandType.DeleteGroupKeys or EditorCommandType.StepGroupLoop or
            EditorCommandType.RenameGroup or EditorCommandType.ToggleOneMember or EditorCommandType.ToggleGroupEase or
            EditorCommandType.EaseGroupKeys))
            return false;

        // The scene gives the editor its groups; nothing here may add a component mid-query.
        if (!editor.HasComponent<LevelGroups>() || !editor.HasComponent<EditorGroupEdit>())
            return true;

        ref EditorGroupEdit edit = ref editor.GetComponent<EditorGroupEdit>();
        switch (command.Type)
        {
            case EditorCommandType.PickGroup:
                Pick(editor, command.IntValue);
                return true;
            case EditorCommandType.ToggleOneMember:
                edit.OneMember = !edit.OneMember;
                return true;
            case EditorCommandType.ToggleGroupEase:
                edit.Easing = command.IntValue != 0;
                return true;
            case EditorCommandType.KeyGroup:
                Key(editor, command.IntValue, command.FloatValue, command.LongValue != 0);
                return true;
        }

        EditorSnapshot before = context.History.Capture();
        List<GroupDocument> groups = editor.GetComponent<LevelGroups>().Groups;
        (GroupDocument? current, string? node) = EditorGroupTarget.Of(editor);
        switch (command.Type)
        {
            case EditorCommandType.NewGroup:
                List<string> selected = SelectedIds(editor);
                if (selected.Count == 0)
                    return true;
                groups.Add(new GroupDocument { Name = FreshName(groups), Members = selected });
                edit.Picked = groups.Count - 1;
                break;

            case EditorCommandType.RenameGroup when current is not null:
                string name = (command.Text ?? "").Trim();
                if (name.Length > 0)
                    current.Name = name;
                break;

            case EditorCommandType.AddToGroup when current is not null:
                foreach (string id in SelectedIds(editor))
                {
                    if (!current.Members.Contains(id))
                        current.Members.Add(id);
                }
                break;

            case EditorCommandType.RemoveFromGroup when current is not null:
                foreach (string id in SelectedIds(editor))
                {
                    current.Members.Remove(id);
                    // A member's own keys leave with it.
                    current.Clip.Channels.RemoveAll(channel => channel.Address.Node == id);
                }
                break;

            case EditorCommandType.DeleteGroup when current is not null:
                groups.RemoveAt(edit.Picked);
                edit.Picked = Math.Min(edit.Picked, groups.Count - 1);
                break;

            case EditorCommandType.StepGroupLoop when current is not null:
                StepLoop(current, command.IntValue);
                break;

            case EditorCommandType.DeleteGroupKeys when current is not null && node is not null:
                current.Clip.RemoveKeysAt(current.ClipTime(PlayheadBeat(editor)), node);
                break;

            case EditorCommandType.EaseGroupKeys when current is not null && node is not null:
                current.Clip.ApplyEase(current.ClipTime(PlayheadBeat(editor)), (EasePreset)command.IntValue, node);
                break;
        }

        context.History.Record(before, context.History.Capture());
        return true;
    }

    /// <summary>The playhead's beat, on the grid, which is where keys go.</summary>
    public static double PlayheadBeat(Entity editor)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        return playhead.Tempo.BeatAt(EditorTiming.Snap(playhead.TimeMs, playhead));
    }

    /// <summary>The picked group's loop as a step of <see cref="LoopLengths"/>: from its start, so many beats long.</summary>
    private static void StepLoop(GroupDocument group, int direction)
    {
        float now = group.Clip.Loop ? group.Clip.LoopEnd - group.Clip.LoopStart : 0f;
        int at = 0;
        while (at < LoopLengths.Length - 1 && LoopLengths[at] < now - 0.001f)
            at++;
        int next = Math.Clamp(at + Math.Sign(direction), 0, LoopLengths.Length - 1);
        group.Clip.Loop = LoopLengths[next] > 0;
        group.Clip.LoopStart = 0f;
        group.Clip.LoopEnd = LoopLengths[next];
    }

    private static void Pick(Entity editor, int index)
    {
        List<GroupDocument> groups = editor.GetComponent<LevelGroups>().Groups;
        if (index < 0 || index >= groups.Count)
            return;

        editor.GetComponent<EditorGroupEdit>().Picked = index;

        // The members show as the selection, so what the group holds is plain to see.
        var members = new HashSet<string>(groups[index].Members);
        var picked = new List<Entity>();
        foreach (Entity deco in editor.Store.Query<Deco>().Entities)
        {
            if (members.Contains(deco.GetComponent<Deco>().Xif.Id))
                picked.Add(deco);
        }

        editor.GetComponent<NoteSelection>().SetMany(picked);
    }

    private void Key(Entity editor, int field, float value, bool live)
    {
        (GroupDocument? group, string? node) = EditorGroupTarget.Of(editor);
        if (group is null || node is null || field < 0 || field >= LevelItemFields.All.Length)
            return;

        _dragBefore ??= context.History.Capture();
        group.Key(node, LevelItemFields.All[field].Name, PlayheadBeat(editor), value);
        if (live)
            return;

        context.History.Record(_dragBefore.Value, context.History.Capture());
        _dragBefore = null;
    }

    /// <summary>The Xif ids of the selected decos.</summary>
    private static List<string> SelectedIds(Entity editor)
    {
        var ids = new List<string>();
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!entity.IsNull && entity.TryGetComponent(out Deco deco) && deco.Xif.Id.Length > 0 && !ids.Contains(deco.Xif.Id))
                ids.Add(deco.Xif.Id);
        }
        return ids;
    }

    private static string FreshName(List<GroupDocument> groups)
    {
        for (int n = groups.Count + 1; ; n++)
        {
            string name = $"group {n}";
            if (!groups.Exists(group => group.Name == name))
                return name;
        }
    }
}
