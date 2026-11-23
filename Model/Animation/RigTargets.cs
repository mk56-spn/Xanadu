using System.Collections.Generic;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Animation;

/// <summary>
/// Solving and baking <see cref="RigLimbTarget"/>s: a limb is folded to reach the
/// target, and a bone free to translate simply goes there. Baking writes as few
/// keys as keep it on the path: the two ends, then wherever it strays furthest.
/// </summary>
public static class RigTargets
{
    /// <summary>The moments checked for where a key is needed.</summary>
    public const float BakeStep = 1f / 60f;

    /// <summary>How far, in rig units, a baked bone may stray from its path before another key is added.</summary>
    public const float DriftTolerance = 0.5f;

    /// <summary>Counts a held facing's turn as this many rig units per radian.</summary>
    private const float FacingWeight = 8f;

    /// <summary>How long before a loop's end its last pose is read from, as a loop plays its last moment as its first.</summary>
    private const float LoopEndLeadIn = 0.001f;

    /// <summary>Whether a target on this bone bends a limb, rather than moving the bone itself.</summary>
    public static bool DrivesLimb(RigPackage package, int bone)
    {
        if (bone < 0 || bone >= package.Skeleton.Bones.Count || package.Skeleton.Bones[bone].CanTranslate)
            return false;
        int parent = package.Skeleton.Bones[bone].Parent;
        return parent >= 0 && package.Skeleton.Bones[parent].Parent >= 0;
    }

    /// <summary>Whether a target's bones are all still in the rig.</summary>
    public static bool IsValid(RigPackage package, RigLimbTarget target)
    {
        int bone = RigRig.FindBone(package, target.BoneId);
        return bone >= 0 && (target.AnchorId.Length == 0 || RigRig.FindBone(package, target.AnchorId) >= 0) &&
               (DrivesLimb(package, bone) || package.Skeleton.Bones[bone].CanTranslate);
    }

    /// <summary>The bones a target's keys are on, with the channel each uses.</summary>
    public static List<(int Bone, RigChannelProperty Property)> Channels(RigPackage package, RigLimbTarget target)
    {
        int bone = RigRig.FindBone(package, target.BoneId);
        var channels = new List<(int, RigChannelProperty)>();
        if (bone < 0)
            return channels;

        if (!DrivesLimb(package, bone))
        {
            channels.Add((bone, RigChannelProperty.TranslationX));
            channels.Add((bone, RigChannelProperty.TranslationY));
            return channels;
        }

        int parent = package.Skeleton.Bones[bone].Parent;
        channels.Add((package.Skeleton.Bones[parent].Parent, RigChannelProperty.Rotation));
        channels.Add((parent, RigChannelProperty.Rotation));
        if (target.HoldFacing)
            channels.Add((bone, RigChannelProperty.Rotation));
        return channels;
    }

    /// <summary>Where a target is in rig space at a time, given the pose's bones.</summary>
    public static Vector2 WorldAt(RigPackage package, RigLimbTarget target, Transform2D[] world, float time)
    {
        int anchor = target.AnchorId.Length > 0 ? RigRig.FindBone(package, target.AnchorId) : -1;
        return anchor >= 0 ? world[anchor] * target.LocalAt(time) : target.LocalAt(time);
    }

    /// <summary>Turns a bone about its origin so its child joint points at a rig-space spot.</summary>
    public static void Aim(RigPackage package, RigPose pose, int bone, int child, Vector2 target)
    {
        Transform2D[] world = RigRig.WorldBones(package, pose);
        Vector2 toTarget = target - world[bone].Origin;
        Vector2 toChild = world[child].Origin - world[bone].Origin;
        if (toTarget.LengthSquared() < 0.0001f || toChild.LengthSquared() < 0.0001f)
            return;

        float delta = Mathf.Wrap(toTarget.Angle() - toChild.Angle(), -float.Pi, float.Pi);
        // A mirrored bone turns local rotation the opposite way on screen.
        if (world[bone].Determinant() < 0f)
            delta = -delta;
        pose.Bones[bone].Rotation += delta;
    }

    /// <summary>Folds a two-bone limb to reach a spot and returns how far short it stayed.</summary>
    public static float SolveLimb(RigPackage package, RigPose pose, int bone, Vector2 at, bool bendPositive, float? facing)
    {
        int parent = package.Skeleton.Bones[bone].Parent;
        int grandparent = package.Skeleton.Bones[parent].Parent;
        Transform2D[] world = RigRig.WorldBones(package, pose);
        bool bend = RigJointLocks.BendPositive(package, parent) ?? bendPositive;
        Vector2[] solved = RigFabrik.Solve([world[grandparent].Origin, world[parent].Origin, world[bone].Origin], at, bend);
        Aim(package, pose, grandparent, parent, solved[1]);
        Aim(package, pose, parent, bone, solved[2]);
        RigJointLocks.Apply(package, pose);

        world = RigRig.WorldBones(package, pose);
        if (facing is { } wanted)
        {
            float turn = Mathf.Wrap(wanted - world[bone].Rotation, -float.Pi, float.Pi);
            pose.Bones[bone].Rotation += world[parent].Determinant() < 0f ? -turn : turn;
        }
        return world[bone].Origin.DistanceTo(at);
    }

    /// <summary>Whether a limb can reach a rig-space spot from where its top joint is now; a bone that moves itself always can.</summary>
    public static bool CanReach(RigPackage package, Transform2D[] world, int bone, Vector2 at)
    {
        if (!DrivesLimb(package, bone))
            return true;

        int parent = package.Skeleton.Bones[bone].Parent;
        int top = package.Skeleton.Bones[parent].Parent;
        float length = world[top].Origin.DistanceTo(world[parent].Origin) + world[parent].Origin.DistanceTo(world[bone].Origin);
        return world[top].Origin.DistanceTo(at) <= length + DriftTolerance;
    }

    /// <summary>The way a limb is folding now, to keep it folding that way.</summary>
    public static bool CurrentBend(RigPackage package, Transform2D[] world, int bone)
    {
        int parent = package.Skeleton.Bones[bone].Parent;
        int grandparent = package.Skeleton.Bones[parent].Parent;
        Vector2 root = world[grandparent].Origin;
        Vector2 reach = world[bone].Origin - root;
        return new Vector2(-reach.Y, reach.X).Dot(world[parent].Origin - root) >= 0f;
    }

    /// <summary>Puts the target's bones where it wants them in a pose and returns how far short they stayed.</summary>
    public static float Apply(RigPackage package, RigPose pose, RigLimbTarget target, float time)
    {
        int bone = RigRig.FindBone(package, target.BoneId);
        Transform2D[] world = RigRig.WorldBones(package, pose);
        Vector2 at = WorldAt(package, target, world, time);
        if (DrivesLimb(package, bone))
            return SolveLimb(package, pose, bone, at, target.BendPositive, target.HoldFacing ? target.Facing : null);

        int parent = package.Skeleton.Bones[bone].Parent;
        pose.Bones[bone].Translation = parent >= 0 ? world[parent].AffineInverse() * at : at;
        return 0f;
    }

    /// <summary>Bakes a clip's targets, parents before children, so each solves against finished parents.</summary>
    public static void Bake(RigPackage package, RigClip clip)
    {
        if (clip.Targets.Count == 0)
            return;

        var ordered = new List<RigLimbTarget>(clip.Targets);
        ordered.Sort((a, b) => RigRig.FindBone(package, a.BoneId).CompareTo(RigRig.FindBone(package, b.BoneId)));
        foreach (RigLimbTarget target in ordered)
        {
            if (IsValid(package, target))
                BakeOne(package, clip, target);
        }
    }

    public static void BakeAll(RigPackage package)
    {
        foreach (RigClip clip in package.Clips)
            Bake(package, clip);
    }

    /// <summary>
    /// Removes the keys a target baked over its old span that its new span no longer
    /// covers, or the limb would go on following a path the target has left.
    /// </summary>
    public static void DropKeysLeftBehind(RigPackage package, RigClip clip, RigLimbTarget target, float oldFrom, float oldTo)
    {
        if (!IsValid(package, target))
            return;

        foreach ((int bone, RigChannelProperty property) in Channels(package, target))
        {
            string id = package.Skeleton.Bones[bone].Id;
            clip.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == id &&
                                          channel.Property == property)
                ?.Keys.RemoveAll(key => key.Time >= oldFrom - RigTracks.TimeTolerance &&
                                             key.Time <= oldTo + RigTracks.TimeTolerance &&
                                             !target.Covers(key.Time));
        }
    }

    /// <summary>
    /// Drops every key the target baked: all keys on its channels inside its
    /// span, since baking clears the span before it writes its own.
    /// </summary>
    public static void DropBakedKeys(RigPackage package, RigClip clip, RigLimbTarget target)
    {
        if (!IsValid(package, target))
            return;

        foreach ((int bone, RigChannelProperty property) in Channels(package, target))
        {
            string id = package.Skeleton.Bones[bone].Id;
            clip.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == id &&
                                          channel.Property == property)
                ?.Keys.RemoveAll(key => key.Time >= target.From - RigTracks.TimeTolerance &&
                                             key.Time <= target.To + RigTracks.TimeTolerance);
        }
        clip.Channels.RemoveAll(channel => channel.Keys.Count == 0);
    }

    private static void BakeOne(RigPackage package, RigClip clip, RigLimbTarget target)
    {
        List<(int Bone, RigChannelProperty Property)> channels = Channels(package, target);
        float from = Math.Clamp(target.From, 0f, clip.Duration);
        float to = Math.Clamp(target.To, from, clip.Duration);

        // Keys left inside the span would pull it off the path.
        foreach ((int bone, RigChannelProperty property) in channels)
        {
            string id = package.Skeleton.Bones[bone].Id;
            clip.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == id &&
                                          channel.Property == property)
                ?.Keys.RemoveAll(key => key.Time >= from - RigTracks.TimeTolerance && key.Time <= to + RigTracks.TimeTolerance);
        }

        int steps = (int)MathF.Ceiling((to - from) / BakeStep);
        var times = new float[steps + 1];
        var values = new float[steps + 1][];
        var reached = new float[steps + 1];
        for (int step = 0; step <= steps; step++)
        {
            times[step] = Math.Min(from + step * BakeStep, to);
            RigPose pose = RigSampler.Sample(package, clip, SampleTime(clip, times[step]));
            reached[step] = Apply(package, pose, target, times[step]);
            values[step] = new float[channels.Count];
            for (int c = 0; c < channels.Count; c++)
            {
                float value = Read(pose.Bones[channels[c].Bone], channels[c].Property);

                // The same turn may read a full circle away; keys between must not spin the long way.
                if (step > 0 && channels[c].Property == RigChannelProperty.Rotation)
                    value = values[step - 1][c] + Mathf.Wrap(value - values[step - 1][c], -float.Pi, float.Pi);
                values[step][c] = value;
            }
        }

        WriteKey(package, clip, channels, times, values, 0);
        if (steps == 0)
            return;

        WriteKey(package, clip, channels, times, values, steps);
        for (int added = 0; added < steps; added++)
        {
            int worst = WorstStep(package, clip, target, times, reached);
            if (worst < 0)
                break;
            WriteKey(package, clip, channels, times, values, worst);
        }
    }

    /// <summary>The moment the keys so far stray furthest, or -1 when they hold everywhere. Only what a key could fix counts.</summary>
    private static int WorstStep(RigPackage package, RigClip clip, RigLimbTarget target, float[] times, float[] reached)
    {
        int bone = RigRig.FindBone(package, target.BoneId);
        int worst = -1;
        float furthest = DriftTolerance;
        for (int step = 1; step < times.Length - 1; step++)
        {
            Transform2D[] world = RigRig.WorldBones(package, RigSampler.Sample(package, clip, SampleTime(clip, times[step])));
            float amount = world[bone].Origin.DistanceTo(WorldAt(package, target, world, times[step])) - reached[step];
            if (target.HoldFacing)
            {
                float turned = MathF.Abs(Mathf.Wrap(world[bone].Rotation - target.Facing, -float.Pi, float.Pi));
                amount = Math.Max(amount, turned * FacingWeight);
            }
            if (amount > furthest)
            {
                furthest = amount;
                worst = step;
            }
        }
        return worst;
    }

    private static float SampleTime(RigClip clip, float time) =>
        clip.Loop && time >= clip.LoopEnd - RigTracks.TimeTolerance ? time - LoopEndLeadIn : time;

    private static float Read(in RigTransform bone, RigChannelProperty property) => property switch
    {
        RigChannelProperty.TranslationX => bone.Translation.X,
        RigChannelProperty.TranslationY => bone.Translation.Y,
        _ => bone.Rotation,
    };

    /// <summary>
    /// A curved key carrying the motion's slope at its moment, so a smooth path
    /// takes a handful of keys where straight segments would take dozens.
    /// </summary>
    private static void WriteKey(RigPackage package, RigClip clip, List<(int Bone, RigChannelProperty Property)> channels,
        float[] times, float[][] values, int step)
    {
        int before = Math.Max(step - 1, 0);
        int after = Math.Min(step + 1, times.Length - 1);
        float span = times[after] - times[before];
        for (int c = 0; c < channels.Count; c++)
        {
            string id = package.Skeleton.Bones[channels[c].Bone].Id;
            RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, id, channels[c].Property, times[step], values[step][c],
                KeyEase.CubicBezier);
            float slope = span > 0f ? (values[after][c] - values[before][c]) / span : 0f;
            RigChannelProperty property = channels[c].Property;
            Keyframe key = clip.Channels
                .Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == id && channel.Property == property)!
                .Keys.Find(candidate => MathF.Abs(candidate.Time - times[step]) <= RigTracks.TimeTolerance)!;
            key.TangentIn = slope;
            key.TangentOut = slope;
        }
    }
}
