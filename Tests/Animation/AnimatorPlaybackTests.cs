using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class AnimatorPlaybackTests
{
    [Test]
    public void PlayAdvancesAndStopFreezes()
    {
        var playback = new AnimatorPlayback(new AnimatorRuntime(Package()));
        playback.Runtime.Play("idle");

        playback.Play();
        playback.Update(0.5f);
        Assert.That(playback.Runtime.CurrentTime,
            Is.EqualTo(0.5f).Within(0.001f));

        playback.Stop();
        playback.Update(1f);
        Assert.That(playback.Runtime.CurrentTime,
            Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void PlayRestartsAClipThatReachedItsEnd()
    {
        var playback = new AnimatorPlayback(new AnimatorRuntime(Package()));
        playback.Runtime.Play("idle");
        playback.Update(2f);
        Assert.That(playback.Runtime.CurrentTime,
            Is.EqualTo(2f).Within(0.001f));

        playback.Play();
        playback.Update(0.5f);

        Assert.That(playback.Runtime.CurrentTime,
            Is.EqualTo(0.5f).Within(0.001f));
    }

    private static XafPackage Package()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
        });
        var clip = new XafClip { Id = "idle", Duration = 2f };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.TranslationX,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        channel.Keyframes.Add(new XafKeyframe { Time = 2f, Value = 10f });
        clip.Channels.Add(channel);
        package.Clips.Add(clip);
        return package;
    }
}
