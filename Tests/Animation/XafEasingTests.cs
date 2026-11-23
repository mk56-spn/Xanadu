using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Timeline;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigEasingTests
{
    private static (RigClip Clip, RigChannel Channel) TwoKeys(float from = 0f, float to = 10f)
    {
        var clip = new RigClip { Id = "c", Duration = 1f };
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "b", RigChannelProperty.Rotation, 0f, from);
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "b", RigChannelProperty.Rotation, 1f, to);
        return (clip, clip.Channels[0]);
    }

    private static float At(RigChannel channel, float time) => RigSampler.SampleChannel(channel, time);

    private static void Apply(RigClip clip, EasePreset preset) =>
        Assert.That(RigEasing.Apply(clip, RigChannelTarget.Bone, "b", 0f, preset), Is.True);

    [Test]
    public void StepHoldsTheValueUntilTheNextKey()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();
        Apply(clip, EasePreset.Step);

        Assert.That(At(channel, 0.99f), Is.EqualTo(0f));
        Assert.That(At(channel, 1f), Is.EqualTo(10f));
    }

    [Test]
    public void LinearIsAStraightLine()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();
        Apply(clip, EasePreset.Step);
        Apply(clip, EasePreset.Linear);

        Assert.That(At(channel, 0.25f), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(channel.Keyframes[0].TangentOut, Is.EqualTo(0f));
    }

    [Test]
    public void EaseIsSlowAtBothEndsAndMeetsLinearInTheMiddle()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();
        Apply(clip, EasePreset.Ease);

        Assert.That(At(channel, 0.25f), Is.LessThan(2.5f));
        Assert.That(At(channel, 0.5f), Is.EqualTo(5f).Within(0.001f));
        Assert.That(At(channel, 0.75f), Is.GreaterThan(7.5f));
    }

    [Test]
    public void EaseInStartsSlowAndEndsAtFullSpeed()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();
        Apply(clip, EasePreset.EaseIn);

        Assert.That(At(channel, 0.5f), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(At(channel, 1f), Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void EaseOutStartsAtFullSpeedAndEndsSlow()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();
        Apply(clip, EasePreset.EaseOut);

        Assert.That(At(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void DescendingValuesEaseCorrectlyToo()
    {
        (RigClip clip, RigChannel channel) = TwoKeys(10f, 0f);
        Apply(clip, EasePreset.EaseIn);

        Assert.That(At(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void ReadReturnsWhatWasApplied()
    {
        (RigClip clip, _) = TwoKeys();

        foreach (EasePreset preset in System.Enum.GetValues<EasePreset>())
        {
            Apply(clip, preset);
            Assert.That(RigEasing.Read(clip, RigChannelTarget.Bone, "b", 0f), Is.EqualTo(preset));
        }
    }

    [Test]
    public void ApplyingToAKeySetCoversEveryChannelAtThatTime()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigClip clip = package.Clips[0];
        RigAuthoring.KeyBoneTransform(package, clip, 0, 0f, RigTransform.Identity);
        RigAuthoring.KeyBoneTransform(package, clip, 0, 1f, RigTransform.Identity);

        RigEasing.Apply(clip, RigChannelTarget.Bone, "root", 0f, EasePreset.Ease);

        foreach (RigChannel channel in clip.Channels)
        {
            Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(RigInterpolation.CubicBezier));
            Assert.That(channel.Keyframes[1].Interpolation, Is.EqualTo(RigInterpolation.Linear));
        }
    }

    [Test]
    public void TheLastKeyAcceptsAModeButHasNoSegmentToShape()
    {
        (RigClip clip, RigChannel channel) = TwoKeys();

        Assert.That(RigEasing.Apply(clip, RigChannelTarget.Bone, "b", 1f, EasePreset.EaseIn), Is.True);
        Assert.That(channel.Keyframes[1].Interpolation, Is.EqualTo(RigInterpolation.CubicBezier));
    }

    [Test]
    public void MissingKeysAndTargetsAreReportedNotInvented()
    {
        (RigClip clip, _) = TwoKeys();

        Assert.That(RigEasing.Apply(clip, RigChannelTarget.Bone, "b", 0.5f, EasePreset.Ease), Is.False);
        Assert.That(RigEasing.Apply(clip, RigChannelTarget.Bone, "x", 0f, EasePreset.Ease), Is.False);
        Assert.That(RigEasing.Read(clip, RigChannelTarget.Bone, "b", 0.5f), Is.Null);
    }

    [Test]
    public void PresetsSurviveASaveAndLoad()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigClip clip = package.Clips[0];
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 0f, 0f);
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1f, 4f);
        RigEasing.Apply(clip, RigChannelTarget.Bone, "root", 0f, EasePreset.EaseOut);

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));

        Assert.That(RigEasing.Read(loaded.Clips[0], RigChannelTarget.Bone, "root", 0f), Is.EqualTo(EasePreset.EaseOut));
    }
}
