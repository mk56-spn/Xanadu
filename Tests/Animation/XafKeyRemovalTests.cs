using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafKeyRemovalTests
{
    [Test]
    public void RemovesMatchingKeyAndDropsEmptyChannel()
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

        Assert.That(XafAuthoring.RemoveKey(
            clip,
            XafChannelTarget.Bone,
            "root",
            XafChannelProperty.Rotation,
            1f), Is.True);
        Assert.That(clip.Channels, Is.Empty);
    }

    [Test]
    public void DoesNotRemoveNonMatchingKey()
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

        Assert.That(XafAuthoring.RemoveKey(
            clip,
            XafChannelTarget.Bone,
            "root",
            XafChannelProperty.Rotation,
            1.5f), Is.False);
        Assert.That(channel.Keyframes, Has.Count.EqualTo(1));
    }
}
