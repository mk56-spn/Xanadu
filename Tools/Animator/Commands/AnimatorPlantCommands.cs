using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Planting: a foot or hand stays at one spot on the rig from the playhead on. A
/// plant is a <see cref="XafLimbTarget"/> from the moment it is made, so it is in
/// the file, in undo and in the baked keys; there is nothing to release first.
/// </summary>
public static class AnimatorPlantCommands
{
    /// <summary>How far, in rig units, a released foot may stray between its keys before another is added.</summary>
    public const float DriftTolerance = XafTargets.DriftTolerance;

    /// <summary>How far a solved foot may sit from its spot before it counts as out of reach.</summary>
    private const float ReachTolerance = 0.5f;

    /// <summary>Plants the selected bone or, if it is planted at the playhead, ends the plant there.</summary>
    public static void Toggle(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (session.GetComponent<AnimatorModes>().Rest)
        {
            AnimatorSession.SetStatus(session, "LEAVE REST POSE TO PIN");
            return;
        }

        int bone = Target(document.Package, session.GetComponent<AnimatorSelection>().Bone);
        if (bone < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT A FOOT OR HAND TO PIN");
            return;
        }

        if (AnimatorSession.CurrentClip(document) is not { } clip)
            return;

        float now = document.Runtime.CurrentTime;
        string id = document.Package.Skeleton.Bones[bone].Id;
        XafLimbTarget? existing = clip.Targets.Find(target => target.BoneId == id && target.Covers(now));
        if (existing is null)
            Plant(session, clip, bone);
        else if (existing.Shape == XafTargetShape.Spot)
            EndPlant(session, clip, existing);
        else
            AnimatorSession.SetStatus(session, $"{id} ALREADY HAS AN OVERRIDE THERE");
    }

    /// <summary>Holds the clip's plants in a paused pose, which has just been sampled or dragged, and lists those at the playhead.</summary>
    public static void Hold(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        List<AnimatorPlant> held = session.GetComponent<AnimatorPlants>().Held;
        held.Clear();
        if (session.GetComponent<AnimatorModes>().Rest || AnimatorSession.CurrentClip(document) is not { } clip)
            return;

        XafPackage package = document.Package;
        float now = document.Runtime.CurrentTime;
        foreach (XafLimbTarget target in clip.Targets)
        {
            if (target.Shape != XafTargetShape.Spot || !target.Covers(now) || !XafTargets.IsValid(package, target))
                continue;

            // Playing shows the keys, as the game will; only a paused pose has unbaked moves to hold against.
            bool paused = document.Runtime.Paused;
            float missed = paused ? XafTargets.Apply(package, document.Runtime.Pose, target, now) : 0f;
            Transform2D[] world = XafRig.WorldBones(package, document.Runtime.Pose);
            int bone = XafRig.FindBone(package, target.BoneId);
            Vector2 at = XafTargets.WorldAt(package, target, world, now);
            if (!paused)
                missed = world[bone].Origin.DistanceTo(at) - DriftTolerance;
            held.Add(new AnimatorPlant { Bone = bone, At = at, OutOfReach = missed > ReachTolerance });
        }
    }

    /// <summary>
    /// Turns the pins holding these bones' facing at the playhead to face as the
    /// bones do now, and re-bakes, as dragging a pin's spot moves it: a pinned
    /// foot can be turned by hand. Returns how many pins turned.
    /// </summary>
    public static int Reface(Entity session, IEnumerable<int> bones)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (session.GetComponent<AnimatorModes>().Rest || AnimatorSession.CurrentClip(document) is not { } clip)
            return 0;

        Transform2D[] world = XafRig.WorldBones(document.Package, document.Runtime.Pose);
        int turned = 0;
        foreach (int bone in bones)
        {
            if (HeldFacing(document.Package, clip, bone, document.Runtime.CurrentTime) is not { } pin)
                continue;
            pin.Facing = world[bone].Rotation;
            turned++;
        }

        if (turned > 0)
            AnimatorTargetCommands.Rebake(session);
        return turned;
    }

    /// <summary>
    /// True when every one of these bones has its facing held by a pin at the
    /// playhead: turning them moves the pins, so there is nothing to key.
    /// </summary>
    public static bool AllHeldFacing(Entity session, IReadOnlyCollection<int> bones)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (bones.Count == 0 || session.GetComponent<AnimatorModes>().Rest ||
            AnimatorSession.CurrentClip(document) is not { } clip)
        {
            return false;
        }

        foreach (int bone in bones)
        {
            if (HeldFacing(document.Package, clip, bone, document.Runtime.CurrentTime) is null)
                return false;
        }
        return true;
    }

    private static XafLimbTarget? HeldFacing(XafPackage package, XafClip clip, int bone, float now)
    {
        if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            return null;
        string id = package.Skeleton.Bones[bone].Id;
        return clip.Targets.Find(target => target.BoneId == id && target.HoldFacing && target.Covers(now) &&
                                           XafTargets.IsValid(package, target));
    }

    /// <summary>The bone a selection plants: itself if it ends a limb, or the bone at its end, as clicking a foot selects the shin.</summary>
    public static int Target(XafPackage package, int selected)
    {
        List<XafBone> bones = package.Skeleton.Bones;
        if (selected < 0 || selected >= bones.Count)
            return -1;

        int bone = selected;
        int child = bones.FindIndex(candidate => candidate.Parent == bone);
        if (child >= 0 && bones.FindLastIndex(candidate => candidate.Parent == bone) == child &&
            bones.FindIndex(candidate => candidate.Parent == child) < 0)
        {
            bone = child;
        }

        int parent = bones[bone].Parent;
        return parent >= 0 && bones[parent].Parent >= 0 ? bone : -1;
    }

    /// <summary>Adds the plant from the playhead to the end of the loop or clip, or to the bone's next target.</summary>
    private static void Plant(Entity session, XafClip clip, int bone)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        AnimatorRuntime runtime = document.Runtime;
        runtime.SetPaused(true);
        float now = runtime.CurrentTime;
        string id = package.Skeleton.Bones[bone].Id;
        float to = clip.Loop && now < clip.LoopEnd ? clip.LoopEnd : clip.Duration;
        foreach (XafLimbTarget other in clip.Targets)
        {
            if (other.BoneId == id && other.From > now)
                to = Math.Min(to, other.From);
        }

        if (to - now < AnimatorOverrideCommands.MinDuration)
        {
            AnimatorSession.SetStatus(session, "NO TIME LEFT TO PIN · MOVE THE PLAYHEAD BACK");
            return;
        }

        XafPose live = runtime.Pose.Clone();
        Transform2D[] world = XafRig.WorldBones(package, live);
        var plant = new XafLimbTarget
        {
            BoneId = id,
            Shape = XafTargetShape.Spot,
            Center = world[bone].Origin,
            From = now,
            To = to,
            HoldFacing = true,
            Facing = world[bone].Rotation,
            BendPositive = XafTargets.CurrentBend(package, world, bone),
        };
        AnimatorHistoryCommands.Edit(session, () =>
        {
            clip.Targets.Add(plant);
            XafTargets.Bake(package, clip);
            KeepPose(runtime, live, now);
        });
        AnimatorSession.SetStatus(session, $"{id} PINNED");
    }

    /// <summary>Ends the plant at the playhead; at its first moment that removes it, leaving its keys as ordinary keys.</summary>
    private static void EndPlant(Entity session, XafClip clip, XafLimbTarget plant)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorRuntime runtime = document.Runtime;
        runtime.SetPaused(true);
        float now = runtime.CurrentTime;
        if (plant.To - now <= XafTracks.TimeTolerance)
        {
            AnimatorSession.SetStatus(session, $"{plant.BoneId} PIN ENDS HERE · DETACH REMOVES IT");
            return;
        }

        XafTargets.Apply(document.Package, runtime.Pose, plant, now);
        XafPose live = runtime.Pose.Clone();
        float oldTo = plant.To;
        // Too short a pin left over would own keys with no bar to show for it.
        bool remove = now - plant.From < AnimatorOverrideCommands.MinDuration;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (remove)
                clip.Targets.Remove(plant);
            else
            {
                plant.To = now;
                XafTargets.DropKeysLeftBehind(document.Package, clip, plant, plant.From, oldTo);
            }
            XafTargets.Bake(document.Package, clip);
            KeepPose(runtime, live, now);
        });
        AnimatorSession.SetStatus(session, remove ? $"{plant.BoneId} UNPINNED · KEYS KEPT" : $"{plant.BoneId} PIN ENDS HERE");
    }

    /// <summary>Puts back the playhead's pose after a bake, as dragged but unkeyed moves there would not survive a seek.</summary>
    private static void KeepPose(AnimatorRuntime runtime, XafPose live, float now)
    {
        runtime.Seek(now);
        live.Bones.CopyTo(runtime.Pose.Bones, 0);
    }
}
