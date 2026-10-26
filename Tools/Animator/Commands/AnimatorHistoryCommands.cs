using System;
using Friflo.Engine.ECS;
using Writer.Animation;
using Writer.History;

namespace Writer.Tools.Animator.Commands;

/// <summary>
/// Undo/redo for package edits. Single-shot edits go through <see cref="Edit"/>;
/// drags bracket their frames with <see cref="BeginGesture"/> and
/// <see cref="EndGesture"/> so the whole drag is one step.
/// </summary>
public static class AnimatorHistoryCommands
{
    public static UndoHistory<XafPackage> CreateHistory(Entity session) =>
        new(XafPackageCopy.AreEqual, snapshot => Restore(session, snapshot));

    /// <summary>Runs an edit and records it; nested edits record once, as the outermost.</summary>
    public static void Edit(Entity session, Action edit)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorHistory>();
        if (history.Depth > 0)
        {
            edit();
            return;
        }

        XafPackage before = Snapshot(session);
        history.Depth++;
        try
        {
            edit();
        }
        finally
        {
            history.Depth--;
        }

        history.History.Record(before, Snapshot(session));
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void BeginGesture(Entity session) =>
        session.GetComponent<AnimatorHistory>().Gesture = Snapshot(session);

    public static void EndGesture(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorHistory>();
        if (history.Gesture is not { } before)
            return;

        history.Gesture = null;
        history.History.Record(before, Snapshot(session));
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Undo(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorHistory>();
        history.Gesture = null;
        AnimatorSession.SetStatus(session, history.History.Undo() ? "UNDO" : "NOTHING TO UNDO");
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Redo(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorHistory>();
        history.Gesture = null;
        AnimatorSession.SetStatus(session, history.History.Redo() ? "REDO" : "NOTHING TO REDO");
        AnimatorFileCommands.RefreshDirty(session);
    }

    public static void Clear(Entity session)
    {
        ref AnimatorHistory history = ref session.GetComponent<AnimatorHistory>();
        history.History.Clear();
        history.Gesture = null;
    }

    private static XafPackage Snapshot(Entity session) =>
        XafPackageCopy.Clone(session.GetComponent<AnimatorDocument>().Package);

    /// <summary>Puts a stored snapshot back as the live package, as a copy so the history keeps its own.</summary>
    private static void Restore(Entity session, XafPackage snapshot)
    {
        session.GetComponent<AnimatorDocument>().Package = XafPackageCopy.Clone(snapshot);
        AnimatorSession.RigChanged(session);

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.ClampBones(package.Skeleton.Bones.Count);
        if (selection.Attachment >= package.Attachments.Count)
            selection.Attachment = -1;
        selection.HasKey = false;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
    }
}
