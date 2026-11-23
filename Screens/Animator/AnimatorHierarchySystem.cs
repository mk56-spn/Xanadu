using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Theme;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Screens.Animator;

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
    private int _wiredTree;

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

            if (ui.BoneList.Id != _wiredTree && !ui.BoneList.IsNull)
            {
                _wiredTree = ui.BoneList.Id;
                ui.BoneList.OnSelected(id => TreePicked(session, id));
            }

            SyncBoneTree(session, ui.BoneList, package);
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
                new Intent(new PlayClip(package.Clips[i].Id)));
            AnimatorTooltips.Attach(
                control.Options[i], new PlayClip(package.Clips[i].Id));
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

        foreach (Entity option in Query.Store.Query<Intent, ButtonVisual, DropdownOption>().WithDisabled().ToEntityList())
        {
            if (!option.TryGetIntent(out PlayClip clip))
                continue;

            Color target = clip.Clip == current ? Selected : UiTheme.DropdownOption;
            ref ButtonVisual visual = ref option.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }

    /// <summary>
    /// Shows a selection made elsewhere (the preview, add/delete, undo) in the
    /// tree, expanding to reveal it. Clicks in the tree go to <see cref="TreePicked"/>.
    /// </summary>
    private static void SyncBoneTree(
        Entity session,
        Entity tree,
        RigPackage package)
    {
        if (tree.IsNull)
            return;

        AnimatorRigRow selected = AnimatorRigTree.Selected(session.GetComponent<AnimatorSelection>(), package);
        string id = AnimatorRigTree.TreeId(selected, package);

        // Only on a real change: revealing every frame would stop the user
        // collapsing a branch that holds the selection.
        if (UiTreeControl.Selected(tree) != id)
            UiTreeControl.Select(tree, id, raiseChanged: false);

        HighlightGroup(session, tree, package, selected);
    }

    /// <summary>
    /// A click on a tree row (<see cref="UiTreeControl.OnSelected"/>) selects
    /// that bone, or, while the bone menu waits for a new parent, reparents onto it.
    /// </summary>
    private void TreePicked(Entity session, string id)
    {
        if (session.IsNull || !session.HasComponent<AnimatorDocument>())
            return;

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        bool shift = pointer?.Read().Shift ?? false;
        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        AnimatorRigRow? row = AnimatorRigTree.Row(id, package);
        int bone = row is { Kind: AnimatorRigRowKind.Bone } ? row.Value.Index : -1;
        if (row is { Kind: not AnimatorRigRowKind.Bone })
        {
            // A part, chain or cloth row: picking or reparenting wants a bone, so it only selects.
            AnimatorRigTree.Select(row.Value).EmitOn(session);
        }
        else if (row is null)
        {
            // A row of nothing the package still holds.
        }
        else if (session.GetComponent<AnimatorChains>().Picking != AnimatorChainPick.None)
        {
            // A chain inspector button is waiting for a bone, and the tree is a way to give one.
            AnimatorChainPlacementCommands.PickBone(session, bone);
        }
        else if (menu.Reparenting >= 0)
        {
            int child = menu.Reparenting;
            menu.Reparenting = -1;
            AnimatorEditing.ReparentBone(session, child, bone);
        }
        else if (shift)
        {
            ToggleBone(session, bone, package);
        }
        else
        {
            AnimatorRigTree.Select(row.Value).EmitOn(session);
        }
    }

    /// <summary>Shows the extra bones of a Shift-click group beside the primary tree row.</summary>
    private static void HighlightGroup(
        Entity session,
        Entity tree,
        RigPackage package,
        AnimatorRigRow primary)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        var extras = new List<string>();
        foreach (int index in selection.Bones())
        {
            var row = new AnimatorRigRow(AnimatorRigRowKind.Bone, index);
            if (index >= 0 && index < package.Skeleton.Bones.Count && row != primary)
                extras.Add(AnimatorRigTree.TreeId(row, package));
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
