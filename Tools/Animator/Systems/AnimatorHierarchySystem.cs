using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Rebuilds the rig tree (bones with their parts, chains and cloth) and the
/// clip dropdown when the package changes, refreshes the tree when the clip or
/// its pins and ellipses change (they mark the bones they drive), and shows the selection in the tree.
/// </summary>
public sealed class AnimatorHierarchySystem(
    IVisualItemFactory visuals,
    IPointerInputSource? pointer = null)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Vector2 ClipDropdownSize = new(AnimatorLayout.ClipWidth, EditorShellMetrics.HeaderControlHeight);
    private static readonly Color Selected = Palette.Selected;

    private int _version = -1;
    private int _treeVersion = -1;
    private int? _treeOwners;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int version = session.GetComponent<AnimatorDocument>().Version;
            if (version != _version)
            {
                _version = version;
                Rebuild(session, ui, package);
            }

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            RigClip? clip = AnimatorSession.CurrentClip(document);
            int owners = AnimatorRigTree.OwnersKey(clip);
            if (version != _treeVersion || owners != _treeOwners)
            {
                _treeVersion = version;
                _treeOwners = owners;
                UiBuilder.Begin(Query.Store, visuals).SetTreeItems(ui.BoneList, AnimatorRigTree.Items(package, clip));
            }

            SyncBoneTree(session, ui.BoneList, package, pointer?.Read().Shift ?? false);
            SyncClipDropdown(session.GetComponent<AnimatorUi>().ClipDropdown,
                session.GetComponent<AnimatorDocument>().Runtime.CurrentClip);
        }
    }

    private void Rebuild(Entity session, AnimatorUi ui, RigPackage package)
    {
        // The old dropdown's list and options float on the menu layer, outside
        // the header slot, so they have to be removed along with its button.
        List<Entity> floating = ClipDropdownParts(ui.ClipDropdown);
        Clear(ui.ClipList);
        foreach (Entity part in floating)
            UiLayout.DeleteSubtree(part);

        session.GetComponent<AnimatorUi>().ClipDropdown = AddClipDropdown(ui, package);
    }

    /// <summary>
    /// A dropdown of the package's clips: its button sits in the header slot
    /// and its list on the menu layer, so an open list draws above the preview.
    /// Each option carries the same action as a clip button, so the action
    /// system switches clips.
    /// </summary>
    private Entity AddClipDropdown(AnimatorUi ui, RigPackage package)
    {
        if (ui.ClipList.IsNull || ui.MenuLayer.IsNull)
            return default;

        var labels = new List<string>();
        foreach (RigClip clip in package.Clips)
            labels.Add(clip.Id.ToUpperInvariant());

        Entity button = UiBuilder.Begin(Query.Store, visuals).Use(ui.ClipList)
            .Button("", ClipDropdownSize);
        button.AddComponent(new UiTooltip("Choose the clip to edit"));
        UiDropdownControl control = UiBuilder.Begin(Query.Store, visuals).Use(ui.MenuLayer)
            .DropdownList(button, labels);
        for (int i = 0; i < control.Options.Count; i++)
        {
            control.Options[i].AddComponent(
                ToolAction.Of(ToolActionKind.Clip, package.Clips[i].Id));
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
        foreach (EntityLink<DropdownOption> link in button.GetIncomingLinks<DropdownOption>())
            parts.Add(link.Entity);
        return parts;
    }

    /// <summary>Shows the current clip on the dropdown button and marks its option.</summary>
    private void SyncClipDropdown(Entity button, string current)
    {
        if (button.IsNull || !button.HasComponent<Text>())
            return;

        string label = current.ToUpperInvariant();
        UiText.Set(button, label);

        foreach (Entity option in Query.Store.Query<UiCommand, ButtonVisual, DropdownOption>().WithDisabled().ToEntityList())
        {
            if (!option.TryGetToolAction(out ToolAction action) || action.Kind != ToolActionKind.Clip)
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
        RigPackage package,
        bool shift)
    {
        if (tree.IsNull)
            return;

        if (tree.TryGetComponent(out UiTreeChanged changed))
        {
            tree.RemoveComponent<UiTreeChanged>();
            ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
            if (AnimatorRigTree.IsOther(changed.Id))
            {
                // A part, chain or cloth row: picking or reparenting wants a bone, so it only selects.
                AnimatorCommands.Run(session, AnimatorRigTree.Select(changed.Id));
            }
            else if (session.GetComponent<AnimatorChains>().Picking != AnimatorChainPick.None)
            {
                // A chain inspector button is waiting for a bone, and the tree is a way to give one.
                AnimatorChainPlacementCommands.PickBone(session, RigRig.FindBone(package, changed.Id));
            }
            else if (menu.Reparenting >= 0)
            {
                int child = menu.Reparenting;
                menu.Reparenting = -1;
                AnimatorEditing.ReparentBone(session, child, RigRig.FindBone(package, changed.Id));
            }
            else if (shift)
            {
                ToggleBone(session, RigRig.FindBone(package, changed.Id), package);
            }
            else
            {
                AnimatorCommands.Run(
                    session,
                    new ToolAction(ToolActionKind.SelectBone, changed.Id));
            }
        }

        string id = AnimatorRigTree.Selected(session.GetComponent<AnimatorSelection>(), package);

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
        RigPackage package,
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

    private static void ToggleBone(Entity session, int bone, RigPackage package)
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

    private static void Clear(Entity list)
    {
        if (list.IsNull)
            return;

        UiLayout.DeleteChildren(list);
    }
}
