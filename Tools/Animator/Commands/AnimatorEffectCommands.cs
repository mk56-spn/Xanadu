using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Effects in the animator: adding a streak to a bone, selecting and deleting
/// effects, and keying when they show and how opaque they are.
/// </summary>
public static class AnimatorEffectCommands
{
    /// <summary>One press of the opacity buttons.</summary>
    public const float OpacityStep = 0.25f;

    /// <summary>The selected effect, if it still exists.</summary>
    public static int Selected(Entity session)
    {
        int effect = session.GetComponent<AnimatorSelection>().Effect;
        int count = session.GetComponent<AnimatorDocument>().Package.Effects.Count;
        return effect >= 0 && effect < count ? effect : -1;
    }

    /// <summary>
    /// Hangs a streak off the selected bone and keys it shown at the playhead
    /// and hidden <see cref="XafEffects.DefaultShowSeconds"/> later, so it is a
    /// visible flash in the clip from the start.
    /// </summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            int bone = session.GetComponent<AnimatorSelection>().Bone;
            if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
                return;
            }

            int index = XafEffects.Add(package, package.Skeleton.Bones[bone].Id, XafEffects.Streak(), "streak");
            AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
            float time = runtime.CurrentTime;
            if (AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is { } clip &&
                !session.GetComponent<AnimatorModes>().Rest)
            {
                XafAuthoring.KeyEffectVisible(package, clip, index, time, true);
                XafAuthoring.KeyEffectVisible(package, clip, index,
                    Math.Min(clip.Duration, time + XafEffects.DefaultShowSeconds), false);
            }

            AnimatorSession.RigChanged(session);
            session.GetComponent<AnimatorSelection>().SelectOnlyEffect(index);
            AnimatorSession.SetStatus(session, $"ADDED {package.Effects[index].Id.ToUpperInvariant()} ON {package.Skeleton.Bones[bone].Id}");
        });

    public static void Select(Entity session, int index)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Effects.Count)
            return;

        session.GetComponent<AnimatorSelection>().SelectOnlyEffect(index);
        AnimatorSession.SetStatus(session, $"SELECTED {package.Effects[index].Id}");
    }

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session);
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT AN EFFECT FIRST");
                return;
            }

            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            string id = package.Effects[index].Id;
            XafEffects.Remove(package, index);
            session.GetComponent<AnimatorSelection>().Effect = -1;
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"DELETED {id}");
        });

    /// <summary>Keys the selected effect the other way from how it is showing at the playhead.</summary>
    public static void ToggleVisible(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryKeyTarget(session, out int index, out XafClip clip))
                return;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            bool showing = document.Runtime.Pose.Effects[index].Visible;
            XafAuthoring.KeyEffectVisible(document.Package, clip, index, document.Runtime.CurrentTime, !showing);
            Resample(session);
            AnimatorSession.SetStatus(session,
                $"{document.Package.Effects[index].Id} {(showing ? "HIDDEN" : "SHOWN")} AT {document.Runtime.CurrentTime:0.00} S");
        });

    public static void StepOpacity(Entity session, float delta) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryKeyTarget(session, out int index, out XafClip clip))
                return;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            XafEffectState state = document.Runtime.Pose.Effects[index];
            state.Opacity = Math.Clamp(MathF.Round((state.Opacity + delta) / OpacityStep) * OpacityStep, 0f, 1f);
            XafAuthoring.KeyEffect(document.Package, clip, index, document.Runtime.CurrentTime, state);
            Resample(session);
            AnimatorSession.SetStatus(session, $"{document.Package.Effects[index].Id} OPACITY {state.Opacity:0.00}");
        });

    /// <summary>The selected effect and the clip to key it in; says why not when there is none.</summary>
    private static bool TryKeyTarget(Entity session, out int index, out XafClip clip)
    {
        index = Selected(session);
        clip = null!;
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (index < 0 || index >= document.Runtime.Pose.Effects.Length)
        {
            AnimatorSession.SetStatus(session, "SELECT AN EFFECT FIRST");
            return false;
        }
        if (session.GetComponent<AnimatorModes>().Rest)
        {
            AnimatorSession.SetStatus(session, "LEAVE REST POSE TO KEY");
            return false;
        }
        if (AnimatorSession.CurrentClip(document) is not { } current)
            return false;

        clip = current;
        document.Runtime.SetPaused(true);
        return true;
    }

    /// <summary>Shows the new keys straight away, without waiting for playback to move.</summary>
    private static void Resample(Entity session)
    {
        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(runtime.CurrentTime);
    }
}
