using Xanadu.Core.Keys;
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Limb targets in the animator: adding and shaping them, and keeping the keys
/// they own baked. Every finished edit re-bakes the open clip, and saving bakes
/// them all, so a saved file is never stale.
/// </summary>
public static class AnimatorTargetCommands
{
    public const float DefaultRadiusX = 20f;
    public const float DefaultRadiusY = 12f;
    private const float RadiusStep = 2f;
    private const float AngleStep = float.Pi / 12f;
    private const float SweepStep = float.Pi / 2f;
    public const float HandleRadius = 10f;

    public static bool IsDrag(AnimatorDragKind kind) =>
        kind is AnimatorDragKind.TargetCenter or AnimatorDragKind.TargetRadiusX or AnimatorDragKind.TargetRadiusY;

    /// <summary>The target on the selected bone or a limb it bends, the one at the playhead if there are several.</summary>
    public static RigLimbTarget? Selected(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        if (bone < 0 || AnimatorSession.CurrentClip(document) is not { } clip)
            return null;

        RigLimbTarget? found = null;
        foreach (RigLimbTarget target in clip.Targets)
        {
            if (RigRig.FindBone(document.Package, target.BoneId) != bone &&
                !RigTargets.Channels(document.Package, target).Exists(channel => channel.Bone == bone))
            {
                continue;
            }
            if (target.Covers(document.Runtime.CurrentTime))
                return target;
            found ??= target;
        }
        return found;
    }

    public static void Run(Entity session, string argument)
    {
        string[] parts = argument.Split(':');
        int step = parts.Length > 1 && int.TryParse(parts[1], out int parsed) ? parsed : 0;
        if (parts[0] == "orbit")
        {
            AddOrbit(session);
            return;
        }
        if (parts[0] == "pin")
        {
            AnimatorPlantCommands.Toggle(session);
            return;
        }

        if (Selected(session) is null)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE WITH AN OVERRIDE");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigLimbTarget target = Selected(session)!;
            float now = session.GetComponent<AnimatorDocument>().Runtime.CurrentTime;
            switch (parts[0])
            {
                case "rx":
                    target.RadiusX = Math.Max(RigLimbTarget.MinRadius, target.RadiusX + step * RadiusStep);
                    break;
                case "ry":
                    target.RadiusY = Math.Max(RigLimbTarget.MinRadius, target.RadiusY + step * RadiusStep);
                    break;
                case "tilt":
                    target.Tilt += step * AngleStep;
                    break;
                case "start":
                    target.StartAngle += step * AngleStep;
                    break;
                case "sweep":
                    target.Sweep += step * SweepStep * MathF.Sign(target.Sweep == 0f ? 1f : target.Sweep);
                    break;
                case "flip":
                    target.Sweep = -target.Sweep;
                    break;
                case "ease":
                    target.Ease = NextEase(target.Ease);
                    break;
                case "anchor":
                    NextAnchor(session, target);
                    break;
                case "from" when now <= target.To - AnimatorOverrideCommands.MinDuration:
                    SetSpan(session, target, now, target.To);
                    break;
                case "to" when now >= target.From + AnimatorOverrideCommands.MinDuration:
                    SetSpan(session, target, target.From, now);
                    break;
                case "detach":
                    AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>())!.Targets.Remove(target);
                    AnimatorSession.SetStatus(session, "DETACHED · ITS KEYS ARE YOURS");
                    break;
            }
        });
    }

    /// <summary>
    /// Sets one of the selected ellipse's shape values, as its slider does:
    /// <c>rx</c>, <c>ry</c> in rig units, <c>tilt</c>, <c>start</c>, <c>sweep</c>
    /// in radians. The clip is baked again at once, so a drag shows as it goes.
    /// </summary>
    public static void SetShape(Entity session, string verb, float value)
    {
        if (Selected(session) is null)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE WITH AN OVERRIDE");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigLimbTarget target = Selected(session)!;
            switch (verb)
            {
                case "rx":
                    target.RadiusX = Math.Max(RigLimbTarget.MinRadius, value);
                    break;
                case "ry":
                    target.RadiusY = Math.Max(RigLimbTarget.MinRadius, value);
                    break;
                case "tilt":
                    target.Tilt = value;
                    break;
                case "start":
                    target.StartAngle = value;
                    break;
                case "sweep":
                    target.Sweep = value;
                    break;
            }
            Rebake(session);
        });
    }

    /// <summary>Gives a target a new span, dropping the keys it baked over the time it no longer covers.</summary>
    private static void SetSpan(Entity session, RigLimbTarget target, float from, float to)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        float oldFrom = target.From;
        float oldTo = target.To;
        target.From = from;
        target.To = to;
        RigTargets.DropKeysLeftBehind(document.Package, AnimatorSession.CurrentClip(document)!, target, oldFrom, oldTo);
    }

    /// <summary>Re-bakes after a finished edit, saying so if it had to overwrite keys the edit made on a driven limb.</summary>
    public static void AfterEdit(Entity session, RigPackage before)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (AnimatorSession.CurrentClip(document) is not { Targets.Count: > 0 } clip)
            return;

        // An override too short to show a bar still owns keys: drop it, leaving them ordinary keys.
        int slivers = clip.Targets.RemoveAll(IsSliver);
        if (slivers > 0)
            AnimatorSession.SetStatus(session, slivers == 1
                ? "REMOVED AN OVERRIDE TOO SHORT TO SEE · ITS KEYS ARE YOURS"
                : $"REMOVED {slivers} OVERRIDES TOO SHORT TO SEE · THEIR KEYS ARE YOURS");

        if (TouchedOwnedKeys(document.Package, before.Clips.Find(old => old.Id == clip.Id), clip))
            AnimatorSession.SetStatus(session, "THOSE KEYS BELONG TO AN OVERRIDE · DETACH");
        Rebake(session);
    }

    /// <summary>Shorter than an override can be made, so too short to see or grab on the timeline.</summary>
    public static bool IsSliver(RigLimbTarget target) =>
        target.To - target.From < AnimatorOverrideCommands.MinDuration - RigTracks.TimeTolerance;

    /// <summary>Bakes the open clip and shows the result on the limbs it drives; the rest of the pose is left as posed.</summary>
    public static void Rebake(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (AnimatorSession.CurrentClip(document) is not { Targets.Count: > 0 } clip)
            return;

        RigTargets.Bake(document.Package, clip);
        RigPose sampled = RigSampler.Sample(document.Package, clip, document.Runtime.CurrentTime);
        foreach (RigLimbTarget target in clip.Targets)
        {
            if (!RigTargets.IsValid(document.Package, target) || !target.Covers(document.Runtime.CurrentTime))
                continue;
            foreach ((int bone, _) in RigTargets.Channels(document.Package, target))
                document.Runtime.Pose.Bones[bone] = sampled.Bones[bone];
        }
    }

    /// <summary>The selected target's ellipse in rig space now, for drawing and grabbing.</summary>
    public static Transform2D AnchorWorld(RigPackage package, RigLimbTarget target, Transform2D[] world)
    {
        int anchor = target.AnchorId.Length > 0 ? RigRig.FindBone(package, target.AnchorId) : -1;
        return anchor >= 0 ? world[anchor] : Transform2D.Identity;
    }

    /// <summary>Starts dragging the selected target's handle under the pointer: its centre, or an ellipse's radii.</summary>
    public static bool TryBegin(Entity session, Vector2 pointer, Transform2D placement, Transform2D[] world)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (Selected(session) is not { } target || !RigTargets.IsValid(package, target))
            return false;

        Transform2D toScreen = placement * AnchorWorld(package, target, world);
        AnimatorDragKind kind = AnimatorDragKind.None;
        if ((toScreen * target.Center).DistanceTo(pointer) <= HandleRadius)
            kind = AnimatorDragKind.TargetCenter;
        else if (target.Shape == RigTargetShape.Ellipse && (toScreen * RadiusXHandle(target)).DistanceTo(pointer) <= HandleRadius)
            kind = AnimatorDragKind.TargetRadiusX;
        else if (target.Shape == RigTargetShape.Ellipse && (toScreen * RadiusYHandle(target)).DistanceTo(pointer) <= HandleRadius)
            kind = AnimatorDragKind.TargetRadiusY;
        if (kind == AnimatorDragKind.None)
            return false;

        session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = kind, StartPointer = pointer };
        return true;
    }

    /// <summary>Moves the grabbed handle, the X radius also tilting, and re-bakes so the limb follows live.</summary>
    public static void Drag(Entity session, Vector2 pointer, Transform2D placement, bool free)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (Selected(session) is not { } target)
            return;

        Transform2D[] world = RigRig.WorldBones(document.Package, document.Runtime.Pose);
        Vector2 rig = placement.AffineInverse() * pointer;
        Transform2D anchor = AnchorWorld(document.Package, target, world);
        switch (session.GetComponent<AnimatorDrag>().Kind)
        {
            case AnimatorDragKind.TargetCenter:
                if (!free && ViewportSnap.IsOn(session.Store))
                    rig = AnimatorSnap.ToGrid(rig, default);
                target.Center = anchor.AffineInverse() * rig;
                break;
            case AnimatorDragKind.TargetRadiusX:
                Vector2 along = anchor.AffineInverse() * rig - target.Center;
                target.RadiusX = Math.Max(RigLimbTarget.MinRadius, along.Length());
                target.Tilt = along.Angle();
                break;
            case AnimatorDragKind.TargetRadiusY:
                Vector2 across = anchor.AffineInverse() * rig - target.Center;
                target.RadiusY = Math.Max(RigLimbTarget.MinRadius, across.Length());
                break;
        }
        Rebake(session);
    }

    public static Vector2 RadiusXHandle(RigLimbTarget target) =>
        target.Center + new Vector2(target.RadiusX, 0f).Rotated(target.Tilt);

    public static Vector2 RadiusYHandle(RigLimbTarget target) =>
        target.Center + new Vector2(0f, target.RadiusY).Rotated(target.Tilt);

    /// <summary>Puts an ellipse on the selected foot or hand, or a bone free to move, starting where it already is.</summary>
    private static void AddOrbit(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigPackage package = document.Package;
        if (AnimatorSession.CurrentClip(document) is not { } clip || clip.Duration <= 0f ||
            session.GetComponent<AnimatorModes>().Rest)
        {
            AnimatorSession.SetStatus(session, "OPEN A CLIP TO ADD AN ELLIPSE");
            return;
        }

        int selected = session.GetComponent<AnimatorSelection>().Bone;
        int bone = selected >= 0 && package.Skeleton.Bones[selected].CanTranslate
            ? selected
            : AnimatorPlantCommands.Target(package, selected);
        if (bone < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT A HAND, FOOT OR THE ROOT");
            return;
        }

        float from = clip.Loop ? clip.LoopStart : 0f;
        float to = clip.Loop ? clip.LoopEnd : clip.Duration;
        string id = package.Skeleton.Bones[bone].Id;
        if (clip.Targets.Exists(target => target.BoneId == id && target.From < to && target.To > from))
        {
            AnimatorSession.SetStatus(session, $"{id} ALREADY HAS AN OVERRIDE THERE");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
        {
            Transform2D[] world = RigRig.WorldBones(package, RigSampler.Sample(package, clip, from));
            bool limb = RigTargets.DrivesLimb(package, bone);
            int above = limb ? package.Skeleton.Bones[package.Skeleton.Bones[bone].Parent].Parent : bone;
            int anchorBone = package.Skeleton.Bones[above].Parent;
            var target = new RigLimbTarget
            {
                BoneId = id,
                AnchorId = anchorBone > 0 ? package.Skeleton.Bones[anchorBone].Id : "",
                Shape = RigTargetShape.Ellipse,
                RadiusX = DefaultRadiusX,
                RadiusY = DefaultRadiusY,
                From = from,
                To = to,
                BendPositive = limb && RigTargets.CurrentBend(package, world, bone),
            };

            // It starts where the bone is, with the ellipse on the side towards the limb's top, so
            // even a straight limb can reach all of it. A bone that moves itself has no such side.
            Transform2D anchor = AnchorWorld(package, target, world);
            Vector2 local = anchor.AffineInverse() * world[bone].Origin;
            if (limb)
            {
                Vector2 inward = anchor.BasisXformInv(world[above].Origin - world[bone].Origin);
                target.StartAngle = new Vector2(-inward.X / DefaultRadiusX, -inward.Y / DefaultRadiusY).Angle();
            }
            target.Center = local - target.OnEllipse(target.StartAngle);
            clip.Targets.Add(target);
            AnimatorSession.SetStatus(session, $"{id} ELLIPSE ADDED");
        });
    }

    private static EasePreset NextEase(EasePreset ease) => ease switch
    {
        EasePreset.Linear => EasePreset.Ease,
        EasePreset.Ease => EasePreset.EaseIn,
        EasePreset.EaseIn => EasePreset.EaseOut,
        _ => EasePreset.Linear,
    };

    /// <summary>Moves to the next bone above the limb, then back to fixed in the rig; the target stays where it is at its start.</summary>
    private static void NextAnchor(Entity session, RigLimbTarget target)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigPackage package = document.Package;
        int bone = RigRig.FindBone(package, target.BoneId);
        if (bone < 0 || AnimatorSession.CurrentClip(document) is not { } clip)
            return;

        var options = new List<string> { "" };
        int above = RigTargets.DrivesLimb(package, bone)
            ? package.Skeleton.Bones[package.Skeleton.Bones[bone].Parent].Parent
            : bone;
        for (int up = package.Skeleton.Bones[above].Parent; up >= 0; up = package.Skeleton.Bones[up].Parent)
            options.Add(package.Skeleton.Bones[up].Id);

        Transform2D[] world = RigRig.WorldBones(package, RigSampler.Sample(package, clip, target.From));
        Vector2 center = AnchorWorld(package, target, world) * target.Center;
        target.AnchorId = options[(options.IndexOf(target.AnchorId) + 1) % options.Count];
        target.Center = AnchorWorld(package, target, world).AffineInverse() * center;
    }

    private static bool TouchedOwnedKeys(RigPackage package, RigClip? before, RigClip clip)
    {
        if (before is null)
            return false;

        foreach (RigLimbTarget target in clip.Targets)
        {
            // Only a target that was there already owned anything to touch.
            if (!before.Targets.Contains(target) || !RigTargets.IsValid(package, target))
                continue;
            foreach ((int bone, RigChannelProperty property) in RigTargets.Channels(package, target))
            {
                string id = package.Skeleton.Bones[bone].Id;
                if (!SameKeys(Keys(before, id, property, target), Keys(clip, id, property, target)))
                    return true;
            }
        }
        return false;
    }

    private static List<Keyframe> Keys(RigClip clip, string bone, RigChannelProperty property, RigLimbTarget target) =>
        clip.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == bone && channel.Property == property)
            ?.Keys.FindAll(key => target.Covers(key.Time)) ?? new List<Keyframe>();

    private static bool SameKeys(List<Keyframe> a, List<Keyframe> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Time != b[i].Time || a[i].Value != b[i].Value)
                return false;
        }
        return true;
    }
}
