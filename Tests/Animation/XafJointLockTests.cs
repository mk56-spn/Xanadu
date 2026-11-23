using Xanadu.Serialisation.Animation;
using System.Linq;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Bend locks: a locked knee is straightened when turned the wrong way, left
/// alone when bent its own way, IK folds it to its side, and the lock is saved.
/// The rig is one leg hanging from the hips: thigh, shin, foot.
/// </summary>
[TestFixture]
public sealed class RigJointLockTests
{
    private const int Thigh = 1;
    private const int Shin = 2;

    private static RigPackage Leg()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "thigh", new Vector2(0, -100));
        RigRigAuthoring.AddBone(package, Thigh, "shin", new Vector2(0, 50));
        RigRigAuthoring.AddBone(package, Shin, "foot", new Vector2(0, 50));
        return package;
    }

    private static float Bend(RigPackage package, RigPose pose)
    {
        RigJointLocks.TryBend(package, RigRig.WorldBones(package, pose), Shin, out float bend);
        return bend;
    }

    [Test]
    public void AKneeTurnedTheWrongWayIsStraightenedAndOneTurnedItsOwnWayIsNot()
    {
        RigPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = 1;

        RigPose right = RigClothLayout.RestPose(package);
        right.Bones[Shin].Rotation += 0.8f;
        float before = Bend(package, right);
        Assert.That(RigJointLocks.Apply(package, right), Is.False);
        Assert.That(Bend(package, right), Is.EqualTo(before).Within(1e-4f));

        RigPose wrong = RigClothLayout.RestPose(package);
        wrong.Bones[Shin].Rotation -= 0.8f;
        Assert.That(Bend(package, wrong), Is.LessThan(0f));
        Assert.That(RigJointLocks.Apply(package, wrong), Is.True);
        Assert.That(Bend(package, wrong), Is.EqualTo(0f).Within(1e-4f), "stops at straight");
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void IkFoldsALockedKneeToItsSide(int locked)
    {
        RigPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = locked;
        Transform2D[] world = RigRig.WorldBones(package, RigClothLayout.RestPose(package));
        Vector2[] chain = { world[Thigh].Origin, world[Shin].Origin, world[3].Origin };

        Vector2[] solved = RigFabrik.Solve(
            chain,
            chain[0] + new Vector2(30, 60),
            RigJointLocks.BendPositive(package, Shin)!.Value);

        float bend = (solved[1] - solved[0]).Cross(solved[2] - solved[1]);
        Assert.That(Math.Sign(bend), Is.EqualTo(locked));
    }

    [Test]
    public void LocksAreSaved()
    {
        RigPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = -1;

        Assert.That(RigBinary.Read(RigBinary.Write(package)).Skeleton.Bones[Shin].Bend, Is.EqualTo(-1));
        Assert.That(RigPackageCopy.AreEqual(package, RigPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void ALimitedBoneIsHeldInsideItsRangeAndLeftAloneWithin()
    {
        RigPackage package = Leg();
        RigBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -0.5f;
        thigh.LimitMax = 0.8f;

        RigPose inside = RigClothLayout.RestPose(package);
        inside.Bones[Thigh].Rotation = 0.3f;
        Assert.That(RigJointLocks.Apply(package, inside), Is.False);
        Assert.That(inside.Bones[Thigh].Rotation, Is.EqualTo(0.3f));

        RigPose over = RigClothLayout.RestPose(package);
        over.Bones[Thigh].Rotation = 1.4f;
        Assert.That(RigJointLocks.Apply(package, over), Is.True);
        Assert.That(over.Bones[Thigh].Rotation, Is.EqualTo(0.8f).Within(1e-5f));

        RigPose under = RigClothLayout.RestPose(package);
        under.Bones[Thigh].Rotation = -2f;
        RigJointLocks.Apply(package, under);
        Assert.That(under.Bones[Thigh].Rotation, Is.EqualTo(-0.5f).Within(1e-5f));
    }

    [Test]
    public void ATurnRoundPastHalfARevolutionStopsAtTheNearerEnd()
    {
        var bone = new RigBone { Limited = true, LimitMin = -0.2f, LimitMax = 3f };

        // -3 is 0.28 short of the high end the other way round, but 2.8 past the low end.
        Assert.That(RigJointLocks.Clamp(bone, -3f), Is.EqualTo(3f));
        Assert.That(RigJointLocks.Clamp(bone, -0.6f), Is.EqualTo(-0.2f));
    }

    [Test]
    public void LimitsCountFromTheRestAngle()
    {
        RigPackage package = Leg();
        RigBone shin = package.Skeleton.Bones[Shin];
        shin.Rest = new Transform2D(1f, shin.Rest.Origin);
        shin.Limited = true;
        shin.LimitMin = -0.1f;
        shin.LimitMax = 0.1f;

        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[Shin].Rotation = 1.5f;
        RigJointLocks.Apply(package, pose);

        Assert.That(pose.Bones[Shin].Rotation, Is.EqualTo(1.1f).Within(1e-5f));
    }

    [Test]
    public void LimitsAreSaved()
    {
        RigPackage package = Leg();
        RigBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -0.25f;
        thigh.LimitMax = 1.5f;

        RigBone loaded = RigBinary.Read(RigBinary.Write(package)).Skeleton.Bones[Thigh];
        Assert.That((loaded.Limited, loaded.LimitMin, loaded.LimitMax), Is.EqualTo((true, -0.25f, 1.5f)));
        Assert.That(RigPackageCopy.AreEqual(package, RigPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void ACrossedOrOutOfRangeLimitIsInvalid()
    {
        RigPackage package = Leg();
        package.Skeleton.Bones[Thigh].LimitMin = 1f;
        package.Skeleton.Bones[Thigh].LimitMax = 0f;

        Assert.That(RigValidation.Validate(package), Has.Some.Contains("angle limit"));
    }

    private static RigClip Keyed(RigPackage package, params (string Bone, float Value)[] keys)
    {
        var clip = new RigClip { Id = "pose", Duration = 1f };
        foreach ((string bone, float value) in keys)
        {
            var channel = new RigChannel
                { Target = RigChannelTarget.Bone, TargetId = bone, Property = RigChannelProperty.Rotation };
            channel.Keyframes.Add(
                new RigKeyframe { Time = 0f, Value = value, Interpolation = RigInterpolation.SineInOut });
            clip.Channels.Add(channel);
        }

        package.Clips.Add(clip);
        return clip;
    }

    [Test]
    public void FittingAHipPastItsRangeRebendsTheKneeToKeepTheFootNear()
    {
        RigPackage package = Leg();
        RigBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -2.6f;
        thigh.LimitMax = 0.5f;
        RigClip clip = Keyed(package, ("thigh", 0.8f), ("shin", 0.2f), ("foot", 0.1f));
        Transform2D[] before = RigRig.WorldBones(package, RigSampler.Sample(package, clip, 0f));

        Assert.That(RigJointLocks.FitClip(package, clip), Is.GreaterThan(0));

        RigPose fitted = RigSampler.Sample(package, clip, 0f);
        Transform2D[] after = RigRig.WorldBones(package, fitted);
        Assert.That(fitted.Bones[Thigh].Rotation, Is.EqualTo(0.5f).Within(1e-4f));
        // Pulled 17 degrees back, the old ankle is out of the shin's reach from the new knee; turning
        // the whole leg would move it about 30, re-bending the knee keeps it as near as it will go.
        Assert.That(after[3].Origin.DistanceTo(before[3].Origin), Is.LessThan(8f), "the ankle stays as near as it can");
        Assert.That(after[3].Rotation, Is.EqualTo(before[3].Rotation).Within(1e-3f), "and the foot keeps its angle");
        Assert.That(
            clip.Channels.All(channel => channel.Keyframes[0].Interpolation == RigInterpolation.SineInOut),
            Is.True,
            "keys keep their easing");
    }

    [Test]
    public void FittingAKneePastStraightReaimsTheHipSoTheFootStaysPut()
    {
        RigPackage package = Leg();
        RigBone shin = package.Skeleton.Bones[Shin];
        shin.Limited = true;
        shin.LimitMin = 0f;
        shin.LimitMax = 2.6f;
        RigClip clip = Keyed(package, ("thigh", -0.3f), ("shin", -0.15f), ("foot", 0f));
        Transform2D[] before = RigRig.WorldBones(package, RigSampler.Sample(package, clip, 0f));

        RigJointLocks.FitClip(package, clip);

        RigPose fitted = RigSampler.Sample(package, clip, 0f);
        Transform2D[] after = RigRig.WorldBones(package, fitted);
        Assert.That(fitted.Bones[Shin].Rotation, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(after[3].Origin.DistanceTo(before[3].Origin), Is.LessThan(1.5f));
        Assert.That(after[3].Rotation, Is.EqualTo(before[3].Rotation).Within(1e-3f));
    }

    [Test]
    public void FittingNeverKeysABoneTheClipDoesNotTurn()
    {
        RigPackage package = Leg();
        RigBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -1f;
        thigh.LimitMax = 0.5f;
        RigClip clip = Keyed(package, ("thigh", 0.8f));

        RigJointLocks.FitClip(package, clip);

        Assert.That(
            clip.Channels.Select(channel => channel.TargetId),
            Is.EqualTo(new[] { "thigh" }),
            "an override that never keyed the knee or foot must not start to");
    }
}
