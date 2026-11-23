using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigKeyMoveTests
{
    [Test]
    public void MovesAKeyAndKeepsItSorted()
    {
        var clip = new RigClip { Id = "idle", Duration = 2f };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "root",
            Property = RigChannelProperty.Rotation
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 0f, Value = 1f });
        channel.Keyframes.Add(new RigKeyframe { Time = 1f, Value = 2f });
        channel.Keyframes.Add(new RigKeyframe { Time = 2f, Value = 3f });
        clip.Channels.Add(channel);

        Assert.That(
            RigAuthoring.MoveKey(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1f, 0.5f),
            Is.True);

        Assert.That(channel.Keyframes[1].Time, Is.EqualTo(0.5f));
        Assert.That(channel.Keyframes[1].Value, Is.EqualTo(2f));
    }

    [Test]
    public void ClampsMovedKeysToClipDuration()
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
            RigAuthoring.MoveKey(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1f, 9f),
            Is.True);
        Assert.That(channel.Keyframes[0].Time, Is.EqualTo(2f));
    }
}
