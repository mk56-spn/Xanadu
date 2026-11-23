using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Logic.Animator;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Animator;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Right-clicking a bone in the bone tree, or its label or joint in the
/// preview, selects the bone and opens a menu to rename, reparent, add a child to or delete it. The menu
/// options are <see cref="Intent"/> buttons; this system places the menu,
/// closes it on the next click, and runs the rename field.
/// </summary>
public sealed class AnimatorBoneMenuSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _primaryHeld;
    private bool _secondaryHeld;

    /// <summary>Whether the rename field has been given focus and is being typed in.</summary>
    private bool _renameFocused;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = AnimatorPointerEdge.Primary(ref _primaryHeld, input);
        bool secondary = AnimatorPointerEdge.Secondary(ref _secondaryHeld, input);

        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (AnimatorUi.Of(session).NewClipDialog.IsOpen())
            {
                ref AnimatorBoneMenu closed = ref session.GetComponent<AnimatorBoneMenu>();
                closed.Open = false;
                if (closed.Renaming >= 0)
                    Stop(ref closed, ui.BoneNameField);
                else
                    AnimatorMenuPlacement.Hide(ui.BoneMenu);
                continue;
            }

            // Preview mode is for watching: no bone menu over the rig.
            if (secondary && !AnimatorPreviewMode.IsOn(session))
                RightClick(session, input.Position);
            else if (primary)
            {
                session.GetComponent<AnimatorBoneMenu>().Open = false;
                session.GetComponent<AnimatorBoneMenu>().RowOpen = false;
            }

            AnimatorBoneMenu menu = session.GetComponent<AnimatorBoneMenu>();
            AnimatorMenuPlacement.Place(ui.BoneMenu, menu.Anchor + AnimatorLayout.BoneMenuSize / 2, shown: menu.Open);
            AnimatorMenuPlacement.Place(ui.RowMenu, menu.Anchor + AnimatorLayout.RowMenuSize / 2, shown: menu.RowOpen);
            Rename(session, ui.BoneNameField, input.Pressed);
        }
    }

    private static void RightClick(Entity session, Vector2 pointer)
    {
        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        menu.Open = false;
        menu.RowOpen = false;
        menu.Reparenting = -1;

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        AnimatorRigRow? row = AnimatorRigTree.Row(AnimatorGeometry.TreeLabelAt(ui.BoneList, pointer), package);
        if (row is { Kind: not AnimatorRigRowKind.Bone } other)
        {
            // A part, chain or cloth row: select it and offer to delete it.
            AnimatorRigTree.Select(other).EmitOn(session);
            (string label, IIntent kind) = AnimatorRigTree.Delete(other);
            UiText.Set(ui.RowMenuDelete, label);
            ui.RowMenuDelete.AddComponent(new Intent(kind));
            AnimatorTooltips.Attach(ui.RowMenuDelete, kind);
            menu.RowOpen = true;
            menu.Anchor = pointer;
            return;
        }
        bool fromTree = row is not null;
        int bone = row?.Index ?? -1;
        if (row is null)
        {
            Rect2 area = AnimatorGeometry.PanelArea(ui.Preview);
            bone = area.HasPoint(pointer) ? PickBone(session, area, pointer) : -1;
            if (bone < 0)
                return;
        }

        session.EmitSignal(new SelectBone(bone));
        menu.Open = true;
        menu.Anchor = pointer;
        menu.FromTree = fromTree;
    }

    private static int PickBone(Entity session, Rect2 area, Vector2 pointer) =>
        AnimatorGeometry.PickBoneOrLabel(
            PreviewWorld(session),
            AnimatorGeometry.PreviewPlacement(
                area,
                session.GetComponent<AnimatorPreviewView>()),
            pointer);

    private static Transform2D[] PreviewWorld(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigPose pose = AnimatorGeometry.PreviewPose(document, AnimatorModes.Of(session));
        return RigRig.WorldBones(document.Package, pose);
    }

    /// <summary>
    /// Shows the name field over the bone's label (tree row or preview) once the click that chose
    /// RENAME is released (a held press would take focus straight back), and
    /// renames the bone when typing ends with Enter or a click elsewhere.
    /// </summary>
    private void Rename(Entity session, Entity field, bool pressed)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (menu.Renaming < 0 || menu.Renaming >= package.Skeleton.Bones.Count)
        {
            // Undo, load or a rig change can drop the bone mid-rename.
            if (_renameFocused || menu.Renaming >= 0)
                Stop(ref menu, field);
            return;
        }

        if (!_renameFocused)
        {
            if (pressed)
                return;

            Rect2 label = NameFieldTarget(session, menu);
            AnimatorMenuPlacement.Place(field, label.Position + AnimatorLayout.BoneNameSize / 2);
            field.GetComponent<TextField>().Value = package.Skeleton.Bones[menu.Renaming].Id;
            foreach (Entity other in Query.Store.Query<TextField>()
                         .AllTags(Tags.Get<TextFieldFocused>()).ToEntityList())
            {
                other.RemoveTag<TextFieldFocused>();
            }
            field.AddTag<TextFieldFocused>();
            _renameFocused = true;
            return;
        }

        if (field.Tags.Has<TextFieldFocused>())
            return;

        int bone = menu.Renaming;
        string name = field.GetComponent<TextField>().Value.Trim();
        Stop(ref menu, field);
        if (name.Length > 0 && name != package.Skeleton.Bones[bone].Id)
            AnimatorEditing.RenameBone(session, bone, name);
        else
            AnimatorSession.SetStatus(session, "RENAME CANCELLED");
    }

    private static Rect2 NameFieldTarget(Entity session, AnimatorBoneMenu menu)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        string id = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[menu.Renaming].Id;
        if (menu.FromTree && AnimatorGeometry.TreeLabel(ui.BoneList, id) is Rect2 row)
        {
            // Vertically centred on the row.
            float y = row.Position.Y + (row.Size.Y - AnimatorLayout.BoneNameSize.Y) / 2;
            return new Rect2(new Vector2(row.Position.X, y), AnimatorLayout.BoneNameSize);
        }

        Rect2 area = AnimatorGeometry.PanelArea(ui.Preview);
        Transform2D placement = AnimatorGeometry.PreviewPlacement(
            area,
            session.GetComponent<AnimatorPreviewView>());
        Vector2 joint = placement * PreviewWorld(session)[menu.Renaming].Origin;
        return AnimatorGeometry.BoneLabel(joint);
    }

    private void Stop(ref AnimatorBoneMenu menu, Entity field)
    {
        menu.Renaming = -1;
        _renameFocused = false;
        field.RemoveTag<TextFieldFocused>();
        AnimatorMenuPlacement.Hide(field);
    }

}
