using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;

namespace Xanadu.Tests.Animation;

public sealed class RigTargetsTests
{
    private const int Hip = 1;
    private const int Thigh = 2;
    private const int Foot = 4;

    private static RigPackage Rig()
    {
        var package = new RigPackage();
        Add(package, "root", -1, Vector2.Zero);
        Add(package, "hip", 0, new Vector2(0, -100));
        Add(package, "thigh", 1, Vector2.Zero);
        Add(package, "shin", 2, new Vector2(4, 48));
        Add(package, "foot", 3, new Vector2(-4, 48));
        package.Clips.Add(new RigClip { Id = "c", Duration = 1f });
        return package;
    }

    private static void Add(RigPackage package, string id, int parent, Vector2 offset) =>
        package.Skeleton.Bones.Add(new RigBone { Id = id, Parent = parent, Rest = new Transform2D(0f, offset) });

    private static RigChannel Keys(
        RigClip clip,
        string bone,
        RigChannelProperty property,
        params (float Time, float Value)[] keys)
    {
        var channel = new RigChannel { Target = RigChannelTarget.Bone, TargetId = bone, Property = property };
        foreach ((float time, float value) in keys)
            channel.Keyframes.Add(new RigKeyframe { Time = time, Value = value });
        clip.Channels.Add(channel);
        return channel;
    }

    private static Vector2 Where(RigPackage package, int bone, float time) =>
        RigRig.WorldBones(package, RigSampler.Sample(package, package.Clips[0], time))[bone].Origin;

    private static RigLimbTarget Orbit(string bone, string anchor, Vector2 center) => new()
    {
        BoneId = bone, AnchorId = anchor, Shape = RigTargetShape.Ellipse, Center = center,
        RadiusX = 16f, RadiusY = 9f, From = 0f, To = 1f
    };

    [Test]
    public void AnEllipseGoesRoundAndComesBack()
    {
        RigLimbTarget target = Orbit("foot", "", new Vector2(10, 20));

        Assert.That(target.LocalAt(0f).DistanceTo(new Vector2(26, 20)), Is.LessThan(0.001f));
        Assert.That(target.LocalAt(0.25f).DistanceTo(new Vector2(10, 29)), Is.LessThan(0.001f));
        Assert.That(target.LocalAt(0.5f).DistanceTo(new Vector2(-6, 20)), Is.LessThan(0.001f));
        Assert.That(target.LocalAt(1f).DistanceTo(new Vector2(26, 20)), Is.LessThan(0.001f));
    }

    [Test]
    public void TiltSweepAndEaseShapeTheTrip()
    {
        RigLimbTarget back = Orbit("foot", "", Vector2.Zero) with { Sweep = -float.Tau };
        Assert.That(back.LocalAt(0.25f).Y, Is.EqualTo(-9f).Within(0.001f));

        RigLimbTarget tilted = Orbit("foot", "", Vector2.Zero) with { Tilt = float.Pi / 2 };
        Assert.That(tilted.LocalAt(0f).DistanceTo(new Vector2(0, 16)), Is.LessThan(0.001f));

        RigLimbTarget slow = Orbit("foot", "", Vector2.Zero) with { Ease = EasePreset.EaseIn };
        Assert.That(slow.LocalAt(0.5f).DistanceTo(Orbit("foot", "", Vector2.Zero).LocalAt(0.25f)), Is.LessThan(0.001f));
    }

    [Test]
    public void ALimbBakedRoundAnEllipseFollowsItWithFarFewerKeysThanMoments()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        RigLimbTarget target = Orbit("foot", "", new Vector2(0, -25));
        clip.Targets.Add(target);

        RigTargets.Bake(package, clip);

        for (int i = 0; i <= 40; i++)
        {
            float time = i / 40f;
            Assert.That(
                Where(package, Foot, time).DistanceTo(target.LocalAt(time)),
                Is.LessThan(RigTargets.DriftTolerance + 0.25f),
                $"at {time}");
        }

        int keys = clip.Channels.Find(c => c.TargetId == "thigh" && c.Property == RigChannelProperty.Rotation)!
            .Keyframes.Count;
        Assert.That(keys, Is.LessThan(13), "a full turn should take a handful of curved keys");
        TestContext.Out.WriteLine($"ELLIPSE KEYS {keys}");
    }

    [Test]
    public void AnEllipseOnlyKeysWhatBendsTheLimb()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        clip.Targets.Add(Orbit("foot", "", new Vector2(0, -25)));

        RigTargets.Bake(package, clip);

        Assert.That(clip.Channels.Exists(c => c.TargetId == "foot"), Is.False, "the end bone turns freely");
        Assert.That(clip.Channels.TrueForAll(c => c.Property == RigChannelProperty.Rotation), Is.True);
    }

    [Test]
    public void ABoneFreeToMoveIsMovedInsteadOfBendingItsParents()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        RigLimbTarget target = Orbit("root", "", new Vector2(0, 5));
        clip.Targets.Add(target);

        RigTargets.Bake(package, clip);

        Assert.That(clip.Channels.TrueForAll(c => c.TargetId == "root"), Is.True);
        for (int i = 0; i <= 20; i++)
            Assert.That(
                Where(package, 0, i / 20f).DistanceTo(target.LocalAt(i / 20f)),
                Is.LessThan(RigTargets.DriftTolerance + 0.25f));
    }

    [Test]
    public void AnchoredToABoneTheOrbitTravelsWithIt()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Keys(clip, "root", RigChannelProperty.TranslationX, (0f, 0f), (1f, 12f));
        RigLimbTarget target = Orbit("foot", "hip", new Vector2(0, 75));
        clip.Targets.Add(target);

        RigTargets.Bake(package, clip);

        Vector2 local = target.LocalAt(0.5f);
        Vector2 world = RigRig.WorldBones(package, RigSampler.Sample(package, clip, 0.5f))[Hip] * local;
        Assert.That(Where(package, Foot, 0.5f).DistanceTo(world), Is.LessThan(RigTargets.DriftTolerance + 0.25f));
    }

    [Test]
    public void BakingAgainGivesTheSameKeys()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        clip.Targets.Add(Orbit("foot", "", new Vector2(0, -25)));
        RigTargets.Bake(package, clip);
        RigPackage first = RigPackageCopy.Clone(package);

        RigTargets.Bake(package, clip);

        Assert.That(RigPackageCopy.AreEqual(first, package), Is.True);
    }

    [Test]
    public void ReBakingFollowsAParentThatChanged()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        var spot = new RigLimbTarget
            { BoneId = "foot", Shape = RigTargetShape.Spot, Center = new Vector2(0, -4), From = 0f, To = 1f };
        clip.Targets.Add(spot);
        RigTargets.Bake(package, clip);

        Keys(clip, "root", RigChannelProperty.TranslationY, (0f, 0f), (1f, 30f));
        RigTargets.Bake(package, clip);

        Assert.That(
            Where(package, Foot, 0.6f).DistanceTo(new Vector2(0, -4)),
            Is.LessThan(RigTargets.DriftTolerance + 0.25f));
    }

    [Test]
    public void OnlyKeysInsideTheSpanAreReplaced()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        RigChannel thigh = Keys(clip, "thigh", RigChannelProperty.Rotation, (0.1f, 1f), (0.9f, 0.2f));
        clip.Targets.Add(Orbit("foot", "", new Vector2(0, -25)) with { From = 0.2f, To = 0.5f });

        RigTargets.Bake(package, clip);

        Assert.That(thigh.Keyframes.Exists(key => key.Time == 0.1f), Is.True);
        Assert.That(thigh.Keyframes.Exists(key => key.Time == 0.9f), Is.True);
    }

    [Test]
    public void ATargetWithAMissingBoneIsLeftAloneAndItsKeysStay()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        RigChannel thigh = Keys(clip, "thigh", RigChannelProperty.Rotation, (0.5f, 1f));
        clip.Targets.Add(Orbit("gone", "", Vector2.Zero));
        clip.Targets.Add(Orbit("foot", "gone", Vector2.Zero));

        RigTargets.Bake(package, clip);

        Assert.That(thigh.Keyframes, Has.Count.EqualTo(1));
    }

    [Test]
    public void TargetsSurviveTheFileCopyAndRename()
    {
        RigPackage package = Rig();
        RigLimbTarget target = Orbit("foot", "hip", new Vector2(3, 4)) with
        {
            Tilt = 0.5f, StartAngle = 1f, Sweep = -3f, From = 0.1f, To = 0.8f,
            Ease = EasePreset.EaseOut, HoldFacing = true, Facing = 0.25f, BendPositive = true
        };
        package.Clips[0].Targets.Add(target);

        RigPackage read = RigBinary.Read(RigBinary.Write(package));
        Assert.That(read.Clips[0].Targets[0], Is.EqualTo(target));

        RigPackage copy = RigPackageCopy.Clone(package);
        Assert.That(RigPackageCopy.AreEqual(package, copy), Is.True);
        copy.Clips[0].Targets[0].RadiusX = 1f;
        Assert.That(RigPackageCopy.AreEqual(package, copy), Is.False);

        RigRigAuthoring.RenameBone(package, Foot, "boot");
        RigRigAuthoring.RenameBone(package, Hip, "pelvis");
        Assert.That(package.Clips[0].Targets[0].BoneId, Is.EqualTo("boot"));
        Assert.That(package.Clips[0].Targets[0].AnchorId, Is.EqualTo("pelvis"));
    }
}
