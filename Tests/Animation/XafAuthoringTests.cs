using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafAuthoringTests
{
    [Test]
    public void InsertsKeyframesInOrderAndReplacesMatchingTime()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];

        Assert.That(XafAuthoring.SetBoneProperty(
            package, clip, "root", XafChannelProperty.Rotation,
            1f, 2f), Is.True);
        Assert.That(XafAuthoring.SetBoneProperty(
            package, clip, "root", XafChannelProperty.Rotation,
            0.5f, 1f), Is.True);
        Assert.That(XafAuthoring.SetBoneProperty(
            package, clip, "root", XafChannelProperty.Rotation,
            1f, 3f), Is.True);

        XafChannel channel = clip.Channels[0];
        Assert.That(channel.Keyframes, Has.Count.EqualTo(2));
        Assert.That(channel.Keyframes[0].Time, Is.EqualTo(0.5f));
        Assert.That(channel.Keyframes[1].Time, Is.EqualTo(1f));
        Assert.That(channel.Keyframes[1].Value, Is.EqualTo(3f));
    }

    [Test]
    public void RejectsMissingBone()
    {
        XafPackage package = Package();

        Assert.That(XafAuthoring.SetBoneProperty(
            package, package.Clips[0], "missing",
            XafChannelProperty.Rotation, 0f, 1f), Is.False);
    }

    [Test]
    public void KeysFullBoneTransform()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];

        XafAuthoring.KeyBoneTransform(
            package,
            clip,
            0,
            0.5f,
            new XafTransform
            {
                Translation = new Godot.Vector2(4, 5),
                Rotation = 0.25f,
                Scale = new Godot.Vector2(2, 3),
            });

        Assert.That(clip.Channels, Has.Count.EqualTo(5));
        Assert.That(clip.Channels.TrueForAll(channel =>
            channel.Keyframes.Count == 1 &&
            channel.Keyframes[0].Time == 0.5f), Is.True);
    }

    private static XafPackage Package()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
        });
        package.Clips.Add(new XafClip
        {
            Id = "idle",
            Duration = 2f,
        });
        return package;
    }
}
