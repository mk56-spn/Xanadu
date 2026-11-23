using Friflo.Engine.ECS;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>Reads the single entity that holds animator editing state; <c>AnimatorSessionSetup</c> creates it.</summary>
public static class AnimatorSession
{
    public static Entity Find(EntityStore store)
    {
        foreach (Entity entity in store.Query<AnimatorDocument>().Entities)
            return entity;
        return default;
    }

    public static void SetStatus(Entity session, string message)
    {
        ref AnimatorStatus status = ref session.GetComponent<AnimatorStatus>();
        status.Message = message;
        status.Serial++;
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

    public static void Restart(ref AnimatorDocument document, string preferredClip)
    {
        document.Runtime = new AnimatorRuntime(document.Package);
        document.Playback.Replace(document.Runtime);
        StartClip(document.Runtime, preferredClip);
        document.Version++;
    }

    /// <summary>Plays the preferred clip, else "idle", else the first clip, if any.</summary>
    public static void StartClip(AnimatorRuntime runtime, string preferredClip)
    {
        if (runtime.Play(preferredClip, 0f) || runtime.Play("idle", 0f))
            return;

        if (runtime.Package.Clips.Count > 0)
            runtime.Play(runtime.Package.Clips[0].Id, 0f);
    }

    /// <summary>The clip currently driving the runtime, if any.</summary>
    public static RigClip? CurrentClip(in AnimatorDocument document)
    {
        string id = document.Runtime.CurrentClip;
        foreach (RigClip clip in document.Package.Clips)
        {
            if (clip.Id == id)
                return clip;
        }
        return null;
    }
}
