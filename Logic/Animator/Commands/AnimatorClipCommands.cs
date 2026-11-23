using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Create, copy, remove, rename, resize and loop the clips of the package.</summary>
public static class AnimatorClipCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<DuplicateClip>((s, _) => Duplicate(s));
        session.On<DeleteClip>((s, _) => Delete(s));
        session.On<ClearClipKeys>((s, _) => ClearKeys(s));
        session.On<StepClipLength>((s, e) => StepDuration(s, e.Direction * LengthStep(e.Size)));
        session.On<ToggleLoop>((s, _) => ToggleLoop(s));
    }

    public const float DurationStep = 0.1f;

    /// <summary>Length changes for the one, two and three arrow step buttons.</summary>
    public static readonly float[] LengthSteps = [0.1f, 1f, 10f];

    /// <summary>The step a length button asks for: <see cref="LengthSteps"/>[arrows - 1], or <see cref="DurationStep"/> for a shortcut (0).</summary>
    public static float LengthStep(int arrows) =>
        arrows >= 1 && arrows <= LengthSteps.Length ? LengthSteps[arrows - 1] : DurationStep;

    /// <summary>
    /// Creates the clip the NEW CLIP dialog describes. False when the name is
    /// empty or already used; the caller reports why.
    /// </summary>
    public static bool Create(Entity session, string name, float duration, bool duplicate)
    {
        string trimmed = (name ?? "").Trim();
        RigPackage package = Package(session);
        if (trimmed.Length == 0 || package.Clips.Exists(clip => clip.Id == trimmed))
            return false;

        bool created = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigClip? source = duplicate ? Current(session) : null;
            RigClip clip = source is null
                ? RigClipAuthoring.Add(package, trimmed)
                : RigClipAuthoring.Duplicate(package, source);
            if (!RigClipAuthoring.Rename(package, clip, trimmed))
                return;

            float actual = RigClipAuthoring.SetDuration(clip, duration);
            Show(session, clip.Id);
            created = true;
            AnimatorSession.SetStatus(session, $"NEW CLIP {clip.Id} {actual:0.00}s");
        });
        return created;
    }

    public static void Duplicate(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigClip? source = Current(session);
            if (source is null)
                return;

            RigClip copy = RigClipAuthoring.Duplicate(Package(session), source);
            Show(session, copy.Id);
            AnimatorSession.SetStatus(session, $"COPIED TO {copy.Id}");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = Package(session);
            RigClip? clip = Current(session);
            if (clip is null)
                return;

            int index = package.Clips.IndexOf(clip);
            if (!RigClipAuthoring.Delete(package, clip))
            {
                AnimatorSession.SetStatus(session, "CANNOT DELETE THE LAST CLIP");
                return;
            }

            Show(session, package.Clips[System.Math.Clamp(index - 1, 0, package.Clips.Count - 1)].Id);
            AnimatorSession.SetStatus(session, $"DELETED {clip.Id}");
        });

    /// <summary>
    /// Drops every key of the current clip, so it plays the rest pose again.
    /// Its length, loop, events and limb paths stay. One undo step.
    /// </summary>
    public static void ClearKeys(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigClip? clip = Current(session);
            if (clip is null)
                return;

            int count = 0;
            foreach (var channel in clip.Channels)
                count += channel.Keys.Count;
            clip.Channels.Clear();
            AnimatorSession.SetStatus(session, count == 0 ? $"{clip.Id} HAS NO KEYS" : $"CLEARED {count} KEYS FROM {clip.Id}");
        });

    /// <summary>Renames the clip with the given id; false when the name is empty or taken.</summary>
    public static bool Rename(Entity session, string clipId, string newId)
    {
        bool renamed = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = Package(session);
            RigClip? clip = RigAuthoring.FindClip(package, clipId);
            if (clip is null || !RigClipAuthoring.Rename(package, clip, newId))
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
            RigClip? clip = Current(session);
            if (clip is null)
                return;

            float wanted = clip.Duration + delta;
            float actual = RigClipAuthoring.SetDuration(clip, wanted);
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
            RigClip? clip = Current(session);
            if (clip is null)
                return;

            float actual = RigClipAuthoring.SetDuration(clip, seconds);
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
            RigClip? clip = Current(session);
            if (clip is null)
                return;

            RigClipAuthoring.SetLoop(clip, !clip.Loop);
            AnimatorSession.SetStatus(session, $"LOOP {(clip.Loop ? "ON" : "OFF")}");
        });

    private static RigPackage Package(Entity session) =>
        session.GetComponent<AnimatorDocument>().Package;

    private static RigClip? Current(Entity session) =>
        AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());

    /// <summary>Rebuilds the runtime for the changed clip list and shows a clip from its start.</summary>
    private static void Show(Entity session, string clipId)
    {
        AnimatorSession.RigChanged(session, clipId);
        session.GetComponent<AnimatorDocument>().Runtime.Seek(0f);
        session.GetComponent<AnimatorSelection>().HasKey = false;
    }
}
