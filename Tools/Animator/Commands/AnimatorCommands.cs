using Friflo.Engine.ECS;
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
