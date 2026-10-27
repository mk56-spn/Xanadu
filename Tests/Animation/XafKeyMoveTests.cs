using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafKeyMoveTests
{
    [Test]
    public void MovesAKeyAndKeepsItSorted()
    {
        var clip = new XafClip { Id = "idle", Duration = 2f };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.Rotation,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 1f });
        channel.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 2f });
        channel.Keyframes.Add(new XafKeyframe { Time = 2f, Value = 3f });
        clip.Channels.Add(channel);

        Assert.That(XafAuthoring.MoveKey(
            clip,
            XafChannelTarget.Bone,
            "root",
            XafChannelProperty.Rotation,
            1f,
            0.5f), Is.True);

        Assert.That(channel.Keyframes[1].Time, Is.EqualTo(0.5f));
        Assert.That(channel.Keyframes[1].Value, Is.EqualTo(2f));
    }

    [Test]
    public void ClampsMovedKeysToClipDuration()
    {
        var clip = new XafClip { Id = "idle", Duration = 2f };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.Rotation,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 2f });
        clip.Channels.Add(channel);

        Assert.That(XafAuthoring.MoveKey(
            clip,
            XafChannelTarget.Bone,
            "root",
            XafChannelProperty.Rotation,
            1f,
            9f), Is.True);
        Assert.That(channel.Keyframes[0].Time, Is.EqualTo(2f));
    }
}
