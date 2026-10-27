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

    [Test]
    public void FilesFromBeforeTheFlagStillLoadAsFixedLength()
    {
        XafPackage package = Package(childMovable: true);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package, minor: 0));

        Assert.That(loaded.Minor, Is.EqualTo(0));
        Assert.That(loaded.Skeleton.Bones[1].AllowTranslation, Is.False);
        Assert.That(loaded.Skeleton.Bones[1].Rest.Origin, Is.EqualTo(new Vector2(0, -50)));
        Assert.That(loaded.Clips[0].Channels, Has.Count.EqualTo(3));
    }
}
