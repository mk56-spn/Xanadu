using System;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Timeline;

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
        foreach (CurveFamily family in Enum.GetValues<CurveFamily>())
        {
            foreach (CurveDirection direction in Enum.GetValues<CurveDirection>())
            {
                Assert.That(EaseCurves.Evaluate(family, direction, 0f), Is.EqualTo(0f).Within(0.002f), $"{family}{direction}");
                Assert.That(EaseCurves.Evaluate(family, direction, 1f), Is.EqualTo(1f).Within(0.002f), $"{family}{direction}");
            }
        }
    }

    [Test]
    public void InOutCurvesPassTheMiddleAtTheMiddle()
    {
        foreach (CurveFamily family in Enum.GetValues<CurveFamily>())
            Assert.That(EaseCurves.Evaluate(family, CurveDirection.InOut, 0.5f), Is.EqualTo(0.5f).Within(0.001f), $"{family}");
    }

    [Test]
    public void BackOutOvershootsAndElasticInDipsBelowTheStart()
    {
        float most = 0f;
        float least = 0f;
        for (int i = 0; i <= 100; i++)
        {
            most = Math.Max(most, EaseCurves.Evaluate(CurveFamily.Back, CurveDirection.Out, i / 100f));
            least = Math.Min(least, EaseCurves.Evaluate(CurveFamily.Elastic, CurveDirection.In, i / 100f));
        }

        Assert.That(most, Is.GreaterThan(1.05f));
        Assert.That(least, Is.LessThan(-0.2f));
    }

    [Test]
    public void EveryPresetHasAFamilyAndDirectionOrIsOneOfTheBasics()
    {
        foreach (EasePreset preset in Enum.GetValues<EasePreset>())
        {
            bool basic = preset is EasePreset.Step or EasePreset.Linear or EasePreset.Ease;
            Assert.That(EasePresets.Shape(preset) is null, Is.EqualTo(basic), preset.ToString());
            if (EasePresets.Shape(preset) is var (family, direction))
                Assert.That(EasePresets.Preset(family, direction), Is.EqualTo(preset));
        }
    }

    [Test]
    public void ANamedCurveIsSampledAndReadBack()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();

        Assert.That(XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, EasePreset.BounceOut), Is.True);

        Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.BounceOut));
        Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0f), Is.EqualTo(EasePreset.BounceOut));
        Assert.That(XafSampler.SampleChannel(channel, 0.3f),
            Is.EqualTo(10f * EaseCurves.Evaluate(CurveFamily.Bounce, CurveDirection.Out, 0.3f)).Within(0.001f));
    }

    [Test]
    public void QuadInAndOutStayTheTangentEasesTheyAlwaysWere()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();

        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, EasePresets.Preset(CurveFamily.Quad, CurveDirection.Out));

        Assert.That(channel.Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
        Assert.That(XafSampler.SampleChannel(channel, 0.5f), Is.EqualTo(7.5f).Within(0.001f));
    }

    [Test]
    public void SwitchingFromATangentEaseToANamedCurveClearsTheTangents()
    {
        (XafClip clip, XafChannel channel) = TwoKeys();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, EasePreset.EaseIn);

        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, EasePreset.SineOut);

        Assert.That(channel.Keyframes[1].TangentIn, Is.EqualTo(0f));
        Assert.That(XafEasing.Read(clip, XafChannelTarget.Bone, "b", 0f), Is.EqualTo(EasePreset.SineOut));
    }

    [Test]
    public void NamedCurvesRoundTripTheFile()
    {
        (XafClip clip, _) = TwoKeys();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "b", 0f, EasePreset.ElasticInOut);

        XafPackage loaded = XafBinary.Read(XafBinary.Write(Package(clip)));

        Assert.That(loaded.Clips[0].Channels[0].Keyframes[0].Interpolation, Is.EqualTo(XafInterpolation.ElasticInOut));
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
