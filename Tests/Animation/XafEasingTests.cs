using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafEasingTests
{
    private static (XafClip Clip, XafChannel Channel) TwoKeys(float from = 0f, float to = 10f)
    {
        var clip = new XafClip { Id = "c", Duration = 1f };
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "b", XafChannelProperty.Rotation, 0f, from);
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "b", XafChannelProperty.Rotation, 1f, to);
        return (clip, clip.Channels[0]);
    }

    private static float At(XafChannel channel, float time) => XafSampler.SampleChannel(channel, time);

    private static void Apply(XafClip clip, XafEasePreset preset) =>
        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, preset), Is.True);

    [Test]
    public void StepHoldsTheValueUntilTheNextKey()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        Apply(clip, XafEasePreset.Step);

        Assert.That(At(channel, 0.99f), Is.EqualTo(0f));
        Assert.That(At(channel, 1f), Is.EqualTo(10f));
    }

    [Test]
    public void LinearIsAStraightLine()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        Apply(clip, XafEasePreset.Step);
        Apply(clip, XafEasePreset.Linear);

        Assert.That(At(channel, 0.25f), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(channel.Keyframes[0].TangentOut, Is.EqualTo(0f));
    }

    [Test]
    public void EaseIsSlowAtBothEndsAndMeetsLinearInTheMiddle()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        Apply(clip, XafEasePreset.Ease);

        Assert.That(At(channel, 0.25f), Is.LessThan(2.5f));
        Assert.That(At(channel, 0.5f), Is.EqualTo(5f).Within(0.001f));
        Assert.That(At(channel, 0.75f), Is.GreaterThan(7.5f));
    }

    [Test]
    public void EaseInStartsSlowAndEndsAtFullSpeed()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        Apply(clip, XafEasePreset.EaseIn);

        Assert.That(At(channel, 0.5f), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(At(channel, 1f), Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void EaseOutStartsAtFullSpeedAndEndsSlow()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        Apply(clip, XafEasePreset.EaseOut);

        Assert.That(At(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void DescendingValuesEaseCorrectlyToo()
    {
        (XafClip clip, XafChannel channel) = TwoKeys(10f, 0f);
        Apply(clip, XafEasePreset.EaseIn);

        Assert.That(At(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void ReadReturnsWhatWasApplied()
    {
        (XafClip clip, _) = TwoKeys();

        foreach (XafEasePreset preset in System.Enum.GetValues<XafEasePreset>())
        {
            Apply(clip, preset);
            Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0f), Is.EqualTo(preset));
        }
    }

    [Test]
    public void ApplyingToAKeySetCoversEveryChannelAtThatTime()
    {
        var package = XafRigAuthoring.CreateEmptyPackage();
        XafClip clip = package.Clips[0];
        XafAuthoring.KeyBoneTransform(package, clip, 0, 0f, XafTransform.Identity);
        XafAuthoring.KeyBoneTransform(package, clip, 0, 1f, XafTransform.Identity);

        XafEasing.Apply(clip, XafChannelTarget.Bone, "root", 0f, XafEasePreset.Ease);

        foreach (XafChannel channel in clip.Channels)
        {
            Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
            Assert.That(channel.Keyframes[1].Interpolation, Is.EqualTo(XafInterpolation.Linear));
        }
    }

    [Test]
    public void TheLastKeyAcceptsAModeButHasNoSegmentToShape()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();

        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 1f, XafEasePreset.EaseIn), Is.True);
        Assert.That(channel.Keyframes[1].Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
    }

    [Test]
    public void MissingKeysAndTargetsAreReportedNotInvented()
    {
        (XafClip clip, _) = TwoKeys();

        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0.5f, XafEasePreset.Ease), Is.False);
        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "x", 0f, XafEasePreset.Ease), Is.False);
        Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0.5f), Is.Null);
    }

    [Test]
    public void PresetsSurviveASaveAndLoad()
    {
        var package = XafRigAuthoring.CreateEmptyPackage();
        XafClip clip = package.Clips[0];
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "root", XafChannelProperty.Rotation, 0f, 0f);
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "root", XafChannelProperty.Rotation, 1f, 4f);
        XafEasing.Apply(clip, XafChannelTarget.Bone, "root", 0f, XafEasePreset.EaseOut);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(XafEasing.Read(loaded.Clips[0], XafChannelTarget.Bone, "root", 0f),
            Is.EqualTo(XafEasePreset.EaseOut));
    }
}
