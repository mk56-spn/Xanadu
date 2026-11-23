using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Logic.Editor.History;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Editor.Intents;
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

    public void Listen(Entity editor)
    {
        context.On<PickGroup>(editor, (on, intent) => { if (HasGroups(on)) Pick(on, intent.Index); });
        context.On<ToggleOneMember>(editor, (on, _) =>
        {
            if (HasGroups(on))
                on.GetComponent<EditorGroupEdit>().OneMember = !on.GetComponent<EditorGroupEdit>().OneMember;
        });
        context.On<ShowGroupEase>(editor, (on, intent) =>
        {
            if (HasGroups(on))
                on.GetComponent<EditorGroupEdit>().Easing = intent.Open;
        });
        context.On<KeyGroup>(editor, (on, intent) => { if (HasGroups(on)) Key(on, intent.Field, intent.Value, intent.Live); });

        context.On<NewGroup>(editor, (on, _) => Change(on, (groups, ref edit, _, _) =>
        {
            List<string> selected = SelectedIds(on);
            if (selected.Count == 0)
                return;
            groups.Add(new GroupDocument { Name = FreshName(groups), Members = selected });
            edit.Picked = groups.Count - 1;
        }));
        context.On<RenameGroup>(editor, (on, intent) => ChangePicked(on, (current, _) =>
        {
            string name = (intent.Name ?? "").Trim();
            if (name.Length > 0)
                current.Name = name;
        }));
        context.On<AddToGroup>(editor, (on, _) => ChangePicked(on, (current, _) =>
        {
            foreach (string id in SelectedIds(on))
            {
                if (!current.Members.Contains(id))
                    current.Members.Add(id);
            }
        }));
        context.On<RemoveFromGroup>(editor, (on, _) => ChangePicked(on, (current, _) =>
        {
            foreach (string id in SelectedIds(on))
            {
                current.Members.Remove(id);
                // A member's own keys leave with it.
                current.Clip.Channels.RemoveAll(channel => channel.Address.Node == id);
            }
        }));
        context.On<DeleteGroup>(editor, (on, _) => Change(on, (groups, ref edit, current, _) =>
        {
            if (current is null)
                return;
            groups.RemoveAt(edit.Picked);
            edit.Picked = Math.Min(edit.Picked, groups.Count - 1);
        }));
        context.On<StepGroupLoop>(editor, (on, intent) => ChangePicked(on, (current, _) => StepLoop(current, intent.Direction)));
        context.On<DeleteGroupKeys>(editor, (on, _) => ChangePicked(on, (current, node) =>
        {
            if (node is not null)
                current.Clip.RemoveKeysAt(current.ClipTime(PlayheadBeat(on)), node);
        }));
        context.On<EaseGroupKeys>(editor, (on, intent) => ChangePicked(on, (current, node) =>
        {
            if (node is not null)
                current.Clip.ApplyEase(current.ClipTime(PlayheadBeat(on)), intent.Preset, node);
        }));
    }

    /// <summary>The scene gives the editor its groups; nothing here adds them.</summary>
    private static bool HasGroups(Entity editor) =>
        editor.HasComponent<LevelGroups>() && editor.HasComponent<EditorGroupEdit>();

    private delegate void GroupChange(List<GroupDocument> groups, ref EditorGroupEdit edit, GroupDocument? current, string? node);

    /// <summary>One undo step around <paramref name="change"/>.</summary>
    private void Change(Entity editor, GroupChange change)
    {
        if (!HasGroups(editor))
            return;

        EditorSnapshot before = context.History.Capture();
        (GroupDocument? current, string? node) = EditorGroupTarget.Of(editor);
        change(editor.GetComponent<LevelGroups>().Groups, ref editor.GetComponent<EditorGroupEdit>(), current, node);
        context.History.Record(before, context.History.Capture());
    }

    /// <summary>One undo step around a change to the picked group; nothing when none is picked.</summary>
    private void ChangePicked(Entity editor, Action<GroupDocument, string?> change) =>
        Change(editor, (_, ref _, current, node) =>
        {
            if (current is not null)
                change(current, node);
        });

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
