using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Animation;

/// <summary>
/// Bend locks, for knees and elbows: a locked bone's joint, the one at its
/// origin, only bends one way. The bend is the turn from the parent's
/// direction (its origin to this joint) to this bone's (this joint to its
/// first child's), in rig space, so it holds whichever way the character
/// faces. Posing respects it: IK always folds a locked joint to its side, and
/// turning a bone past straight stops at straight.
///
/// Angle limits, for any bone: a limited bone may only turn so far either way
/// from its rest angle, measured locally against its parent, so a spine, a
/// neck or a wrist stays within what a body can do however it is dragged.
/// Posing clamps to them the same way; clips are played as keyed.
/// </summary>
public static class XafJointLocks
{
    /// <summary>Below this the joint counts as straight, with no side to lock to.</summary>
    public const float Straight = 0.05f;

    /// <summary>How far either way a newly limited bone may turn: 45 degrees.</summary>
    public const float DefaultLimit = MathF.PI / 4f;

    /// <summary>A bone's turn from its rest angle in a pose, from -pi to pi.</summary>
    public static float Turn(XafPackage package, XafPose pose, int bone) =>
        Mathf.Wrap(pose.Bones[bone].Rotation - package.Skeleton.Bones[bone].Rest.Rotation, -MathF.PI, MathF.PI);

    /// <summary>
    /// The turn a limited bone is held to: itself inside the range, else the
    /// nearer end, measured round the circle so a turn just past half a
    /// revolution does not jump to the far end.
    /// </summary>
    public static float Clamp(XafBone bone, float turn)
    {
        if (turn >= bone.LimitMin && turn <= bone.LimitMax)
            return turn;
        float toMin = MathF.Abs(Mathf.Wrap(turn - bone.LimitMin, -MathF.PI, MathF.PI));
        float toMax = MathF.Abs(Mathf.Wrap(turn - bone.LimitMax, -MathF.PI, MathF.PI));
        return toMin <= toMax ? bone.LimitMin : bone.LimitMax;
    }

    /// <summary>The signed bend at a bone's joint, or false when it has no parent or child to measure from.</summary>
    public static bool TryBend(XafPackage package, Transform2D[] world, int bone, out float bend)
    {
        bend = 0f;
        if (bone < 0 || bone >= package.Skeleton.Bones.Count || bone >= world.Length)
            return false;

        int parent = package.Skeleton.Bones[bone].Parent;
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        if (parent < 0 || child < 0 || child >= world.Length)
            return false;

        Vector2 into = world[bone].Origin - world[parent].Origin;
        Vector2 outOf = world[child].Origin - world[bone].Origin;
        if (into.LengthSquared() < 1e-6f || outOf.LengthSquared() < 1e-6f)
            return false;

        bend = MathF.Atan2(into.Cross(outOf), into.Dot(outOf));
        return true;
    }

    /// <summary>
    /// Holds every limited bone inside its range and straightens every locked
    /// joint that bends the wrong way; true if any changed.
    /// </summary>
    public static bool Apply(XafPackage package, XafPose pose)
    {
        bool changed = ApplyLimits(package, pose);
        Transform2D[] world = XafRig.WorldBones(package, pose);
        for (int i = 0; i < package.Skeleton.Bones.Count && i < pose.Bones.Length; i++)
        {
            int locked = package.Skeleton.Bones[i].Bend;
            if (locked == 0 || !TryBend(package, world, i, out float bend) || bend * locked >= 0f)
                continue;

            // Turning this bone by -bend on screen lines it up with its parent;
            // a mirrored bone turns local rotation the other way. Joints below
            // measure their bend against this bone, so turning it leaves theirs alone.
            pose.Bones[i].Rotation += world[i].Determinant() < 0f ? bend : -bend;
            changed = true;
        }
        return changed;
    }

    /// <summary>Turns every limited bone back inside its range; true if any had left it.</summary>
    public static bool ApplyLimits(XafPackage package, XafPose pose)
    {
        bool changed = false;
        for (int i = 0; i < package.Skeleton.Bones.Count && i < pose.Bones.Length; i++)
        {
            XafBone bone = package.Skeleton.Bones[i];
            if (!bone.Limited)
                continue;

            float turn = Turn(package, pose, i);
            float held = Clamp(bone, turn);
            if (held == turn)
                continue;
            pose.Bones[i].Rotation += held - turn;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The side a two-bone IK solve must fold the middle joint to, for
    /// <see cref="XafFabrik.Solve"/>, or null when that joint is free. A fold to
    /// the solver's positive side turns the chain the negative way.
    /// </summary>
    public static bool? BendPositive(XafPackage package, int middle) =>
        middle >= 0 && middle < package.Skeleton.Bones.Count && package.Skeleton.Bones[middle].Bend != 0
            ? package.Skeleton.Bones[middle].Bend < 0
            : null;

    /// <summary>The step, in radians, the fitting search turns a compensating joint by.</summary>
    private const float FitStep = MathF.PI / 720f;

    /// <summary>
    /// Brings every key of a clip inside the rig's angle limits while keeping
    /// what the pose was for: a limb's end stays where it was. A clamped hip
    /// re-bends its knee to put the ankle back; a clamped knee re-aims its hip
    /// to; either way the foot keeps its angle in the rig. An end bone, such as
    /// a foot, is only clamped. Existing keys keep their easing; a key added
    /// takes the easing of the one before it. Returns how many bone turns
    /// were changed.
    /// </summary>
    public static int FitClip(XafPackage package, XafClip clip)
    {
        int changed = 0;
        var times = new SortedSet<float>();
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != XafChannelTarget.Bone || channel.Property != XafChannelProperty.Rotation)
                continue;
            int bone = XafRig.FindBone(package, channel.TargetId);
            if (bone >= 0 && package.Skeleton.Bones[bone].Limited)
            {
                foreach (XafKeyframe key in channel.Keyframes)
                    times.Add(key.Time);
            }
        }

        foreach (float time in times)
        {
            // Parents first, so a hip's fix is in place before its knee is looked at.
            for (int bone = 0; bone < package.Skeleton.Bones.Count; bone++)
            {
                XafBone limited = package.Skeleton.Bones[bone];
                XafChannel? keyed = RotationChannel(clip, limited.Id);
                if (!limited.Limited || keyed is null ||
                    !keyed.Keyframes.Exists(key => MathF.Abs(key.Time - time) <= XafTracks.TimeTolerance))
                {
                    continue;
                }

                XafPose pose = XafSampler.Sample(package, clip, time);
                float turn = Turn(package, pose, bone);
                float held = Clamp(limited, turn);
                if (held == turn)
                    continue;

                changed += Fit(package, clip, pose, bone, held - turn, time);
            }
        }
        return changed;
    }

    /// <summary>Turns one bone by <paramref name="by"/> at a time and compensates as <see cref="FitClip"/> says; keys what moved.</summary>
    private static int Fit(XafPackage package, XafClip clip, XafPose pose, int bone, float by, float time)
    {
        List<XafBone> bones = package.Skeleton.Bones;
        int child = bones.FindIndex(candidate => candidate.Parent == bone);
        int grandchild = child >= 0 ? bones.FindIndex(candidate => candidate.Parent == child) : -1;
        int parent = bones[bone].Parent;

        // The limb's end, and the bone whose angle in the rig is kept: what the pose was for.
        int end = grandchild >= 0 ? grandchild : child;
        Transform2D[] before = XafRig.WorldBones(package, pose);
        Vector2 endAt = end >= 0 ? before[end].Origin : Vector2.Zero;
        float endFacing = end >= 0 ? before[end].Rotation : 0f;

        pose.Bones[bone].Rotation += by;
        var touched = new List<int> { bone };
        if (end >= 0)
        {
            // A hip re-bends its knee; a knee re-aims its hip. The other joint stays inside its own limits.
            // Only bones the clip already turns: an attack layered over the base
            // must not start overriding a bone it never keyed.
            int adjust = grandchild >= 0 ? child : parent;
            if (adjust >= 0 && bones[adjust].Parent >= 0 && RotationChannel(clip, bones[adjust].Id) is not null)
            {
                BestTurn(package, pose, adjust, end, endAt);
                touched.Add(adjust);
            }

            if (RotationChannel(clip, bones[end].Id) is not null)
            {
                Transform2D[] after = XafRig.WorldBones(package, pose);
                float facing = Mathf.Wrap(endFacing - after[end].Rotation, -MathF.PI, MathF.PI);
                pose.Bones[end].Rotation += after[end].Determinant() < 0f ? -facing : facing;
                if (bones[end].Limited)
                {
                    float endTurn = Turn(package, pose, end);
                    pose.Bones[end].Rotation += Clamp(bones[end], endTurn) - endTurn;
                }
                touched.Add(end);
            }
        }

        foreach (int moved in touched)
            KeyRotation(clip, bones[moved].Id, time, pose.Bones[moved].Rotation);
        return touched.Count;
    }

    /// <summary>Turns one bone, within its limits, to bring another's joint as near a spot as it will go.</summary>
    private static void BestTurn(XafPackage package, XafPose pose, int bone, int end, Vector2 spot)
    {
        XafBone owner = package.Skeleton.Bones[bone];
        float rest = owner.Rest.Rotation;
        float low = owner.Limited ? rest + owner.LimitMin : pose.Bones[bone].Rotation - MathF.PI;
        float high = owner.Limited ? rest + owner.LimitMax : pose.Bones[bone].Rotation + MathF.PI;

        float best = pose.Bones[bone].Rotation;
        float bestDistance = float.MaxValue;
        for (float rotation = low; rotation <= high + 1e-6f; rotation += FitStep)
        {
            pose.Bones[bone].Rotation = rotation;
            float distance = XafRig.WorldBones(package, pose)[end].Origin.DistanceSquaredTo(spot);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = rotation;
            }
        }
        pose.Bones[bone].Rotation = best;
    }

    private static XafChannel? RotationChannel(XafClip clip, string bone) =>
        clip.Channels.Find(channel => channel.Target == XafChannelTarget.Bone && channel.TargetId == bone &&
                                      channel.Property == XafChannelProperty.Rotation);

    /// <summary>Sets a rotation key's value, keeping its easing; a new key eases as the one before it.</summary>
    private static void KeyRotation(XafClip clip, string bone, float time, float value)
    {
        XafChannel? channel = RotationChannel(clip, bone);
        if (channel is null)
        {
            channel = new XafChannel { Target = XafChannelTarget.Bone, TargetId = bone, Property = XafChannelProperty.Rotation };
            clip.Channels.Add(channel);
        }

        XafKeyframe? existing = channel.Keyframes.Find(key => MathF.Abs(key.Time - time) <= XafTracks.TimeTolerance);
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        int index = channel.Keyframes.FindIndex(key => key.Time > time);
        XafKeyframe? previous = index < 0 ? channel.Keyframes.LastOrDefault() : index > 0 ? channel.Keyframes[index - 1] : null;
        var added = new XafKeyframe { Time = time, Value = value, Interpolation = previous?.Interpolation ?? XafInterpolation.Linear };
        if (index < 0)
            channel.Keyframes.Add(added);
        else
            channel.Keyframes.Insert(index, added);
    }
}
