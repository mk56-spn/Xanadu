using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafSamplingTests
{
    [Test]
    public void SamplesLinearStepAndBezierChannels()
    {
        XafChannel channel = Channel(
            XafInterpolation.Linear,
            (0f, 0f, XafInterpolation.Linear),
            (1f, 10f, XafInterpolation.Linear));
        Assert.That(XafSampler.SampleChannel(channel, 0.5f),
            Is.EqualTo(5f).Within(0.001f));

        channel.Keyframes[0].Interpolation = XafInterpolation.Step;
        Assert.That(XafSampler.SampleChannel(channel, 0.5f),
            Is.EqualTo(0f).Within(0.001f));

        channel.Keyframes[0].Interpolation = XafInterpolation.CubicBezier;
        channel.Keyframes[0].TangentOut = 0f;
        channel.Keyframes[1].TangentIn = 0f;
        Assert.That(XafSampler.SampleChannel(channel, 0.5f),
            Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void CrossfadeBlendsTranslationRotationAndScale()
    {
        var from = new XafPose(1, 0);
        from.Bones[0] = new XafTransform
        {
            Translation = new Vector2(0, 0),
            Rotation = 0,
            Scale = Vector2.One,
        };
        var to = new XafPose(1, 0);
        to.Bones[0] = new XafTransform
        {
            Translation = new Vector2(10, 20),
            Rotation = 1f,
            Scale = new Vector2(2, 3),
        };

        XafPose result = XafBlend.Crossfade(from, to, 0.5f);

        Assert.That(result.Bones[0].Translation, Is.EqualTo(new Vector2(5, 10)));
        Assert.That(result.Bones[0].Rotation, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(result.Bones[0].Scale, Is.EqualTo(new Vector2(1.5f, 2f)));
    }

    [Test]
    public void AdditiveLayerAddsOnTopOfBasePose()
    {
        var basePose = new XafPose(1, 0);
        basePose.Bones[0] = new XafTransform
        {
            Translation = new Vector2(10, 20),
            Rotation = 0.5f,
            Scale = new Vector2(2, 2),
        };
        var additive = new XafPose(1, 0);
        additive.Bones[0] = new XafTransform
        {
            Translation = new Vector2(4, 6),
            Rotation = 0.25f,
            Scale = new Vector2(2, 2),
        };

        XafPose result = XafBlend.Add(basePose, additive, 0.5f);

        Assert.That(result.Bones[0].Translation, Is.EqualTo(new Vector2(12, 23)));
        Assert.That(result.Bones[0].Rotation, Is.EqualTo(0.625f).Within(0.001f));
        Assert.That(result.Bones[0].Scale, Is.EqualTo(new Vector2(3, 3)));
    }

    [Test]
    public void AdditiveSamplingStartsFromIdentity()
    {
        XafPackage package = Package();
        XafPackage additive = package;
        XafPose pose = XafSampler.Sample(
            additive,
            additive.Clips[0],
            0f,
            additive: true);

        Assert.That(pose.Bones[0].Translation, Is.EqualTo(Vector2.Zero));
        Assert.That(pose.Bones[0].Scale, Is.EqualTo(Vector2.One));
    }

    [Test]
    public void LoopingClipWrapsWithinItsLoopRange()
    {
        XafPackage package = Package();
        package.Clips[0].Loop = true;
        package.Clips[0].LoopStart = 0f;
        package.Clips[0].LoopEnd = 2f;

        XafPose pose = XafSampler.Sample(
            package,
            package.Clips[0],
            2.5f);

        Assert.That(pose.Bones[0].Translation.X,
            Is.EqualTo(5f).Within(0.001f));
    }

    private static XafChannel Channel(
        XafInterpolation first,
        params (float Time, float Value, XafInterpolation Interpolation)[] keys)
    {
        var channel = new XafChannel();
        foreach ((float time, float value, XafInterpolation interpolation) in keys)
        {
            channel.Keyframes.Add(new XafKeyframe
            {
                Time = time,
                Value = value,
                Interpolation = interpolation,
            });
        }
        return channel;
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
        var clip = new XafClip
        {
            Id = "test",
            Duration = 2f,
            Loop = false,
        };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.TranslationX,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        channel.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 10f });
        clip.Channels.Add(channel);
        package.Clips.Add(clip);
        return package;
    }
}
