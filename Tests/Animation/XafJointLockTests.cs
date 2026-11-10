using System.Linq;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Bend locks: a locked knee is straightened when turned the wrong way, left
/// alone when bent its own way, IK folds it to its side, and the lock is saved.
/// The rig is one leg hanging from the hips: thigh, shin, foot.
/// </summary>
[TestFixture]
public sealed class XafJointLockTests
{
    private const int Thigh = 1;
    private const int Shin = 2;

    private static XafPackage Leg()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "thigh", new Vector2(0, -100));
        XafRigAuthoring.AddBone(package, Thigh, "shin", new Vector2(0, 50));
        XafRigAuthoring.AddBone(package, Shin, "foot", new Vector2(0, 50));
        return package;
    }

    private static float Bend(XafPackage package, XafPose pose)
    {
        XafJointLocks.TryBend(package, XafRig.WorldBones(package, pose), Shin, out float bend);
        return bend;
    }

    [Test]
    public void AKneeTurnedTheWrongWayIsStraightenedAndOneTurnedItsOwnWayIsNot()
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = 1;

        XafPose right = XafClothLayout.RestPose(package);
        right.Bones[Shin].Rotation += 0.8f;
        float before = Bend(package, right);
        Assert.That(XafJointLocks.Apply(package, right), Is.False);
        Assert.That(Bend(package, right), Is.EqualTo(before).Within(1e-4f));

        XafPose wrong = XafClothLayout.RestPose(package);
        wrong.Bones[Shin].Rotation -= 0.8f;
        Assert.That(Bend(package, wrong), Is.LessThan(0f));
        Assert.That(XafJointLocks.Apply(package, wrong), Is.True);
        Assert.That(Bend(package, wrong), Is.EqualTo(0f).Within(1e-4f), "stops at straight");
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void IkFoldsALockedKneeToItsSide(int locked)
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = locked;
        Transform2D[] world = XafRig.WorldBones(package, XafClothLayout.RestPose(package));
        Vector2[] chain = { world[Thigh].Origin, world[Shin].Origin, world[3].Origin };

        Vector2[] solved = XafFabrik.Solve(chain, chain[0] + new Vector2(30, 60),
            XafJointLocks.BendPositive(package, Shin)!.Value);

        float bend = (solved[1] - solved[0]).Cross(solved[2] - solved[1]);
        Assert.That(Mathf.Sign(bend), Is.EqualTo(locked));
    }

    [Test]
    public void LocksAreSaved()
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = -1;

        Assert.That(XafBinary.Read(XafBinary.Write(package)).Skeleton.Bones[Shin].Bend, Is.EqualTo(-1));
        Assert.That(XafPackageCopy.AreEqual(package, XafPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void ALimitedBoneIsHeldInsideItsRangeAndLeftAloneWithin()
    {
        XafPackage package = Leg();
        XafBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -0.5f;
        thigh.LimitMax = 0.8f;

        XafPose inside = XafClothLayout.RestPose(package);
        inside.Bones[Thigh].Rotation = 0.3f;
        Assert.That(XafJointLocks.Apply(package, inside), Is.False);
        Assert.That(inside.Bones[Thigh].Rotation, Is.EqualTo(0.3f));

        XafPose over = XafClothLayout.RestPose(package);
        over.Bones[Thigh].Rotation = 1.4f;
        Assert.That(XafJointLocks.Apply(package, over), Is.True);
        Assert.That(over.Bones[Thigh].Rotation, Is.EqualTo(0.8f).Within(1e-5f));

        XafPose under = XafClothLayout.RestPose(package);
        under.Bones[Thigh].Rotation = -2f;
        XafJointLocks.Apply(package, under);
        Assert.That(under.Bones[Thigh].Rotation, Is.EqualTo(-0.5f).Within(1e-5f));
    }

    [Test]
    public void ATurnRoundPastHalfARevolutionStopsAtTheNearerEnd()
    {
        var bone = new XafBone { Limited = true, LimitMin = -0.2f, LimitMax = 3f };

        // -3 is 0.28 short of the high end the other way round, but 2.8 past the low end.
        Assert.That(XafJointLocks.Clamp(bone, -3f), Is.EqualTo(3f));
        Assert.That(XafJointLocks.Clamp(bone, -0.6f), Is.EqualTo(-0.2f));
    }

    [Test]
    public void LimitsCountFromTheRestAngle()
    {
        XafPackage package = Leg();
        XafBone shin = package.Skeleton.Bones[Shin];
        shin.Rest = new Transform2D(1f, shin.Rest.Origin);
        shin.Limited = true;
        shin.LimitMin = -0.1f;
        shin.LimitMax = 0.1f;

        XafPose pose = XafClothLayout.RestPose(package);
        pose.Bones[Shin].Rotation = 1.5f;
        XafJointLocks.Apply(package, pose);

        Assert.That(pose.Bones[Shin].Rotation, Is.EqualTo(1.1f).Within(1e-5f));
    }

    [Test]
    public void LimitsAreSaved()
    {
        XafPackage package = Leg();
        XafBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -0.25f;
        thigh.LimitMax = 1.5f;

        XafBone loaded = XafBinary.Read(XafBinary.Write(package)).Skeleton.Bones[Thigh];
        Assert.That((loaded.Limited, loaded.LimitMin, loaded.LimitMax), Is.EqualTo((true, -0.25f, 1.5f)));
        Assert.That(XafPackageCopy.AreEqual(package, XafPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void ACrossedOrOutOfRangeLimitIsInvalid()
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Thigh].LimitMin = 1f;
        package.Skeleton.Bones[Thigh].LimitMax = 0f;

        Assert.That(XafValidation.Validate(package), Has.Some.Contains("angle limit"));
    }

    private static XafClip Keyed(XafPackage package, params (string Bone, float Value)[] keys)
    {
        var clip = new XafClip { Id = "pose", Duration = 1f };
        foreach (var (bone, value) in keys)
        {
            var channel = new XafChannel { Target = XafChannelTarget.Bone, TargetId = bone, Property = XafChannelProperty.Rotation };
            channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = value, Interpolation = XafInterpolation.SineInOut });
            clip.Channels.Add(channel);
        }
        package.Clips.Add(clip);
        return clip;
    }

    [Test]
    public void FittingAHipPastItsRangeRebendsTheKneeToKeepTheFootNear()
    {
        XafPackage package = Leg();
        XafBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -2.6f;
        thigh.LimitMax = 0.5f;
        XafClip clip = Keyed(package, ("thigh", 0.8f), ("shin", 0.2f), ("foot", 0.1f));
        Transform2D[] before = XafRig.WorldBones(package, XafSampler.Sample(package, clip, 0f));

        Assert.That(XafJointLocks.FitClip(package, clip), Is.GreaterThan(0));

        XafPose fitted = XafSampler.Sample(package, clip, 0f);
        Transform2D[] after = XafRig.WorldBones(package, fitted);
        Assert.That(fitted.Bones[Thigh].Rotation, Is.EqualTo(0.5f).Within(1e-4f));
        // Pulled 17 degrees back, the old ankle is out of the shin's reach from the new knee; turning
        // the whole leg would move it about 30, re-bending the knee keeps it as near as it will go.
        Assert.That(after[3].Origin.DistanceTo(before[3].Origin), Is.LessThan(8f), "the ankle stays as near as it can");
        Assert.That(after[3].Rotation, Is.EqualTo(before[3].Rotation).Within(1e-3f), "and the foot keeps its angle");
        Assert.That(clip.Channels.All(channel => channel.Keyframes[0].Interpolation == XafInterpolation.SineInOut), Is.True,
            "keys keep their easing");
    }

    [Test]
    public void FittingAKneePastStraightReaimsTheHipSoTheFootStaysPut()
    {
        XafPackage package = Leg();
        XafBone shin = package.Skeleton.Bones[Shin];
        shin.Limited = true;
        shin.LimitMin = 0f;
        shin.LimitMax = 2.6f;
        XafClip clip = Keyed(package, ("thigh", -0.3f), ("shin", -0.15f), ("foot", 0f));
        Transform2D[] before = XafRig.WorldBones(package, XafSampler.Sample(package, clip, 0f));

        XafJointLocks.FitClip(package, clip);

        XafPose fitted = XafSampler.Sample(package, clip, 0f);
        Transform2D[] after = XafRig.WorldBones(package, fitted);
        Assert.That(fitted.Bones[Shin].Rotation, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(after[3].Origin.DistanceTo(before[3].Origin), Is.LessThan(1.5f));
        Assert.That(after[3].Rotation, Is.EqualTo(before[3].Rotation).Within(1e-3f));
    }

    [Test]
    public void FittingNeverKeysABoneTheClipDoesNotTurn()
    {
        XafPackage package = Leg();
        XafBone thigh = package.Skeleton.Bones[Thigh];
        thigh.Limited = true;
        thigh.LimitMin = -1f;
        thigh.LimitMax = 0.5f;
        XafClip clip = Keyed(package, ("thigh", 0.8f));

        XafJointLocks.FitClip(package, clip);

        Assert.That(clip.Channels.Select(channel => channel.TargetId), Is.EqualTo(new[] { "thigh" }),
            "an override that never keyed the knee or foot must not start to");
    }
}
