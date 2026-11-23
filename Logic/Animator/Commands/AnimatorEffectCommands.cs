using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Parts that flash on and off in the animator: adding a streak or a particle
/// stream to a bone (a part that starts hidden), deleting parts, and keying
/// when the selected part shows and how opaque it is.
/// </summary>
public static class AnimatorEffectCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<AddEffect>((s, _) => Add(s));
        session.On<AddParticles>((s, _) => AddParticles(s));
        session.On<DeleteEffect>((s, _) => Delete(s));
        session.On<ToggleEffectVisible>((s, _) => ToggleVisible(s));
        session.On<StepEffectOpacity>((s, e) => StepOpacity(s, e.By * OpacityStep));
    }

    /// <summary>One press of the opacity buttons.</summary>
    public const float OpacityStep = 0.25f;

    /// <summary>
    /// Hangs a streak off the selected bone and keys it shown at the playhead
    /// and hidden <see cref="RigEffects.DefaultShowSeconds"/> later, so it is a
    /// visible flash in the clip from the start.
    /// </summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int bone = session.GetComponent<AnimatorSelection>().Bone;
            if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
                return;
            }

            int index = RigEffects.Add(package, package.Skeleton.Bones[bone].Id, RigEffects.Streak(), "streak");
            AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
            float time = runtime.CurrentTime;
            if (AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is { } clip &&
                !AnimatorModes.Of(session).Rest)
            {
                RigAuthoring.KeyVisible(package, clip, index, time, true);
                RigAuthoring.KeyVisible(package, clip, index,
                    Math.Min(clip.Duration, time + RigEffects.DefaultShowSeconds), false);
            }

            AnimatorSession.RigChanged(session);
            session.GetComponent<AnimatorSelection>().SelectOnlyAttachment(index);
            AnimatorSession.SetStatus(session, $"ADDED {package.Attachments[index].Id.ToUpperInvariant()} ON {package.Skeleton.Bones[bone].Id}");
        });

    /// <summary>
    /// Hangs a particle stream off the selected bone, with its own copy of the
    /// library's sparks preset (or sparks made here), and keys it flowing at the playhead and
    /// stopped <see cref="RigEffects.DefaultStreamSeconds"/> later.
    /// </summary>
    public static void AddParticles(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int bone = session.GetComponent<AnimatorSelection>().Bone;
            if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
                return;
            }

            int index = RigEffects.Add(package, package.Skeleton.Bones[bone].Id, new Xif { Name = "particles", Modules = { RigEffects.SparkArt() } }, "particles");
            package.Attachments[index].Particles = RigEffects.StreamParticle();
            AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
            float time = runtime.CurrentTime;
            if (AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is { } clip &&
                !AnimatorModes.Of(session).Rest)
            {
                RigAuthoring.KeyVisible(package, clip, index, time, true);
                RigAuthoring.KeyVisible(package, clip, index,
                    Math.Min(clip.Duration, time + RigEffects.DefaultStreamSeconds), false);
            }

            AnimatorSession.RigChanged(session);
            session.GetComponent<AnimatorSelection>().SelectOnlyAttachment(index);
            AnimatorSession.SetStatus(session, $"ADDED {package.Attachments[index].Id.ToUpperInvariant()} ON {package.Skeleton.Bones[bone].Id}");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = AnimatorSelected.Part(session);
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A PART FIRST");
                return;
            }

            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            string id = package.Attachments[index].Id;
            RigEffects.Remove(package, index);
            session.GetComponent<AnimatorSelection>().Attachment = -1;
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"DELETED {id}");
        });

    /// <summary>Keys the selected part the other way from how it is showing at the playhead.</summary>
    public static void ToggleVisible(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryKeyTarget(session, out int index, out RigClip clip))
                return;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            bool showing = !document.Runtime.Pose.AttachmentStates[index].Hidden;
            RigAuthoring.KeyVisible(document.Package, clip, index, document.Runtime.CurrentTime, !showing);
            Resample(session);
            AnimatorSession.SetStatus(session,
                $"{document.Package.Attachments[index].Id} {(showing ? "HIDDEN" : "SHOWN")} AT {document.Runtime.CurrentTime:0.00} S");
        });

    public static void StepOpacity(Entity session, float delta) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryKeyTarget(session, out int index, out RigClip clip))
                return;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            float opacity = Opacity(document, index);
            opacity = Math.Clamp(MathF.Round((opacity + delta) / OpacityStep) * OpacityStep, 0f, 1f);
            RigAuthoring.KeyOpacity(document.Package, clip, index, document.Runtime.CurrentTime, opacity);
            Resample(session);
            AnimatorSession.SetStatus(session, $"{document.Package.Attachments[index].Id} OPACITY {opacity:0.00}");
        });

    /// <summary>Keys the selected part's opacity, 0 to 1, at the playhead.</summary>
    public static void SetOpacity(Entity session, float opacity) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryKeyTarget(session, out int index, out RigClip clip))
                return;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            opacity = Math.Clamp(opacity, 0f, 1f);
            RigAuthoring.KeyOpacity(document.Package, clip, index, document.Runtime.CurrentTime, opacity);
            Resample(session);
            AnimatorSession.SetStatus(session, $"{document.Package.Attachments[index].Id} OPACITY {opacity:0.00}");
        });

    /// <summary>The part's opacity in the pose at the playhead: 1 unless a clip keys it.</summary>
    public static float Opacity(AnimatorDocument document, int index)
    {
        RigAttachmentState[] states = document.Runtime.Pose.AttachmentStates;
        if (index < 0 || index >= states.Length)
            return 1f;
        return states[index].HasOpacity ? states[index].Opacity : 1f;
    }

    /// <summary>The selected effect and the clip to key it in; says why not when there is none.</summary>
    private static bool TryKeyTarget(Entity session, out int index, out RigClip clip)
    {
        index = AnimatorSelected.Part(session);
        clip = null!;
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (index < 0 || index >= document.Runtime.Pose.AttachmentStates.Length)
        {
            AnimatorSession.SetStatus(session, "SELECT A PART FIRST");
            return false;
        }
        if (AnimatorModes.Of(session).Rest)
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
