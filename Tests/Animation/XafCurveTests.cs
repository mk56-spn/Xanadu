using System;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafCurveTests
{
    private static (XafClip Clip, XafChannel Channel) TwoKeys()
    {
        var clip = new XafClip { Id = "c", Duration = 1f };
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "b", XafChannelProperty.Rotation, 0f, 0f);
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "b", XafChannelProperty.Rotation, 1f, 10f);
        return (clip, clip.Channels[0]);
    }

    private static XafPackage Package(XafClip clip)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "b" });
        package.Clips.Add(clip);
        return package;
    }

    [Test]
    public void EveryCurveStartsAtZeroAndEndsAtOne()
    {
        foreach (XafCurveFamily family in Enum.GetValues<XafCurveFamily>())
        {
            foreach (XafCurveDirection direction in Enum.GetValues<XafCurveDirection>())
            {
                Assert.That(XafCurves.Evaluate(family, direction, 0f), Is.EqualTo(0f).Within(0.002f), $"{family}{direction}");
                Assert.That(XafCurves.Evaluate(family, direction, 1f), Is.EqualTo(1f).Within(0.002f), $"{family}{direction}");
            }
        }
    }

    [Test]
    public void InOutCurvesPassTheMiddleAtTheMiddle()
    {
        foreach (XafCurveFamily family in Enum.GetValues<XafCurveFamily>())
            Assert.That(XafCurves.Evaluate(family, XafCurveDirection.InOut, 0.5f), Is.EqualTo(0.5f).Within(0.001f), $"{family}");
    }

    [Test]
    public void BackOutOvershootsAndElasticInDipsBelowTheStart()
    {
        float most = 0f;
        float least = 0f;
        for (int i = 0; i <= 100; i++)
        {
            most = Math.Max(most, XafCurves.Evaluate(XafCurveFamily.Back, XafCurveDirection.Out, i / 100f));
            least = Math.Min(least, XafCurves.Evaluate(XafCurveFamily.Elastic, XafCurveDirection.In, i / 100f));
        }

        Assert.That(most, Is.GreaterThan(1.05f));
        Assert.That(least, Is.LessThan(-0.2f));
    }

    [Test]
    public void EveryPresetHasAFamilyAndDirectionOrIsOneOfTheBasics()
    {
        foreach (XafEasePreset preset in Enum.GetValues<XafEasePreset>())
        {
            bool basic = preset is XafEasePreset.Step or XafEasePreset.Linear or XafEasePreset.Ease;
            Assert.That(XafEasing.Shape(preset) is null, Is.EqualTo(basic), preset.ToString());
            if (XafEasing.Shape(preset) is var (family, direction))
                Assert.That(XafEasing.Preset(family, direction), Is.EqualTo(preset));
        }
    }

    [Test]
    public void ANamedCurveIsSampledAndReadBack()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();

        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasePreset.BounceOut), Is.True);

        Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.BounceOut));
        Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0f), Is.EqualTo(XafEasePreset.BounceOut));
        Assert.That(XafSampler.SampleChannel(channel, 0.3f),
            Is.EqualTo(10f * XafCurves.Evaluate(XafCurveFamily.Bounce, XafCurveDirection.Out, 0.3f)).Within(0.001f));
    }

    [Test]
    public void QuadInAndOutStayTheTangentEasesTheyAlwaysWere()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();

        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasing.Preset(XafCurveFamily.Quad, XafCurveDirection.Out));

        Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
        Assert.That(XafSampler.SampleChannel(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void SwitchingFromATangentEaseToANamedCurveClearsTheTangents()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasePreset.EaseIn);

        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasePreset.SineOut);

        Assert.That(channel.Keyframes[1].TangentIn, Is.EqualTo(0f));
        Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0f), Is.EqualTo(XafEasePreset.SineOut));
    }

    [Test]
    public void NamedCurvesRoundTripTheFile()
    {
        (XafClip clip, _) = TwoKeys();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasePreset.ElasticInOut);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(Package(clip)));

        Assert.That(loaded.Clips[0].Channels[0].Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.ElasticInOut));
    }

    [Test]
    public void AnOlderRevisionGetsThePlainEaseInPlaceOfANamedCurve()
    {
        (XafClip clip, _) = TwoKeys();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, XafEasePreset.ExpoIn);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(Package(clip), 11));

        Assert.That(loaded.Clips[0].Channels[0].Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
        Assert.That(XafEasing.Read(loaded.Clips[0], XafChannelTarget.Bone, "b", 0f), Is.EqualTo(XafEasePreset.Ease));
    }

    [Test]
    public void AnUnknownInterpolationIsRefused()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        channel.Keyframes[0].Interpolation = (XafInterpolation)250;

        byte[] data = XafBinary.Write(Package(clip));

        Assert.Throws<XafFormatException>(() => XafBinary.Read(data));
    }
}
