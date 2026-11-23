using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigKeyRemovalTests
{
    [Test]
    public void RemovesMatchingKeyAndDropsEmptyChannel()
    {
        var clip = new RigClip { Id = "idle", Duration = 2f };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "root",
            Property = RigChannelProperty.Rotation
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 1f, Value = 2f });
        clip.Channels.Add(channel);

        Assert.That(
            RigAuthoring.RemoveKey(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1f),
            Is.True);
        Assert.That(clip.Channels, Is.Empty);
    }

    [Test]
    public void DoesNotRemoveNonMatchingKey()
    {
        var clip = new RigClip { Id = "idle", Duration = 2f };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "root",
            Property = RigChannelProperty.Rotation
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 1f, Value = 2f });
        clip.Channels.Add(channel);

        Assert.That(
            RigAuthoring.RemoveKey(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1.5f),
            Is.False);
        Assert.That(channel.Keyframes, Has.Count.EqualTo(1));
    }
}
