using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Executes <see cref="ToolAction"/> requests against the session entity.</summary>
public static class AnimatorCommands
{
    public static void Run(Entity session, ToolAction action)
    {
        // Any other action withdraws a pending "discard unsaved changes?" question.
        if (action.Kind is not (ToolActionKind.Load or ToolActionKind.NewSkeleton))
            session.GetComponent<AnimatorFile>().DiscardArmed = false;

        // And a chain button waiting for a bone click: pressing anything else drops it.
        if (action.Kind is not (ToolActionKind.PickChainBone or ToolActionKind.PickColliderBone))
            session.GetComponent<AnimatorChains>().Picking = AnimatorChainPick.None;
        if (action.Kind is not (ToolActionKind.PickClothBone or ToolActionKind.PinClothPoints or
            ToolActionKind.PickClothColliders))
        {
            session.GetComponent<AnimatorCloths>().Picking = AnimatorClothPick.None;
        }

        switch (action.Kind)
        {
            case ToolActionKind.Play:
                session.GetComponent<AnimatorDocument>().Playback.Play();
                AnimatorSession.SetStatus(session, "PLAYING");
                break;
            case ToolActionKind.Stop:
                AnimatorSequenceCommands.Finish(session);
                session.GetComponent<AnimatorDocument>().Playback.Stop();
                AnimatorSession.SetStatus(session, "STOPPED");
                break;
            case ToolActionKind.SequenceGame:
                AnimatorSequenceCommands.StartGame(session);
                break;
            case ToolActionKind.SequenceAll:
                AnimatorSequenceCommands.StartAll(session);
                break;
            case ToolActionKind.AllowTransition:
                AnimatorFlowCommands.Toggle(session, action.Argument);
                break;
            case ToolActionKind.SelectTransition:
                AnimatorFlowCommands.Select(session, action.Argument);
                break;
            case ToolActionKind.TransitionFadeDown:
                AnimatorFlowCommands.StepFade(session, -XafTransitions.FadeStep);
                break;
            case ToolActionKind.TransitionFadeUp:
                AnimatorFlowCommands.StepFade(session, XafTransitions.FadeStep);
                break;
            case ToolActionKind.TransitionCurve:
                AnimatorFlowCommands.SetCurve(session, action.Argument);
                break;
            case ToolActionKind.PreviewTransition:
                AnimatorFlowCommands.Preview(session);
                break;
            case ToolActionKind.ToggleFlowLoop:
                AnimatorFlowCommands.ToggleLoop(session);
                break;
            case ToolActionKind.ToggleSlow:
                AnimatorFlowCommands.ToggleSlow(session);
                break;
            case ToolActionKind.AddEffect:
                AnimatorEffectCommands.Add(session);
                break;
            case ToolActionKind.SelectEffect:
                if (int.TryParse(action.Argument, out int effect))
                    AnimatorEffectCommands.Select(session, effect);
                break;
            case ToolActionKind.DeleteEffect:
                AnimatorEffectCommands.Delete(session);
                break;
            case ToolActionKind.ToggleEffectVisible:
                AnimatorEffectCommands.ToggleVisible(session);
                break;
            case ToolActionKind.EffectOpacityDown:
                AnimatorEffectCommands.StepOpacity(session, -AnimatorEffectCommands.OpacityStep);
                break;
            case ToolActionKind.EffectOpacityUp:
                AnimatorEffectCommands.StepOpacity(session, AnimatorEffectCommands.OpacityStep);
                break;
            case ToolActionKind.Clip:
                PlayClip(session, action.Argument);
                break;
            case ToolActionKind.TogglePreviewMode:
                AnimatorPreviewModeCommands.Set(session, !session.GetComponent<AnimatorPreviewMode>().On);
                break;
            case ToolActionKind.ExitPreviewMode:
                AnimatorPreviewModeCommands.Set(session, false);
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
            case ToolActionKind.OpenNewClip:
                AnimatorNewClipDialogCommands.Open(
                    session,
                    action.Argument == "duplicate");
                break;
            case ToolActionKind.CreateClip:
                AnimatorNewClipDialogCommands.Create(session);
                break;
            case ToolActionKind.CloseNewClip:
                AnimatorNewClipDialogCommands.Close(session);
                break;
            case ToolActionKind.OpenInfo:
                session.GetComponent<AnimatorInfoPanel>().Open = true;
                break;
            case ToolActionKind.CloseInfo:
                session.GetComponent<AnimatorInfoPanel>().Open = false;
                break;
            case ToolActionKind.TogglePathWindow:
                ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
                modes.PathWindow = !modes.PathWindow;
                AnimatorSession.SetStatus(session, modes.PathWindow
                    ? "MOTION PATHS: ONE SECOND AROUND THE PLAYHEAD"
                    : "MOTION PATHS: WHOLE CLIP");
                break;
            case ToolActionKind.OpenClipLength:
                session.GetComponent<AnimatorClipLength>().Open = true;
                AnimatorSession.SetStatus(
                    session,
                    "TYPE A LENGTH OR STEP WITH THE ARROWS");
                break;
            case ToolActionKind.DuplicateClip:
                AnimatorClipCommands.Duplicate(session);
                break;
            case ToolActionKind.DeleteClip:
                AnimatorClipCommands.Delete(session);
                break;
            case ToolActionKind.DurationDown:
                AnimatorClipCommands.StepDuration(session, -DurationStep(action.Argument));
                break;
            case ToolActionKind.DurationUp:
                AnimatorClipCommands.StepDuration(session, DurationStep(action.Argument));
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
            case ToolActionKind.AddChain:
                AnimatorChainCommands.Add(session);
                break;
            case ToolActionKind.SelectChain:
                if (int.TryParse(action.Argument, out int chain))
                    AnimatorChainCommands.Select(session, chain);
                break;
            case ToolActionKind.DeleteChain:
                AnimatorChainCommands.Delete(session);
                break;
            case ToolActionKind.PickChainArt:
                AnimatorChainPlacementCommands.OpenArtPicker(session);
                break;
            case ToolActionKind.PickChainBone:
                AnimatorChainPlacementCommands.BeginPick(session, AnimatorChainPick.Anchor);
                break;
            case ToolActionKind.PickColliderBone:
                AnimatorChainPlacementCommands.BeginPick(session, AnimatorChainPick.Collider);
                break;
            case ToolActionKind.AddCloth:
                AnimatorClothCommands.Add(session);
                break;
            case ToolActionKind.SelectCloth:
                if (int.TryParse(action.Argument, out int cloth))
                    AnimatorClothCommands.Select(session, cloth);
                break;
            case ToolActionKind.DeleteCloth:
                AnimatorClothCommands.Delete(session);
                break;
            case ToolActionKind.PickClothBone:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Bone);
                break;
            case ToolActionKind.PinClothPoints:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Pins);
                break;
            case ToolActionKind.PickClothColliders:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Colliders);
                break;
            case ToolActionKind.TogglePlay:
                TogglePlay(session);
                break;
            case ToolActionKind.ToggleMode:
                ToggleMode(session, action.Argument);
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
            case ToolActionKind.FitPreview:
                AnimatorPreviewCommands.Fit(session);
                break;
            case ToolActionKind.ResetPreview:
                AnimatorPreviewCommands.Reset(session);
                break;
            case ToolActionKind.ResetTransform:
                AnimatorTransformCommands.ResetSelected(session);
                break;
            case ToolActionKind.CopyKeys:
                AnimatorKeyClipboardCommands.Copy(session);
                break;
            case ToolActionKind.PasteKeys:
                AnimatorKeyClipboardCommands.Paste(session);
                break;
            case ToolActionKind.SelectAllKeys:
                AnimatorKeyClipboardCommands.SelectAll(session);
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
        selection.SelectOnlyBone(index);
        AnimatorSession.SetStatus(session, $"SELECTED {boneId}");
    }

    /// <summary>Adds a key at the row and time the key menu was opened on.</summary>
    private static void AddKeyFromMenu(Entity session)
    {
        AnimatorKeyMenu menu = session.GetComponent<AnimatorKeyMenu>();
        AnimatorKeyCommands.AddKeyAt(
            session, new AnimatorTrackRow(menu.Track, menu.Property), menu.Time);
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
        menu.ForChain = false;
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
        if (menu.ForChain)
            AnimatorChainPlacementCommands.SetArt(session, choice);
        else
            AnimatorAttachmentCommands.Add(session, choice.Path, choice.Group, menu.Bone);
    }

    /// <summary>Plays if stopped and stops if playing, the one action behind Space.</summary>
    private static void TogglePlay(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (document.Playback.IsPlaying)
        {
            document.Playback.Stop();
            AnimatorSession.SetStatus(session, "STOPPED");
        }
        else
        {
            document.Playback.Play();
            AnimatorSession.SetStatus(session, "PLAYING");
        }
    }

    /// <summary>Flips one editing mode by name, as clicking its toggle does.</summary>
    private static void ToggleMode(Entity session, string argument)
    {
        if (!System.Enum.TryParse(argument, out AnimatorMode mode))
            return;

        ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
        bool value = !modes.Get(mode);
        modes.Set(mode, value);
        AnimatorModeSystem.Announce(session, mode, value);
    }

    /// <summary>Shows the name field over the selected bone; the menu system focuses it.</summary>
    private static void BeginBoneRename(Entity session)
    {
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

    /// <summary>The step a length arrow asks for, or the default 0.1 s for a keybinding.</summary>
    private static float DurationStep(string argument) =>
        float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture,
            out float seconds) && seconds > 0f
            ? seconds
            : AnimatorClipCommands.DurationStep;

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
        selection.SelectOnlyAttachment(index);
        AnimatorSession.SetStatus(
            session,
            $"SELECTED {package.Attachments[index].Group}");
    }
}
