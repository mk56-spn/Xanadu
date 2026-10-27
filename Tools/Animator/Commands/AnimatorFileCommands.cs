using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Save, load and new-skeleton commands, the file path and the unsaved-changes state.</summary>
public static class AnimatorFileCommands
{
    public const string Extension = ".xaf";

    /// <summary>Trims the path and adds the extension when it has none; empty stays empty.</summary>
    public static string NormalizePath(string path)
    {
        string trimmed = (path ?? "").Trim();
        if (trimmed.Length == 0)
            return "";

        string name = trimmed[(trimmed.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];
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

        try
        {
            document.Files.Save(path, document.Package);
        }
        catch (Exception exception) when (exception is XafFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            AnimatorSession.SetStatus(session, $"SAVE FAILED: {exception.Message}");
            return;
        }

        SetBaseline(session);
        AnimatorSession.SetStatus(session, $"SAVED {path}");
    }

    public static void Load(Entity session)
    {
        if (!ConfirmDiscard(session))
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        string path = session.GetComponent<AnimatorFile>().Path;
        XafPackage? package = document.Files.Load(path);
        if (package is null)
        {
            AnimatorSession.SetStatus(session, $"LOAD FAILED: {path}");
            return;
        }

        AnimatorSession.Replace(session, package);
        session.GetComponent<AnimatorModes>().Rest = false;
        AnimatorPreviewCommands.Fit(session);
        SetBaseline(session);
        AnimatorSession.SetStatus(session, $"LOADED {path}");
    }

    public static void NewSkeleton(Entity session)
    {
        if (!ConfirmDiscard(session))
            return;

        AnimatorSession.Replace(session, XafRigAuthoring.CreateEmptyPackage());
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyBone(0);
        session.GetComponent<AnimatorModes>().Rest = false;
        AnimatorPreviewCommands.Reset(session);
        SetBaseline(session);
        AnimatorSession.SetStatus(session, "NEW SKELETON");
    }

    /// <summary>Recomputes whether the package differs from the last saved state.</summary>
    public static void RefreshDirty(Entity session)
    {
        ref AnimatorFile file = ref session.GetComponent<AnimatorFile>();
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        file.Dirty = file.Saved is null || !XafPackageCopy.AreEqual(file.Saved, package);
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
        file.Saved = XafPackageCopy.Clone(session.GetComponent<AnimatorDocument>().Package);
        file.Dirty = false;
        file.DiscardArmed = false;
    }

    /// <summary>The saved file's package, or the placeholder rig when there is none.</summary>
    public static XafPackage LoadOrDefault(IAnimatorFiles files, string path) =>
        files.Load(path) ?? AnimatorDefaultPackage.Create();
}
