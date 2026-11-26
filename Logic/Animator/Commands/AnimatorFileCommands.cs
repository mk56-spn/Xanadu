using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Save, load and new-skeleton commands, the file path and the unsaved-changes state.</summary>
public static class AnimatorFileCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<Save>((s, _) => Save(s));
        session.On<Load>((s, _) => Load(s));
        session.On<NewSkeleton>((s, _) => NewSkeleton(s));
        session.On<CloseEditor>((s, _) => CloseEditor(s));
    }

    public const string Extension = ".xif";

    /// <summary>Trims the path and adds the extension when it has none; empty stays empty.</summary>
    public static string NormalizePath(string path)
    {
        string trimmed = (path ?? "").Trim();
        if (trimmed.Length == 0)
            return "";

        string name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Contains('.') ? trimmed : trimmed + Extension;
    }

    /// <summary>Points Save and Load at another file; with Save this is "save as".</summary>
    public static void SetPath(Entity session, string path)
    {
        string normalized = NormalizePath(path);
        ref AnimatorFile file = ref session.GetComponent<AnimatorFile>();
        if (normalized.Length == 0 || normalized == file.Path)
            return;

        file.Path = normalized;
        AnimatorSession.SetStatus(session, $"FILE {normalized}");
    }

    public static void Save(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        string path = session.GetComponent<AnimatorFile>().Path;
        if (path.Length == 0)
        {
            AnimatorSession.SetStatus(session, "SAVE FAILED: NO FILE PATH");
            return;
        }

        // The file must never hold keys older than the targets beside them.
        RigTargets.BakeAll(document.Package);
        try
        {
            AnimatorFiles.Save(path, document.Package);
        }
        catch (Exception exception) when (exception is RigFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            AnimatorSession.SetStatus(session, $"SAVE FAILED: {exception.Message}");
            return;
        }

        SetBaseline(session);
        AnimatorFiles.DropRecovery(path);
        // A pose left unkeyed is not in the file; say so now, while it can still be keyed.
        int unkeyed = AnimatorUnkeyed.Count(session);
        AnimatorSession.SetStatus(session, unkeyed == 0
            ? $"SAVED {path}"
            : $"SAVED {path} · {AnimatorUnkeyed.Note(unkeyed)}");
    }

    public static void Load(Entity session)
    {
        if (!ConfirmDiscard(session))
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        string path = session.GetComponent<AnimatorFile>().Path;
        RigPackage? package = AnimatorFiles.Load(path);
        if (package is null)
        {
            AnimatorSession.SetStatus(session, $"LOAD FAILED: {path}");
            return;
        }

        AnimatorSessionSetup.Replace(session, package);
        session.SetTag<RestPose>(false);
        AnimatorPreviewCommands.Fit(session);
        SetBaseline(session);
        AnimatorSession.SetStatus(session, $"LOADED {path}");
    }

    public static void NewSkeleton(Entity session)
    {
        if (!ConfirmDiscard(session))
            return;

        AnimatorSessionSetup.Replace(session, RigRigAuthoring.CreateEmptyPackage());
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyBone(0);
        session.SetTag<RestPose>(false);
        AnimatorPreviewCommands.Reset(session);
        SetBaseline(session);
        AnimatorSession.SetStatus(session, "NEW SKELETON");
    }

    /// <summary>Asks the tool to quit, once unsaved changes are saved or confirmed lost.</summary>
    public static void CloseEditor(Entity session)
    {
        if (!ConfirmDiscard(session))
            return;

        session.EmitSignal(new CloseAsked());
    }

    /// <summary>Recomputes whether the package differs from the last saved state.</summary>
    public static void RefreshDirty(Entity session)
    {
        ref AnimatorFile file = ref session.GetComponent<AnimatorFile>();
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        file.Dirty = file.Saved is null || !RigPackageCopy.AreEqual(file.Saved, package);
    }

    /// <summary>
    /// True when it is fine to replace the document. With unsaved changes the
    /// first request only warns; repeating the same request confirms.
    /// </summary>
    private static bool ConfirmDiscard(Entity session)
    {
        ref AnimatorFile file = ref session.GetComponent<AnimatorFile>();
        if (!file.Dirty || file.DiscardArmed)
        {
            if (file.DiscardArmed)
                AnimatorFiles.DropRecovery(file.Path);
            file.DiscardArmed = false;
            return true;
        }

        file.DiscardArmed = true;
        AnimatorSession.SetStatus(session, "UNSAVED CHANGES: PRESS AGAIN TO DISCARD, OR SAVE");
        return false;
    }

    /// <summary>Takes the current package as the saved state.</summary>
    private static void SetBaseline(Entity session)
    {
        ref AnimatorFile file = ref session.GetComponent<AnimatorFile>();
        file.Saved = RigPackageCopy.Clone(session.GetComponent<AnimatorDocument>().Package);
        file.Dirty = false;
        file.DiscardArmed = false;
    }

    /// <summary>The saved file's package, or the placeholder rig when there is none.</summary>
    public static RigPackage LoadOrDefault(string path) =>
        AnimatorFiles.Load(path) ?? AnimatorDefaultPackage.Create();
}
