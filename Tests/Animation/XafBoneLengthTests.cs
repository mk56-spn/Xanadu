using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigBoneLengthTests
{
    private static RigPackage Package(bool childMovable = false)
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "root", Parent = -1 });
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "child",
                Parent = 0,
                Rest = new Transform2D(0f, new Vector2(0, -50)),
                AllowTranslation = childMovable
            });
        var clip = new RigClip { Id = "idle", Duration = 1f };
        AddKey(clip, "root", RigChannelProperty.TranslationX, 10f);
        AddKey(clip, "child", RigChannelProperty.TranslationX, 30f);
        AddKey(clip, "child", RigChannelProperty.Rotation, 0.5f);
        package.Clips.Add(clip);
        return package;
    }

    private static void AddKey(RigClip clip, string bone, RigChannelProperty property, float value)
    {
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = bone,
            Property = property
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 0f, Value = value });
        clip.Channels.Add(channel);
    }

    [Test]
    public void TheRootAlwaysTranslates()
    {
        RigPackage package = Package();

        RigPose pose = RigSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[0].Translation.X, Is.EqualTo(10f));
    }

    [Test]
    public void ABoneIsFixedLengthByDefaultButStillRotates()
    {
        RigPackage package = Package();

        RigPose pose = RigSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[1].Translation, Is.EqualTo(new Vector2(0, -50)));
        Assert.That(pose.Bones[1].Rotation, Is.EqualTo(0.5f));
    }

    [Test]
    public void AMovableBoneFollowsItsTranslationChannel()
    {
        RigPackage package = Package(true);

        RigPose pose = RigSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[1].Translation.X, Is.EqualTo(30f));
    }

    [Test]
    public void KeyingAFixedLengthBoneWritesNoTranslationChannels()
    {
        RigPackage package = Package();
        var clip = new RigClip { Id = "new", Duration = 1f };

        RigAuthoring.KeyBoneTransform(package, clip, 1, 0.5f, RigTransform.Identity);

        Assert.That(clip.Channels, Has.Count.EqualTo(3));
        Assert.That(clip.Channels.Exists(channel => RigSampler.IsTranslation(channel.Property)), Is.False);
    }

    [Test]
    public void KeyingTheRootOrAMovableBoneWritesTranslation()
    {
        RigPackage package = Package(true);
        var clip = new RigClip { Id = "new", Duration = 1f };

        RigAuthoring.KeyBoneTransform(package, clip, 0, 0.5f, RigTransform.Identity);
        RigAuthoring.KeyBoneTransform(package, clip, 1, 0.5f, RigTransform.Identity);

        Assert.That(clip.Channels, Has.Count.EqualTo(10));
    }

    [Test]
    public void TheMovableFlagSurvivesASaveAndLoad()
    {
        RigPackage package = Package(true);

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));

        Assert.That(loaded.Skeleton.Bones[1].AllowTranslation, Is.True);
        Assert.That(loaded.Skeleton.Bones[0].AllowTranslation, Is.False);
        Assert.That(loaded.Minor, Is.EqualTo(RigPackage.MinorVersion));
    }

    /// <summary>
    /// A leg hanging straight down, hip at the origin: a 50 long thigh, a 50
    /// long shin and a foot, each with its own art.
    /// </summary>
    private static RigPackage Leg()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "thigh", Parent = -1 });
        package.Skeleton.Bones.Add(
            new RigBone { Id = "shin", Parent = 0, Rest = new Transform2D(0f, new Vector2(0, 50)) });
        package.Skeleton.Bones.Add(
            new RigBone { Id = "foot", Parent = 1, Rest = new Transform2D(0f, new Vector2(0, 50)) });
        foreach (string id in new[] { "thigh", "shin", "foot" })
            package.Attachments.Add(new RigAttachment { Group = id, BoneId = id });
        return package;
    }

    [Test]
    public void AKneeBroughtOutOfThePlaneForeshortensTheThighAndTheShinAlike()
    {
        RigPackage package = Leg();
        var clip = new RigClip { Id = "chamber", Duration = 1f };
        AddKey(clip, "shin", RigChannelProperty.Depth, 30f);
        package.Clips.Add(clip);

        RigPose pose = RigSampler.Sample(package, clip, 0f);
        Transform2D[] bones = RigRig.WorldBones(package, pose);
        Transform2D[] art = RigRig.WorldAttachments(package, pose, bones);

        // Each 50 long segment spans a depth gap of 30, so 40 of it shows.
        Assert.That(bones[0].Origin, Is.EqualTo(Vector2.Zero), "the hip stays put");
        Assert.That(bones[1].Origin.Y, Is.EqualTo(40f).Within(1e-3f), "the knee rises");
        Assert.That(bones[2].Origin.Y, Is.EqualTo(80f).Within(1e-3f), "the ankle rises with both");
        Assert.That(art[0].Scale.Y, Is.EqualTo(0.8f).Within(1e-4f), "the thigh's art squashes along it");
        Assert.That(art[1].Scale.Y, Is.EqualTo(0.8f).Within(1e-4f), "and so does the shin's");
        Assert.That(art[0].Scale.X, Is.EqualTo(1f).Within(1e-4f), "neither gets narrower");
        Assert.That(art[2].Scale, Is.EqualTo(Vector2.One), "the foot, in the plane at both ends, keeps its shape");
    }

    [Test]
    public void ASegmentSquashesAlongItsOwnDirectionWhicheverWayItPoints()
    {
        RigPackage package = Leg();
        package.Skeleton.Bones[1].Rest = new Transform2D(0f, new Vector2(50, 0));
        var clip = new RigClip { Id = "chamber", Duration = 1f };
        AddKey(clip, "shin", RigChannelProperty.Depth, 30f);
        package.Clips.Add(clip);

        RigPose pose = RigSampler.Sample(package, clip, 0f);
        Transform2D squash = RigRig.Foreshorten(package, pose, 0);

        Assert.That(squash * new Vector2(10, 0), Is.EqualTo(new Vector2(8, 0)));
        Assert.That(squash * new Vector2(0, 10), Is.EqualTo(new Vector2(0, 10)));
    }

    [Test]
    public void AJointCannotSinkFurtherThanItsSegmentIsLong()
    {
        Assert.That(RigRig.Projection(50f, 0f, 80f), Is.EqualTo(RigRig.MinProjection));
        Assert.That(RigRig.Projection(50f, 10f, 10f), Is.EqualTo(1f));
    }
}
