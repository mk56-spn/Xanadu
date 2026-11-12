using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.History;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Undo/redo for package and pose edits. Single-shot edits go through <see cref="Edit"/>;
/// drags bracket their frames with <see cref="BeginGesture"/> and
/// <see cref="EndGesture"/> so the whole drag is one step.
/// </summary>
public static class AnimatorHistoryCommands
{
    public static UndoHistory<AnimatorSnapshot> CreateHistory(Entity session) =>
        new(AreEqual, snapshot => Restore(session, snapshot));

    /// <summary>Runs an edit and records it; nested edits record once, as the outermost.</summary>
    public static void Edit(Entity session, Action edit)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        if (history.Depth > 0)
        {
            edit();
            return;
        }

        AnimatorSnapshot before = Snapshot(session);
        history.Depth++;
        try
        {
            edit();
        }
        finally
        {
            history.Depth--;
        }

        AnimatorTargetCommands.AfterEdit(session, before.Package);
        history.History.Record(before, Snapshot(session));
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void BeginGesture(Entity session) =>
        session.GetComponent<AnimatorSessionState>().History.Gesture = Snapshot(session);

    /// <summary>
    /// Runs an edit as part of the open gesture: it is not recorded on its own,
    /// since <see cref="EndGesture"/> records the whole gesture as one step.
    /// </summary>
    public static void DuringGesture(Entity session, Action edit)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        history.Depth++;
        try
        {
            edit();
        }
        finally
        {
            history.Depth--;
        }
    }

    /// <summary>Records the drag; edits made by <paramref name="last"/> land in the same step.</summary>
    public static void EndGesture(Entity session, Action? last = null)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        if (history.Gesture is not { } before)
        {
            if (last is not null)
                Edit(session, last);
            return;
        }

        history.Gesture = null;
        history.Depth++;
        try
        {
            last?.Invoke();
        }
        finally
        {
            history.Depth--;
        }

        AnimatorTargetCommands.AfterEdit(session, before.Package);
        history.History.Record(before, Snapshot(session));
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Undo(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        history.Gesture = null;
        AnimatorSession.SetStatus(session, history.History.Undo() ? "UNDO" : "NOTHING TO UNDO");
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Redo(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        history.Gesture = null;
        AnimatorSession.SetStatus(session, history.History.Redo() ? "REDO" : "NOTHING TO REDO");
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Clear(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorSessionState>().History;
        history.History.Clear();
        history.Gesture = null;
    }

    private static AnimatorSnapshot Snapshot(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        return new AnimatorSnapshot(
            XafPackageCopy.Clone(document.Package),
            document.Runtime.Pose.Clone(),
            document.Runtime.CurrentClip,
            document.Runtime.CurrentTime);
    }

    private static bool AreEqual(AnimatorSnapshot before, AnimatorSnapshot after) =>
        XafPackageCopy.AreEqual(before.Package, after.Package) && SamePose(before.Pose, after.Pose);

    private static bool SamePose(XafPose a, XafPose b) =>
        a.Bones.AsSpan().SequenceEqual(b.Bones) &&
        a.Attachments.AsSpan().SequenceEqual(b.Attachments) &&
        a.AttachmentStates.AsSpan().SequenceEqual(b.AttachmentStates) &&
        a.Effects.AsSpan().SequenceEqual(b.Effects);

    /// <summary>
    /// Puts a stored snapshot back, copying so the history keeps its own. A step
    /// that only changed the pose returns to the clip and time it was made at,
    /// because that pose means nothing elsewhere; a package edit keeps the
    /// playhead, and gets its pose back only if the playhead has not moved.
    /// </summary>
    private static void Restore(Entity session, AnimatorSnapshot snapshot)
    {
        bool poseOnly = XafPackageCopy.AreEqual(session.GetComponent<AnimatorDocument>().Package, snapshot.Package);
        if (!poseOnly)
        {
            session.GetComponent<AnimatorDocument>().Package = XafPackageCopy.Clone(snapshot.Package);
            AnimatorSession.RigChanged(session);
        }

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        if (poseOnly)
        {
            runtime.Play(snapshot.Clip, 0f);
            runtime.Seek(snapshot.Time);
            runtime.SetPaused(true);
        }

        if (runtime.CurrentClip == snapshot.Clip && runtime.CurrentTime == snapshot.Time)
            CopyPose(snapshot.Pose, runtime.Pose);

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.ClampBones(package.Skeleton.Bones.Count);
        if (selection.Attachment >= package.Attachments.Count)
            selection.Attachment = -1;
        if (selection.Chain >= package.Chains.Count)
            selection.Chain = -1;
        if (selection.Effect >= package.Effects.Count)
            selection.Effect = -1;
        selection.HasKey = false;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
    }

    private static void CopyPose(XafPose from, XafPose to)
    {
        if (from.Bones.Length != to.Bones.Length ||
            from.Attachments.Length != to.Attachments.Length ||
            from.Effects.Length != to.Effects.Length)
            return;

        from.Bones.CopyTo(to.Bones, 0);
        from.Attachments.CopyTo(to.Attachments, 0);
        from.AttachmentStates.CopyTo(to.AttachmentStates, 0);
        from.Effects.CopyTo(to.Effects, 0);
    }
}
