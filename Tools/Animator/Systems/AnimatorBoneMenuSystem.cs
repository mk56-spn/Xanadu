using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Right-clicking a bone in the bone tree, or its label or joint in the
/// preview, selects the bone and opens a menu to rename, reparent, add a child to or delete it. The menu
/// options are <see cref="ToolAction"/> buttons; this system places the menu,
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
        bool primary = input.Pressed && !_primaryHeld;
        bool secondary = input.SecondaryPressed && !_secondaryHeld;
        _primaryHeld = input.Pressed;
        _secondaryHeld = input.SecondaryPressed;

        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (session.GetComponent<AnimatorNewClipDialog>().Open)
            {
                ref AnimatorBoneMenu closed = ref session.GetComponent<AnimatorBoneMenu>();
                closed.Open = false;
                if (closed.Renaming >= 0)
                    Stop(ref closed, ui.BoneNameField);
                else
                    AnimatorMenuPlacement.Place(ui.BoneMenu, UiLayout.Hidden);
                continue;
            }

            if (secondary)
                RightClick(session, input.Position);
            else if (primary)
                session.GetComponent<AnimatorBoneMenu>().Open = false;

            AnimatorBoneMenu menu = session.GetComponent<AnimatorBoneMenu>();
            AnimatorMenuPlacement.Place(ui.BoneMenu, menu.Open ? menu.Anchor + AnimatorLayout.BoneMenuSize / 2 : UiLayout.Hidden);
            Rename(session, ui.BoneNameField, input.Pressed);
        }
    }

    private static void RightClick(Entity session, Vector2 pointer)
    {
        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        menu.Open = false;
        menu.Reparenting = -1;

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        string? id = AnimatorGeometry.TreeLabelAt(ui.BoneList, pointer);
        bool fromTree = id is not null;
        if (id is null)
        {
            Rect2 area = AnimatorGeometry.PanelArea(ui.Preview);
            int bone = area.HasPoint(pointer) ? PickBone(session, area, pointer) : -1;
            if (bone < 0)
                return;
            id = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[bone].Id;
        }

        AnimatorCommands.Run(session, new ToolAction(ToolActionKind.SelectBone, id));
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
        XafPose pose = AnimatorGeometry.PreviewPose(document, session.GetComponent<AnimatorModes>());
        return XafRig.WorldBones(document.Package, pose);
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
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
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
        AnimatorMenuPlacement.Place(field, UiLayout.Hidden);
    }

}
