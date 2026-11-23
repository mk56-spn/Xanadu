using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigAuthoringTests
{
    [Test]
    public void InsertsKeyframesInOrderAndReplacesMatchingTime()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];

        Assert.That(RigAuthoring.SetBoneProperty(package, clip, "root", RigChannelProperty.Rotation, 1f, 2f), Is.True);
        Assert.That(
            RigAuthoring.SetBoneProperty(package, clip, "root", RigChannelProperty.Rotation, 0.5f, 1f),
            Is.True);
        Assert.That(RigAuthoring.SetBoneProperty(package, clip, "root", RigChannelProperty.Rotation, 1f, 3f), Is.True);

        RigChannel channel = clip.Channels[0];
        Assert.That(channel.Keyframes, Has.Count.EqualTo(2));
        Assert.That(channel.Keyframes[0].Time, Is.EqualTo(0.5f));
        Assert.That(channel.Keyframes[1].Time, Is.EqualTo(1f));
        Assert.That(channel.Keyframes[1].Value, Is.EqualTo(3f));
    }

    [Test]
    public void RejectsMissingBone()
    {
        RigPackage package = Package();

        Assert.That(
            RigAuthoring.SetBoneProperty(package, package.Clips[0], "missing", RigChannelProperty.Rotation, 0f, 1f),
            Is.False);
    }

    [Test]
    public void KeysFullBoneTransform()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];

        RigAuthoring.KeyBoneTransform(
            package,
            clip,
            0,
            0.5f,
            new RigTransform
            {
                Translation = new Vector2(4, 5),
                Rotation = 0.25f,
                Scale = new Vector2(2, 3)
            });

        Assert.That(clip.Channels, Has.Count.EqualTo(5));
        Assert.That(
            clip.Channels.TrueForAll(channel => channel.Keyframes.Count == 1 && channel.Keyframes[0].Time == 0.5f),
            Is.True);
    }

    private static RigPackage Package()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "root",
                Parent = -1
            });
        package.Clips.Add(
            new RigClip
            {
                Id = "idle",
                Duration = 2f
            });
        return package;
    }
}
