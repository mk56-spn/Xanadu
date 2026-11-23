using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigTracksTests
{
    private static RigPackage Rig()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "arm", new Vector2(10, 0));
        RigRigAuthoring.AddBone(package, 0, "leg", new Vector2(-10, 0));
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "a.svg",
                Group = "sword",
                BoneId = "arm"
            });
        return package;
    }

    private static void Key(RigPackage package, RigClip clip, string bone, float time) =>
        RigAuthoring.KeyBoneTransform(
            package,
            clip,
            package.Skeleton.Bones.FindIndex(b => b.Id == bone),
            time,
            RigTransform.Identity);

    [Test]
    public void TracksListAnimatedBonesInSkeletonOrderThenAttachments()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "leg", 0.5f);
        Key(package, clip, "arm", 0.25f);
        RigAuthoring.KeyAttachmentTransform(package, clip, 0, 0.5f, RigTransform.Identity);

        List<RigTrack> tracks = RigTracks.Of(package, clip);

        Assert.That(
            tracks,
            Is.EqualTo(
                new[]
                {
                    new RigTrack(RigChannelTarget.Bone, "arm"),
                    new RigTrack(RigChannelTarget.Bone, "leg"),
                    new RigTrack(RigChannelTarget.Attachment, "sword")
                }));
    }

    [Test]
    public void UnanimatedTargetsAreLeftOutUnlessAskedFor()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);

        Assert.That(RigTracks.Of(package, clip), Has.Count.EqualTo(1));

        List<RigTrack> withLeg = RigTracks.Of(package, clip, new RigTrack(RigChannelTarget.Bone, "leg"));
        Assert.That(withLeg.ConvertAll(track => track.Id), Is.EqualTo(new[] { "arm", "leg" }));
    }

    [Test]
    public void ChannelsOfMissingTargetsAreIgnored()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "ghost", RigChannelProperty.Rotation, 0.5f, 1f);

        Assert.That(RigTracks.Of(package, clip), Is.Empty);
    }

    [Test]
    public void KeyTimesAreTheDistinctSortedTimesAcrossChannels()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 1f);
        Key(package, clip, "arm", 0.25f);
        RigAuthoring.SetBoneProperty(package, clip, "arm", RigChannelProperty.Rotation, 0.5f, 3f);

        List<float> times = RigTracks.KeyTimes(clip, new RigTrack(RigChannelTarget.Bone, "arm"));

        Assert.That(times, Is.EqualTo(new[] { 0.25f, 0.5f, 1f }));
    }

    [Test]
    public void MovingAKeySetMovesEveryChannelAtThatTime()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "arm", 1f);

        bool moved = RigAuthoring.MoveKeysAt(clip, RigChannelTarget.Bone, "arm", 0.5f, 0.75f);

        Assert.That(moved, Is.True);
        Assert.That(
            RigTracks.KeyTimes(clip, new RigTrack(RigChannelTarget.Bone, "arm")),
            Is.EqualTo(new[] { 0.75f, 1f }));
        foreach (RigChannel channel in clip.Channels)
            Assert.That(channel.Keyframes, Has.Count.EqualTo(2));
    }

    [Test]
    public void MovingOntoAnExistingKeyReplacesIt()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        RigAuthoring.SetBoneProperty(package, clip, "arm", RigChannelProperty.Rotation, 1f, 9f);

        RigAuthoring.MoveKeysAt(clip, RigChannelTarget.Bone, "arm", 0.5f, 1f);

        RigChannel rotation = clip.Channels.Find(c => c.Property == RigChannelProperty.Rotation)!;
        Assert.That(rotation.Keyframes, Has.Count.EqualTo(1));
        Assert.That(rotation.Keyframes[0].Value, Is.EqualTo(0f));
    }

    [Test]
    public void MovingClampsToTheClipAndLeavesOtherTargetsAlone()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "leg", 0.5f);

        RigAuthoring.MoveKeysAt(clip, RigChannelTarget.Bone, "arm", 0.5f, 99f);

        Assert.That(
            RigTracks.KeyTimes(clip, new RigTrack(RigChannelTarget.Bone, "arm")),
            Is.EqualTo(new[] { clip.Duration }));
        Assert.That(RigTracks.KeyTimes(clip, new RigTrack(RigChannelTarget.Bone, "leg")), Is.EqualTo(new[] { 0.5f }));
    }

    [Test]
    public void MovingWhereThereIsNoKeyDoesNothing()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);

        Assert.That(RigAuthoring.MoveKeysAt(clip, RigChannelTarget.Bone, "arm", 0.3f, 0.6f), Is.False);
        Assert.That(RigAuthoring.MoveKeysAt(clip, RigChannelTarget.Bone, "leg", 0.5f, 0.6f), Is.False);
    }

    [Test]
    public void RemovingAKeySetDropsEmptyChannelsAndKeepsOtherTimes()
    {
        RigPackage package = Rig();
        RigClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "arm", 1f);

        Assert.That(RigAuthoring.RemoveKeysAt(clip, RigChannelTarget.Bone, "arm", 0.5f), Is.True);
        Assert.That(RigTracks.KeyTimes(clip, new RigTrack(RigChannelTarget.Bone, "arm")), Is.EqualTo(new[] { 1f }));

        RigAuthoring.RemoveKeysAt(clip, RigChannelTarget.Bone, "arm", 1f);
        Assert.That(clip.Channels, Is.Empty);
        Assert.That(RigAuthoring.RemoveKeysAt(clip, RigChannelTarget.Bone, "arm", 1f), Is.False);
    }
}
