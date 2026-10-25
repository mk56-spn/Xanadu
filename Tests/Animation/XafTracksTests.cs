using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafTracksTests
{
    private static XafPackage Rig()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "arm", new Vector2(10, 0));
        XafRigAuthoring.AddBone(package, 0, "leg", new Vector2(-10, 0));
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://a.svg",
            Group = "sword",
            BoneId = "arm",
        });
        return package;
    }

    private static void Key(XafPackage package, XafClip clip, string bone, float time) =>
        XafAuthoring.KeyBoneTransform(package, clip, package.Skeleton.Bones.FindIndex(b => b.Id == bone),
            time, XafTransform.Identity);

    [Test]
    public void TracksListAnimatedBonesInSkeletonOrderThenAttachments()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "leg", 0.5f);
        Key(package, clip, "arm", 0.25f);
        XafAuthoring.KeyAttachmentTransform(package, clip, 0, 0.5f, XafTransform.Identity);

        var tracks = XafTracks.Of(package, clip);

        Assert.That(tracks, Is.EqualTo(new[]
        {
            new XafTrack(XafChannelTarget.Bone, "arm"),
            new XafTrack(XafChannelTarget.Bone, "leg"),
            new XafTrack(XafChannelTarget.Attachment, "sword"),
        }));
    }

    [Test]
    public void UnanimatedTargetsAreLeftOutUnlessAskedFor()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);

        Assert.That(XafTracks.Of(package, clip), Has.Count.EqualTo(1));

        var withLeg = XafTracks.Of(package, clip, new XafTrack(XafChannelTarget.Bone, "leg"));
        Assert.That(withLeg.ConvertAll(track => track.Id), Is.EqualTo(new[] { "arm", "leg" }));
    }

    [Test]
    public void ChannelsOfMissingTargetsAreIgnored()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "ghost",
            XafChannelProperty.Rotation, 0.5f, 1f);

        Assert.That(XafTracks.Of(package, clip), Is.Empty);
    }

    [Test]
    public void KeyTimesAreTheDistinctSortedTimesAcrossChannels()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 1f);
        Key(package, clip, "arm", 0.25f);
        XafAuthoring.SetBoneProperty(package, clip, "arm",
            XafChannelProperty.Rotation, 0.5f, 3f);

        var times = XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "arm"));

        Assert.That(times, Is.EqualTo(new[] { 0.25f, 0.5f, 1f }));
    }

    [Test]
    public void MovingAKeySetMovesEveryChannelAtThatTime()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "arm", 1f);

        bool moved = XafAuthoring.MoveKeysAt(clip, XafChannelTarget.Bone, "arm", 0.5f, 0.75f);

        Assert.That(moved, Is.True);
        Assert.That(XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "arm")),
            Is.EqualTo(new[] { 0.75f, 1f }));
        foreach (XafChannel channel in clip.Channels)
            Assert.That(channel.Keyframes, Has.Count.EqualTo(2));
    }

    [Test]
    public void MovingOntoAnExistingKeyReplacesIt()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        XafAuthoring.SetBoneProperty(package, clip, "arm",
            XafChannelProperty.Rotation, 1f, 9f);

        XafAuthoring.MoveKeysAt(clip, XafChannelTarget.Bone, "arm", 0.5f, 1f);

        XafChannel rotation = clip.Channels.Find(c => c.Property == XafChannelProperty.Rotation)!;
        Assert.That(rotation.Keyframes, Has.Count.EqualTo(1));
        Assert.That(rotation.Keyframes[0].Value, Is.EqualTo(0f));
    }

    [Test]
    public void MovingClampsToTheClipAndLeavesOtherTargetsAlone()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "leg", 0.5f);

        XafAuthoring.MoveKeysAt(clip, XafChannelTarget.Bone, "arm", 0.5f, 99f);

        Assert.That(XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "arm")),
            Is.EqualTo(new[] { clip.Duration }));
        Assert.That(XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "leg")),
            Is.EqualTo(new[] { 0.5f }));
    }

    [Test]
    public void MovingWhereThereIsNoKeyDoesNothing()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);

        Assert.That(XafAuthoring.MoveKeysAt(clip, XafChannelTarget.Bone, "arm", 0.3f, 0.6f), Is.False);
        Assert.That(XafAuthoring.MoveKeysAt(clip, XafChannelTarget.Bone, "leg", 0.5f, 0.6f), Is.False);
    }

    [Test]
    public void RemovingAKeySetDropsEmptyChannelsAndKeepsOtherTimes()
    {
        XafPackage package = Rig();
        XafClip clip = package.Clips[0];
        Key(package, clip, "arm", 0.5f);
        Key(package, clip, "arm", 1f);

        Assert.That(XafAuthoring.RemoveKeysAt(clip, XafChannelTarget.Bone, "arm", 0.5f), Is.True);
        Assert.That(XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "arm")),
            Is.EqualTo(new[] { 1f }));

        XafAuthoring.RemoveKeysAt(clip, XafChannelTarget.Bone, "arm", 1f);
        Assert.That(clip.Channels, Is.Empty);
        Assert.That(XafAuthoring.RemoveKeysAt(clip, XafChannelTarget.Bone, "arm", 1f), Is.False);
    }
}
