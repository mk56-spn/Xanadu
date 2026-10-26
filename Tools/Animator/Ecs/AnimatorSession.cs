using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Animation;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Files;

namespace Writer.Tools.Animator;

/// <summary>Creates and reads the single entity that holds animator editing state.</summary>
public static class AnimatorSession
{
    public static Entity Create(
        EntityStore store,
        XafPackage package,
        IAnimatorFiles files,
        string path)
    {
        var runtime = new AnimatorRuntime(package);
        StartClip(runtime, "idle");
        Entity session = store.CreateEntity(
            new AnimatorDocument
            {
                Package = package,
                Runtime = runtime,
                Playback = new AnimatorPlayback(runtime),
                Files = files,
            },
            AnimatorSelection.None,
            new AnimatorModes(),
            new AnimatorDrag(),
            new AnimatorStatus { Message = "" },
            new AnimatorFile
            {
                Path = path,
                Saved = XafPackageCopy.Clone(package),
                Dirty = false,
            },
            new TimelineView(1f),
            new AnimatorTracks(),
            AnimatorBoneMenu.Closed);
        session.AddComponent(new AnimatorHistory
        {
            History = AnimatorHistoryCommands.CreateHistory(session),
        });
        return session;
    }

    public static Entity Find(EntityStore store)
    {
        foreach (Entity entity in store.Query<AnimatorDocument>().ToEntityList())
            return entity;
        return default;
    }

    public static void SetStatus(Entity session, string message) =>
        session.GetComponent<AnimatorStatus>().Message = message;

    /// <summary>Swaps in a new package and resets selection and playback to match.</summary>
    public static void Replace(Entity session, XafPackage package)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        document.Package = package;
        Restart(ref document, "idle");
        session.GetComponent<AnimatorSelection>() = AnimatorSelection.None;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
        session.GetComponent<AnimatorBoneMenu>() = AnimatorBoneMenu.Closed;
        AnimatorHistoryCommands.Clear(session);
    }

    /// <summary>
    /// Rebuilds the runtime after bones or attachments were added or removed,
    /// since its pose arrays are sized to the rig. Clip, time and pause state
    /// are kept; unkeyed pose tweaks are discarded.
    /// </summary>
    public static void RigChanged(Entity session, string? preferredClip = null)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorRuntime old = document.Runtime;
        string clip = preferredClip ?? old.CurrentClip;
        float time = old.CurrentTime;
        bool paused = old.Paused;

        Restart(ref document, clip);
        document.Runtime.Seek(time);
        document.Runtime.SetPaused(paused);
    }

    private static void Restart(ref AnimatorDocument document, string preferredClip)
    {
        document.Runtime = new AnimatorRuntime(document.Package);
        document.Playback.Replace(document.Runtime);
        StartClip(document.Runtime, preferredClip);
        document.Version++;
    }

    /// <summary>Plays the preferred clip, else "idle", else the first clip, if any.</summary>
    private static void StartClip(AnimatorRuntime runtime, string preferredClip)
    {
        if (runtime.Play(preferredClip, 0f) || runtime.Play("idle", 0f))
            return;

        if (runtime.Package.Clips.Count > 0)
            runtime.Play(runtime.Package.Clips[0].Id, 0f);
    }

    /// <summary>The clip currently driving the runtime, if any.</summary>
    public static XafClip? CurrentClip(in AnimatorDocument document)
    {
        string id = document.Runtime.CurrentClip;
        foreach (XafClip clip in document.Package.Clips)
        {
            if (clip.Id == id)
                return clip;
        }
        return null;
    }
}
