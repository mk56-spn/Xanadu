using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Inertial hand-overs: the new clip plays at once and the gap from what was shown settles away.</summary>
[TestFixture]
public sealed class AnimatorInertialTests
{
    private const float Frame = 1f / 60f;

    [Test]
    public void AHandOverStartsFromWhatWasShown()
    {
        AnimatorRuntime runtime = Runtime();
        runtime.Update(Frame);
        float shown = runtime.Pose.Bones[0].Rotation;

        runtime.Transition("b");

        Assert.That(runtime.CurrentClip, Is.EqualTo("b"));
        Assert.That(runtime.IsCrossfading, Is.False);
        Assert.That(runtime.Pose.Bones[0].Rotation, Is.EqualTo(shown).Within(1e-4f));
    }

    [Test]
    public void TheGapSettlesSmoothlyOntoTheNewClip()
    {
        AnimatorRuntime runtime = Runtime();
        runtime.Update(Frame);
        runtime.Transition("b");

        float last = runtime.Pose.Bones[0].Rotation;
        float lastStep = 0f;
        for (float time = 0f; time < AnimatorRuntime.InertialSeconds; time += Frame)
        {
            runtime.Update(Frame);
            float now = runtime.Pose.Bones[0].Rotation;
            Assert.That(now, Is.GreaterThanOrEqualTo(last - 1e-4f), "heads one way, no overshoot from rest");
            lastStep = now - last;
            last = now;
        }

        Assert.That(runtime.IsSettling, Is.False);
        Assert.That(runtime.Pose.Bones[0].Rotation, Is.EqualTo(1f).Within(0.01f));
        Assert.That(lastStep, Is.LessThan(0.01f), "it eases in rather than arriving at speed");
    }

    [Test]
    public void AHardCutStillCuts()
    {
        AnimatorRuntime runtime = Runtime();
        runtime.Update(Frame);

        runtime.Play("b", 0f);

        Assert.That(runtime.Pose.Bones[0].Rotation, Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void ACrossfadingRuntimeIsUnchanged()
    {
        AnimatorRuntime runtime = Runtime(false);
        runtime.Update(Frame);

        runtime.Transition("b");

        Assert.That(runtime.IsCrossfading, Is.True);
        Assert.That(runtime.NextClip, Is.EqualTo("b"));
    }

    private static AnimatorRuntime Runtime(bool inertial = true)
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "root" });
        package.Clips.Add(Held("a", 0f));
        package.Clips.Add(Held("b", 1f));
        var runtime = new AnimatorRuntime(package) { Inertial = inertial };
        runtime.Play("a", 0f);
        return runtime;
    }

    /// <summary>A looping clip that holds the root turned by <paramref name="turn"/>.</summary>
    private static RigClip Held(string id, float turn)
    {
        var clip = new RigClip { Id = id, Duration = 1f, Loop = true, LoopStart = 0f, LoopEnd = 1f };
        var channel = new RigChannel
            { Target = RigChannelTarget.Bone, TargetId = "root", Property = RigChannelProperty.Rotation };
        channel.Keyframes.Add(new RigKeyframe { Time = 0f, Value = turn });
        channel.Keyframes.Add(new RigKeyframe { Time = 1f, Value = turn });
        clip.Channels.Add(channel);
        return clip;
    }
}
