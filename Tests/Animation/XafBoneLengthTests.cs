using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafBoneLengthTests
{
    private static XafPackage Package(bool childMovable = false)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1 });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "child",
            Parent = 0,
            Rest = new Transform2D(0f, new Vector2(0, -50)),
            AllowTranslation = childMovable,
        });
        var clip = new XafClip { Id = "idle", Duration = 1f };
        AddKey(clip, "root", XafChannelProperty.TranslationX, 10f);
        AddKey(clip, "child", XafChannelProperty.TranslationX, 30f);
        AddKey(clip, "child", XafChannelProperty.Rotation, 0.5f);
        package.Clips.Add(clip);
        return package;
    }

    private static void AddKey(
        XafClip clip,
        string bone,
        XafChannelProperty property,
        float value)
    {
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = bone,
            Property = property,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = value });
        clip.Channels.Add(channel);
    }

    [Test]
    public void TheRootAlwaysTranslates()
    {
        XafPackage package = Package();

        XafPose pose = XafSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[0].Translation.X, Is.EqualTo(10f));
    }

    [Test]
    public void ABoneIsFixedLengthByDefaultButStillRotates()
    {
        XafPackage package = Package();

        XafPose pose = XafSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[1].Translation, Is.EqualTo(new Vector2(0, -50)));
        Assert.That(pose.Bones[1].Rotation, Is.EqualTo(0.5f));
    }

    [Test]
    public void AMovableBoneFollowsItsTranslationChannel()
    {
        XafPackage package = Package(childMovable: true);

        XafPose pose = XafSampler.Sample(package, package.Clips[0], 0f);

        Assert.That(pose.Bones[1].Translation.X, Is.EqualTo(30f));
    }

    [Test]
    public void KeyingAFixedLengthBoneWritesNoTranslationChannels()
    {
        XafPackage package = Package();
        var clip = new XafClip { Id = "new", Duration = 1f };

        XafAuthoring.KeyBoneTransform(package, clip, 1, 0.5f, XafTransform.Identity);

        Assert.That(clip.Channels, Has.Count.EqualTo(3));
        Assert.That(clip.Channels.Exists(channel =>
            XafSampler.IsTranslation(channel.Property)), Is.False);
    }

    [Test]
    public void KeyingTheRootOrAMovableBoneWritesTranslation()
    {
        XafPackage package = Package(childMovable: true);
        var clip = new XafClip { Id = "new", Duration = 1f };

        XafAuthoring.KeyBoneTransform(package, clip, 0, 0.5f, XafTransform.Identity);
        XafAuthoring.KeyBoneTransform(package, clip, 1, 0.5f, XafTransform.Identity);

        Assert.That(clip.Channels, Has.Count.EqualTo(10));
    }

    [Test]
    public void TheMovableFlagSurvivesASaveAndLoad()
    {
        XafPackage package = Package(childMovable: true);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(loaded.Skeleton.Bones[1].AllowTranslation, Is.True);
        Assert.That(loaded.Skeleton.Bones[0].AllowTranslation, Is.False);
        Assert.That(loaded.Minor, Is.EqualTo(XafPackage.MinorVersion));
    }

    /// <summary>
    /// A leg hanging straight down, hip at the origin: a 50 long thigh, a 50
    /// long shin and a foot, each with its own art.
    /// </summary>
    private static XafPackage Leg()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "thigh", Parent = -1 });
        package.Skeleton.Bones.Add(new XafBone { Id = "shin", Parent = 0, Rest = new Transform2D(0f, new Vector2(0, 50)) });
        package.Skeleton.Bones.Add(new XafBone { Id = "foot", Parent = 1, Rest = new Transform2D(0f, new Vector2(0, 50)) });
        foreach (string id in new[] { "thigh", "shin", "foot" })
            package.Attachments.Add(new XafAttachment { Group = id, BoneId = id });
        return package;
    }

    [Test]
    public void AKneeBroughtOutOfThePlaneForeshortensTheThighAndTheShinAlike()
    {
        XafPackage package = Leg();
        var clip = new XafClip { Id = "chamber", Duration = 1f };
        AddKey(clip, "shin", XafChannelProperty.Depth, 30f);
        package.Clips.Add(clip);

        XafPose pose = XafSampler.Sample(package, clip, 0f);
        Transform2D[] bones = XafRig.WorldBones(package, pose);
        Transform2D[] art = XafRig.WorldAttachments(package, pose, bones);

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
        XafPackage package = Leg();
        package.Skeleton.Bones[1].Rest = new Transform2D(0f, new Vector2(50, 0));
        var clip = new XafClip { Id = "chamber", Duration = 1f };
        AddKey(clip, "shin", XafChannelProperty.Depth, 30f);
        package.Clips.Add(clip);

        XafPose pose = XafSampler.Sample(package, clip, 0f);
        Transform2D squash = XafRig.Foreshorten(package, pose, 0);

        Assert.That(squash * new Vector2(10, 0), Is.EqualTo(new Vector2(8, 0)));
        Assert.That(squash * new Vector2(0, 10), Is.EqualTo(new Vector2(0, 10)));
    }

    [Test]
    public void AJointCannotSinkFurtherThanItsSegmentIsLong()
    {
        Assert.That(XafRig.Projection(50f, 0f, 80f), Is.EqualTo(XafRig.MinProjection));
        Assert.That(XafRig.Projection(50f, 10f, 10f), Is.EqualTo(1f));
    }
}
