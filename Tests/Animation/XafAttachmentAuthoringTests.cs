using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafAttachmentAuthoringTests
{
    [Test]
    public void KeysAllAttachmentTransformChannels()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];

        XafAuthoring.KeyAttachmentTransform(
            package,
            clip,
            0,
            0.5f,
            new XafTransform
            {
                Translation = new Vector2(4, 5),
                Rotation = 0.25f,
                Scale = new Vector2(2, 3),
            });

        Assert.That(clip.Channels, Has.Count.EqualTo(5));
        Assert.That(clip.Channels.TrueForAll(channel =>
            channel.Target == XafChannelTarget.Attachment &&
            channel.TargetId == "body" &&
            channel.Keyframes.Count == 1 &&
            channel.Keyframes[0].Time == 0.5f), Is.True);
    }

    [Test]
    public void IgnoreInvalidAttachmentIndex()
    {
        XafPackage package = Package();

        XafAuthoring.KeyAttachmentTransform(
            package,
            package.Clips[0],
            99,
            0f,
            XafTransform.Identity);

        Assert.That(package.Clips[0].Channels, Is.Empty);
    }

    private static XafPackage Package()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://body.svg",
            Group = "body",
            BoneId = "root",
        });
        package.Clips.Add(new XafClip
        {
            Id = "idle",
            Duration = 2f,
        });
        return package;
    }
}
