using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Create, copy, remove, rename, resize and loop the clips of the package.</summary>
public static class AnimatorClipCommands
{
    public const float DurationStep = 0.1f;

    /// <summary>Length changes for the one, two and three arrow step buttons.</summary>
    public static readonly float[] LengthSteps = { 0.1f, 1f, 10f };

    /// <summary>
    /// Creates the clip the NEW CLIP dialog describes. False when the name is
    /// empty or already used; the caller reports why.
    /// </summary>
    public static bool Create(Entity session, string name, float duration, bool duplicate)
    {
        string trimmed = (name ?? "").Trim();
        XafPackage package = Package(session);
        if (trimmed.Length == 0 || package.Clips.Exists(clip => clip.Id == trimmed))
            return false;

        bool created = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafClip? source = duplicate ? Current(session) : null;
            XafClip clip = source is null
                ? XafClipAuthoring.Add(package, trimmed)
                : XafClipAuthoring.Duplicate(package, source);
            if (!XafClipAuthoring.Rename(package, clip, trimmed))
                return;

            float actual = XafClipAuthoring.SetDuration(clip, duration);
            Show(session, clip.Id);
            created = true;
            AnimatorSession.SetStatus(session, $"NEW CLIP {clip.Id} {actual:0.00}s");
        });
        return created;
    }

    public static void Duplicate(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafClip? source = Current(session);
            if (source is null)
                return;

            XafClip copy = XafClipAuthoring.Duplicate(Package(session), source);
            Show(session, copy.Id);
            AnimatorSession.SetStatus(session, $"COPIED TO {copy.Id}");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = Package(session);
            XafClip? clip = Current(session);
            if (clip is null)
                return;

            int index = package.Clips.IndexOf(clip);
            if (!XafClipAuthoring.Delete(package, clip))
            {
                AnimatorSession.SetStatus(session, "CANNOT DELETE THE LAST CLIP");
                return;
            }

            Show(session, package.Clips[System.Math.Clamp(index - 1, 0, package.Clips.Count - 1)].Id);
            AnimatorSession.SetStatus(session, $"DELETED {clip.Id}");
        });

    /// <summary>Renames the clip with the given id; false when the name is empty or taken.</summary>
    public static bool Rename(Entity session, string clipId, string newId)
    {
        bool renamed = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = Package(session);
            XafClip? clip = XafAuthoring.FindClip(package, clipId);
            if (clip is null || !XafClipAuthoring.Rename(package, clip, newId))
                return;

            renamed = true;
            bool wasCurrent = session.GetComponent<AnimatorDocument>().Runtime.CurrentClip == clipId;
            AnimatorSession.RigChanged(session, wasCurrent ? clip.Id : null);
            AnimatorSession.SetStatus(session, $"RENAMED TO {clip.Id}");
        });

        if (!renamed)
            AnimatorSession.SetStatus(session, "NAME EMPTY OR ALREADY USED");
        return renamed;
    }

    public static void StepDuration(Entity session, float delta) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafClip? clip = Current(session);
            if (clip is null)
                return;

            float wanted = clip.Duration + delta;
            float actual = XafClipAuthoring.SetDuration(clip, wanted);
            AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
            if (runtime.CurrentTime > actual)
                runtime.Seek(actual);

            AnimatorSession.SetStatus(
                session,
                actual > wanted + 0.0001f && delta < 0
                    ? $"LENGTH {actual:0.00}s (KEYS END THERE)"
                    : $"LENGTH {actual:0.00}s");
        });

    /// <summary>Sets the current clip's length, clamped to at least its last key.</summary>
    public static void SetDuration(Entity session, float seconds) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafClip? clip = Current(session);
            if (clip is null)
                return;

            float actual = XafClipAuthoring.SetDuration(clip, seconds);
            AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
            if (runtime.CurrentTime > actual)
                runtime.Seek(actual);

            AnimatorSession.SetStatus(
                session,
                actual > seconds + 0.0001f
                    ? $"LENGTH {actual:0.00}s (KEYS END THERE)"
                    : $"LENGTH {actual:0.00}s");
        });

    public static void ToggleLoop(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafClip? clip = Current(session);
            if (clip is null)
                return;

            XafClipAuthoring.SetLoop(clip, !clip.Loop);
            AnimatorSession.SetStatus(session, $"LOOP {(clip.Loop ? "ON" : "OFF")}");
        });

    private static XafPackage Package(Entity session) =>
        session.GetComponent<AnimatorDocument>().Package;

    private static XafClip? Current(Entity session) =>
        AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());

    /// <summary>Rebuilds the runtime for the changed clip list and shows a clip from its start.</summary>
    private static void Show(Entity session, string clipId)
    {
        AnimatorSession.RigChanged(session, clipId);
        session.GetComponent<AnimatorDocument>().Runtime.Seek(0f);
        session.GetComponent<AnimatorSelection>().HasKey = false;
    }
}
