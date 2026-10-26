using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>Executes <see cref="ToolAction"/> requests against the session entity.</summary>
public static class AnimatorCommands
{
    public static void Run(Entity session, ToolAction action)
    {
        // Any other action withdraws a pending "discard unsaved changes?" question.
        if (action.Kind is not (ToolActionKind.Load or ToolActionKind.NewSkeleton))
            session.GetComponent<AnimatorFile>().DiscardArmed = false;

        switch (action.Kind)
        {
            case ToolActionKind.Play:
                session.GetComponent<AnimatorDocument>().Playback.Play();
                AnimatorSession.SetStatus(session, "PLAYING");
                break;
            case ToolActionKind.Stop:
                session.GetComponent<AnimatorDocument>().Playback.Stop();
                AnimatorSession.SetStatus(session, "STOPPED");
                break;
            case ToolActionKind.Clip:
                PlayClip(session, action.Argument);
                break;
            case ToolActionKind.Undo:
                AnimatorHistoryCommands.Undo(session);
                break;
            case ToolActionKind.Redo:
                AnimatorHistoryCommands.Redo(session);
                break;
            case ToolActionKind.Interpolate:
                if (System.Enum.TryParse(action.Argument, out XafEasePreset preset))
                    AnimatorKeyCommands.SetInterpolation(session, preset);
                break;
            case ToolActionKind.NewClip:
                AnimatorClipCommands.New(session);
                break;
            case ToolActionKind.DuplicateClip:
                AnimatorClipCommands.Duplicate(session);
                break;
            case ToolActionKind.DeleteClip:
                AnimatorClipCommands.Delete(session);
                break;
            case ToolActionKind.DurationDown:
                AnimatorClipCommands.StepDuration(session, -AnimatorClipCommands.DurationStep);
                break;
            case ToolActionKind.DurationUp:
                AnimatorClipCommands.StepDuration(session, AnimatorClipCommands.DurationStep);
                break;
            case ToolActionKind.ToggleLoop:
                AnimatorClipCommands.ToggleLoop(session);
                break;
            case ToolActionKind.SelectBone:
                SelectBone(session, action.Argument);
                break;
            case ToolActionKind.SelectAttachment:
                SelectAttachment(session, action.Argument);
                break;
            case ToolActionKind.KeyBone:
                AnimatorEditing.KeySelected(session);
                break;
            case ToolActionKind.RotateLeft:
                AnimatorEditing.Nudge(session, -AnimatorEditing.RotationStep, 0f);
                break;
            case ToolActionKind.RotateRight:
                AnimatorEditing.Nudge(session, AnimatorEditing.RotationStep, 0f);
                break;
            case ToolActionKind.ScaleDown:
                AnimatorEditing.Nudge(session, 0f, -AnimatorEditing.ScaleStep);
                break;
            case ToolActionKind.ScaleUp:
                AnimatorEditing.Nudge(session, 0f, AnimatorEditing.ScaleStep);
                break;
            case ToolActionKind.ToggleMovable:
                AnimatorEditing.ToggleMovable(session);
                break;
            case ToolActionKind.AddBone:
                AnimatorEditing.AddBone(session);
                break;
            case ToolActionKind.DeleteBone:
                AnimatorEditing.DeleteBone(session);
                break;
            case ToolActionKind.RenameBone:
                BeginBoneRename(session);
                break;
            case ToolActionKind.ReparentBone:
                BeginBoneReparent(session);
                break;
            case ToolActionKind.AddKey:
                AddKeyFromMenu(session);
                break;
            case ToolActionKind.DeleteKey:
                AnimatorKeyCommands.DeleteSelected(session);
                break;
            case ToolActionKind.AddSvg:
                OpenSvgMenu(session);
                break;
            case ToolActionKind.AttachSvg:
                AttachSvgFromMenu(session, action.Argument);
                break;
            case ToolActionKind.KeyPose:
                AnimatorPoseCommands.KeyPose(session);
                break;
            case ToolActionKind.CopyPose:
                AnimatorPoseCommands.CopyPose(session);
                break;
            case ToolActionKind.PastePose:
                AnimatorPoseCommands.PastePose(session);
                break;
            case ToolActionKind.MirrorPose:
                AnimatorPoseCommands.MirrorPose(session);
                break;
            case ToolActionKind.Save:
                AnimatorFileCommands.Save(session);
                break;
            case ToolActionKind.Load:
                AnimatorFileCommands.Load(session);
                break;
            case ToolActionKind.NewSkeleton:
                AnimatorFileCommands.NewSkeleton(session);
                break;
        }
    }

    /// <summary>
    /// Switches to a clip at once. Crossfading is for the game; here the
    /// timeline, name field and auto-key must all act on the clip you chose.
    /// </summary>
    private static void PlayClip(Entity session, string clipId)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        if (!document.Runtime.Play(clipId, 0f))
        {
            AnimatorSession.SetStatus(session, $"NO CLIP {clipId}");
            return;
        }

        session.GetComponent<AnimatorSelection>().HasKey = false;
        document.Runtime.SetPaused(false);
    }

    private static void SelectBone(Entity session, string boneId)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = package.Skeleton.Bones.FindIndex(bone => bone.Id == boneId);
        if (index < 0)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.Bone = index;
        selection.Attachment = -1;
        AnimatorSession.SetStatus(session, $"SELECTED {boneId}");
    }

    /// <summary>Adds a key at the row and time the key menu was opened on.</summary>
    private static void AddKeyFromMenu(Entity session)
    {
        AnimatorKeyMenu menu = session.GetComponent<AnimatorKeyMenu>();
        AnimatorKeyCommands.AddKeyAt(session, menu.Track, menu.Time);
    }

    /// <summary>
    /// Opens the ADD SVG picker beside the bone menu, on the selected bone.
    /// The picker system lists the project's groups and places the panel.
    /// </summary>
    private static void OpenSvgMenu(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
            return;
        }

        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        menu.Open = true;
        menu.Bone = bone;
        menu.Anchor = OpenAnchor(session);
    }

    /// <summary>Where the picker starts: just right of the bone menu that opened it.</summary>
    private static Vector2 OpenAnchor(Entity session)
    {
        Vector2 boneMenu = session.GetComponent<AnimatorBoneMenu>().Anchor;
        var offset = new Vector2(AnimatorLayout.BoneMenuSize.X + 4, 0);
        return boneMenu + offset;
    }

    /// <summary>Adds the picker row the argument indexes, keeping the picker open.</summary>
    private static void AttachSvgFromMenu(Entity session, string argument)
    {
        AnimatorSvgMenu menu = session.GetComponent<AnimatorSvgMenu>();
        if (menu.Choices is null ||
            !int.TryParse(argument, out int index) ||
            index < 0 ||
            index >= menu.Choices.Count)
        {
            return;
        }

        AnimatorSvgChoice choice = menu.Choices[index];
        AnimatorAttachmentCommands.Add(session, choice.Path, choice.Group, menu.Bone);
    }

    /// <summary>Shows the name field over the selected bone; the menu system focuses it.</summary>
    private static void BeginBoneRename(Entity session)    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        session.GetComponent<AnimatorBoneMenu>().Renaming = bone;
        AnimatorSession.SetStatus(session, "TYPE A NAME, ENTER TO RENAME");
    }

    /// <summary>The next bone clicked in the preview becomes the selected bone's parent.</summary>
    private static void BeginBoneReparent(Entity session)
    {
        int bone = SelectedBone(session);
        if (bone < 0)
            return;

        XafBone rig = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[bone];
        if (rig.Parent < 0)
        {
            AnimatorSession.SetStatus(session, "ROOT CANNOT BE REPARENTED");
            return;
        }

        session.GetComponent<AnimatorBoneMenu>().Reparenting = bone;
        AnimatorSession.SetStatus(session, $"CLICK THE NEW PARENT OF {rig.Id}");
    }

    private static int SelectedBone(Entity session)
    {
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        int count = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones.Count;
        return bone >= 0 && bone < count ? bone : -1;
    }

    private static void SelectAttachment(Entity session, string argument)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (!int.TryParse(argument, out int index) ||
            index < 0 ||
            index >= package.Attachments.Count)
        {
            return;
        }

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.Attachment = index;
        selection.Bone = -1;
        AnimatorSession.SetStatus(
            session,
            $"SELECTED {package.Attachments[index].Group}");
    }
}
