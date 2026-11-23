using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Editor;

/// <summary>
/// What the level editor's group keys go to: the picked group, and in it the
/// whole group (node "") or, with the one-member escape hatch on, the one
/// selected deco that belongs to it.
/// </summary>
public static class EditorGroupTarget
{
    /// <summary>The picked group, or null.</summary>
    public static GroupDocument? Picked(Entity editor)
    {
        if (!editor.TryGetComponent(out LevelGroups groups) || !editor.TryGetComponent(out EditorGroupEdit edit))
            return null;
        return edit.Picked >= 0 && edit.Picked < groups.Groups.Count ? groups.Groups[edit.Picked] : null;
    }

    /// <summary>
    /// The picked group and the node keys go to: "" for the whole group, a
    /// member's id in one-member mode, or null in one-member mode while the
    /// selection isn't exactly one of the group's members (nothing to key).
    /// </summary>
    public static (GroupDocument? Group, string? Node) Of(Entity editor)
    {
        GroupDocument? group = Picked(editor);
        if (group is null)
            return (null, null);
        if (!editor.GetComponent<EditorGroupEdit>().OneMember)
            return (group, "");

        var selection = editor.GetComponent<NoteSelection>().AllNotes;
        if (selection.Count != 1 || selection[0].IsNull || !selection[0].TryGetComponent(out Deco deco) ||
            !group.Members.Contains(deco.Xif.Id))
            return (group, null);
        return (group, deco.Xif.Id);
    }
}
