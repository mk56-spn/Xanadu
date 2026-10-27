using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class AnimatorRuntimeTests
{
    [Test]
    public void PlaysAndCrossfadesBetweenClips()
    {
        var runtime = new AnimatorRuntime(Package());

        Assert.That(runtime.Play("idle"), Is.True);
        Assert.That(runtime.CurrentClip, Is.EqualTo("idle"));
        runtime.Update(0.5f);
        Assert.That(runtime.Pose.Bones[0].Translation.X, Is.EqualTo(5f));

        Assert.That(runtime.Play("jump", 1f), Is.True);
        Assert.That(runtime.IsCrossfading, Is.True);
        runtime.Update(0.5f);
        Assert.That(runtime.Pose.Bones[0].Translation.X,
            Is.EqualTo(15f).Within(0.01f));

        runtime.Update(0.6f);
        Assert.That(runtime.IsCrossfading, Is.False);
        Assert.That(runtime.CurrentClip, Is.EqualTo("jump"));
    }

    [Test]
    public void PauseFreezesAnimationTime()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("idle");
        runtime.Update(0.25f);
        float x = runtime.Pose.Bones[0].Translation.X;

        runtime.SetPaused(true);
        runtime.Update(2f);

        Assert.That(runtime.Pose.Bones[0].Translation.X, Is.EqualTo(x));
    }

    [Test]
    public void AdditiveLayerAddsToBasePose()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("idle");
        runtime.AddLayer("bob", 1f);

        runtime.Update(0.5f);

        Assert.That(runtime.Pose.Bones[0].Translation.X,
            Is.EqualTo(8f).Within(0.01f));
        Assert.That(runtime.Additives, Has.Count.EqualTo(1));

        runtime.RemoveLayer("bob");
        runtime.Update(0.01f);
        Assert.That(runtime.Pose.Bones[0].Translation.X,
            Is.EqualTo(5.1f).Within(0.01f));
    }

    [Test]
    public void NonLoopingClipStopsAtItsDuration()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("jump");

        runtime.Update(1f);
        runtime.Update(1f);

        Assert.That(runtime.CurrentTime,
            Is.EqualTo(runtime.CurrentDuration));
    }

    [Test]
    public void LoopingClipWrapsItsRuntimeTime()
    {
        var runtime = new AnimatorRuntime(Package());
        XafClip clip = runtime.Package.Clips[2];
        clip.Loop = true;
        clip.LoopStart = 0f;
        clip.LoopEnd = 1f;
        runtime.Play("bob");

        runtime.Update(1.5f);

        Assert.That(runtime.CurrentTime, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void RestartReturnsAClipToItsFirstFrame()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("jump");
        runtime.Update(2f);
        Assert.That(runtime.CurrentTime, Is.EqualTo(runtime.CurrentDuration));

        runtime.Restart();
        runtime.SetPaused(false);
        runtime.Update(0.5f);

        Assert.That(runtime.CurrentTime, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void UnknownClipDoesNotChangeState()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("idle");

        Assert.That(runtime.Play("nope"), Is.False);
        Assert.That(runtime.CurrentClip, Is.EqualTo("idle"));
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
        package.Clips.Add(Clip("idle", 0f, 10f));
        package.Clips.Add(Clip("jump", 10f, 30f));
        package.Clips.Add(Clip("bob", 0f, 6f));
        return package;
    }

    private static XafClip Clip(string id, float start, float end)
    {
        var clip = new XafClip
        {
            Id = id,
            Duration = 2f,
        };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.TranslationX,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = start });
        channel.Keyframes.Add(new XafKeyframe { Time = 1f, Value = end });
        channel.Keyframes.Add(new XafKeyframe { Time = 2f, Value = end });
        clip.Channels.Add(channel);
        return clip;
    }
}
