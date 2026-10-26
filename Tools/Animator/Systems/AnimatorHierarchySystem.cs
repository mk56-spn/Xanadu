using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// Rebuilds the bone, attachment and clip lists when the package changes and
/// highlights the selected entries.
/// </summary>
public sealed class AnimatorHierarchySystem(
    IVisualRidFactory visuals,
    IPointerInputSource? pointer = null)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Vector2 ItemSize = new(200, 26);
    private static readonly Vector2 ClipDropdownSize = new(AnimatorLayout.ClipWidth, 30);
    private static readonly Color Selected = new("#0f766e");

    private int _version = -1;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            int version = session.GetComponent<AnimatorDocument>().Version;
            if (version != _version)
            {
                _version = version;
                Rebuild(session, ui, package);
            }

            SyncBoneTree(session, ui.BoneList, package, pointer?.Read().Shift ?? false);
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            SyncClipDropdown(session.GetComponent<AnimatorUi>().ClipDropdown,
                session.GetComponent<AnimatorDocument>().Runtime.CurrentClip);
            Highlight(ui.AttachmentList, entry =>
                entry.Kind == ToolActionKind.SelectAttachment &&
                entry.Argument == selection.Attachment.ToString());
        }
    }

    private void Rebuild(Entity session, AnimatorUi ui, XafPackage package)
    {
        Clear(ui.AttachmentList);

        // The old dropdown's list and options float on the menu layer, outside
        // the header slot, so they have to be removed along with its button.
        List<Entity> floating = ClipDropdownParts(ui.ClipDropdown);
        Clear(ui.ClipList);
        foreach (Entity part in floating)
            UiLayout.DeleteSubtree(part);

        var bones = new List<UiTreeItem>();
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            string? parent = bone.Parent >= 0
                ? package.Skeleton.Bones[bone.Parent].Id
                : null;
            bones.Add(new UiTreeItem(bone.Id, parent, bone.Id));
        }
        UiBuilder.Begin(Query.Store, visuals).SetTreeItems(ui.BoneList, bones);

        for (int i = 0; i < package.Attachments.Count; i++)
        {
            AddEntry(ui.AttachmentList, package.Attachments[i].Group,
                ToolActionKind.SelectAttachment, ItemSize, i.ToString());
        }

        session.GetComponent<AnimatorUi>().ClipDropdown = AddClipDropdown(ui, package);
    }

    /// <summary>
    /// A dropdown of the package's clips: its button sits in the header slot
    /// and its list on the menu layer, so an open list draws above the preview.
    /// Each option carries the same action as a clip button, so the action
    /// system switches clips.
    /// </summary>
    private Entity AddClipDropdown(AnimatorUi ui, XafPackage package)
    {
        if (ui.ClipList.IsNull || ui.MenuLayer.IsNull)
            return default;

        var labels = new List<string>();
        foreach (XafClip clip in package.Clips)
            labels.Add(clip.Id.ToUpperInvariant());

        Entity button = UiBuilder.Begin(Query.Store, visuals).Use(ui.ClipList)
            .Button("", ClipDropdownSize);
        button.AddComponent(new UiTooltip("Choose the clip to edit"));
        UiDropdownControl control = UiBuilder.Begin(Query.Store, visuals).Use(ui.MenuLayer)
            .DropdownList(button, labels);
        for (int i = 0; i < control.Options.Count; i++)
        {
            control.Options[i].AddComponent(
                new ToolAction(ToolActionKind.Clip, package.Clips[i].Id));
            AnimatorTooltips.Attach(
                control.Options[i], ToolActionKind.Clip, package.Clips[i].Id);
        }
        return control.Button;
    }

    /// <summary>The list and options of a clip dropdown, which live apart from its button.</summary>
    private List<Entity> ClipDropdownParts(Entity button)
    {
        var parts = new List<Entity>();
        if (button.IsNull || !button.TryGetComponent(out Dropdown dropdown))
            return parts;

        if (!dropdown.List.IsNull)
            parts.Add(dropdown.List);
        foreach (Entity option in Query.Store.Query<DropdownOption>().ToEntityList())
        {
            if (option.GetComponent<DropdownOption>().Owner.Id == button.Id)
                parts.Add(option);
        }
        return parts;
    }

    /// <summary>Shows the current clip on the dropdown button and marks its option.</summary>
    private void SyncClipDropdown(Entity button, string current)
    {
        if (button.IsNull || !button.HasComponent<Text>())
            return;

        string label = current.ToUpperInvariant();
        ref Text text = ref button.GetComponent<Text>();
        if (text.Value != label)
        {
            text.Value = label;
            button.AddTag<Dirty>();
        }

        foreach (Entity option in Query.Store.Query<ToolAction, ButtonVisual, DropdownOption>().ToEntityList())
        {
            ToolAction action = option.GetComponent<ToolAction>();
            if (action.Kind != ToolActionKind.Clip)
                continue;

            Color target = action.Argument == current ? Selected : UiTheme.DropdownOption;
            ref ButtonVisual visual = ref option.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }

    /// <summary>
    /// A click in the tree selects that bone, or, while the bone menu waits for
    /// a new parent, reparents onto it; a selection made elsewhere (the
    /// preview, add/delete, undo) is shown in the tree, expanding to reveal it.
    /// </summary>
    private static void SyncBoneTree(
        Entity session,
        Entity tree,
        XafPackage package,
        bool shift)
    {
        if (tree.IsNull)
            return;

        if (tree.TryGetComponent(out UiTreeChanged changed))
        {
            tree.RemoveComponent<UiTreeChanged>();
            ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
            if (menu.Reparenting >= 0)
            {
                int child = menu.Reparenting;
                menu.Reparenting = -1;
                AnimatorEditing.ReparentBone(session, child, XafRig.FindBone(package, changed.Id));
            }
            else if (shift)
            {
                ToggleBone(session, XafRig.FindBone(package, changed.Id), package);
            }
            else
            {
                AnimatorCommands.Run(
                    session,
                    new ToolAction(ToolActionKind.SelectBone, changed.Id));
            }
        }

        int bone = session.GetComponent<AnimatorSelection>().Bone;
        string id = bone >= 0 && bone < package.Skeleton.Bones.Count
            ? package.Skeleton.Bones[bone].Id
            : "";

        // Only on a real change: revealing every frame would stop the user
        // collapsing a branch that holds the selection.
        if (UiTreeControl.Selected(tree) != id)
            UiTreeControl.Select(tree, id, raiseChanged: false);

        HighlightGroup(session, tree, package, id);
    }

    /// <summary>Shows the extra bones of a Shift-click group beside the primary tree row.</summary>
    private static void HighlightGroup(
        Entity session,
        Entity tree,
        XafPackage package,
        string primaryId)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        var extras = new List<string>();
        foreach (int index in selection.Bones())
        {
            if (index < 0 || index >= package.Skeleton.Bones.Count)
                continue;

            string id = package.Skeleton.Bones[index].Id;
            if (id != primaryId)
                extras.Add(id);
        }

        UiTreeControl.SetMultiSelected(tree, extras);
    }

    private static void ToggleBone(Entity session, int bone, XafPackage package)
    {
        if (bone < 0)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.ToggleBone(bone);
        int count = selection.Bones().Count;
        AnimatorSession.SetStatus(
            session,
            count > 1
                ? $"{package.Skeleton.Bones[bone].Id} ADDED • {count} BONES SELECTED"
                : $"SELECTED {package.Skeleton.Bones[bone].Id}");
    }

    private void AddEntry(
        Entity list,
        string label,
        ToolActionKind kind,
        Vector2 size,
        string argument)
    {
        if (list.IsNull)
            return;

        UiBuilder ui = UiBuilder.Begin(Query.Store, visuals).Use(list);
        Entity entry = ui.Button(label, size);
        entry.AddComponent(new ToolAction(kind, argument));
        AnimatorTooltips.Attach(entry, kind, argument);
    }

    private static void Clear(Entity list)
    {
        if (list.IsNull)
            return;

        var children = new List<Entity>();
        foreach (Entity child in list.ChildEntities)
            children.Add(child);
        foreach (Entity child in children)
            UiLayout.DeleteSubtree(child);
        list.AddTag<LayoutDirty>();
    }

    private static void Highlight(Entity list, System.Func<ToolAction, bool> isSelected)
    {
        if (list.IsNull)
            return;

        foreach (Entity child in list.ChildEntities)
        {
            if (!child.HasComponent<ToolAction>() || !child.HasComponent<ButtonVisual>())
                continue;

            Color target = isSelected(child.GetComponent<ToolAction>())
                ? Selected
                : UiTheme.Button;
            ref ButtonVisual visual = ref child.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }
}
