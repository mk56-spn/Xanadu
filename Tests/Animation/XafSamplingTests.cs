using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigSamplingTests
{
    [Test]
    public void SamplesLinearStepAndBezierChannels()
    {
        RigChannel channel = Channel(
            RigInterpolation.Linear,
            (0f, 0f, RigInterpolation.Linear),
            (1f, 10f, RigInterpolation.Linear));
        Assert.That(RigSampler.SampleChannel(channel, 0.5f), Is.EqualTo(5f).Within(0.001f));

        channel.Keyframes[0].Interpolation = RigInterpolation.Step;
        Assert.That(RigSampler.SampleChannel(channel, 0.5f), Is.EqualTo(0f).Within(0.001f));

        channel.Keyframes[0].Interpolation = RigInterpolation.CubicBezier;
        channel.Keyframes[0].TangentOut = 0f;
        channel.Keyframes[1].TangentIn = 0f;
        Assert.That(RigSampler.SampleChannel(channel, 0.5f), Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void CrossfadeBlendsTranslationRotationAndScale()
    {
        var from = new RigPose(1, 0);
        from.Bones[0] = new RigTransform
        {
            Translation = new Vector2(0, 0),
            Rotation = 0,
            Scale = Vector2.One
        };
        var to = new RigPose(1, 0);
        to.Bones[0] = new RigTransform
        {
            Translation = new Vector2(10, 20),
            Rotation = 1f,
            Scale = new Vector2(2, 3)
        };

        RigPose result = RigBlend.Crossfade(from, to, 0.5f);

        Assert.That(result.Bones[0].Translation, Is.EqualTo(new Vector2(5, 10)));
        Assert.That(result.Bones[0].Rotation, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(result.Bones[0].Scale, Is.EqualTo(new Vector2(1.5f, 2f)));
    }

    [Test]
    public void AdditiveLayerAddsOnTopOfBasePose()
    {
        var basePose = new RigPose(1, 0);
        basePose.Bones[0] = new RigTransform
        {
            Translation = new Vector2(10, 20),
            Rotation = 0.5f,
            Scale = new Vector2(2, 2)
        };
        var additive = new RigPose(1, 0);
        additive.Bones[0] = new RigTransform
        {
            Translation = new Vector2(4, 6),
            Rotation = 0.25f,
            Scale = new Vector2(2, 2)
        };

        RigPose result = RigBlend.Add(basePose, additive, 0.5f);

        Assert.That(result.Bones[0].Translation, Is.EqualTo(new Vector2(12, 23)));
        Assert.That(result.Bones[0].Rotation, Is.EqualTo(0.625f).Within(0.001f));
        Assert.That(result.Bones[0].Scale, Is.EqualTo(new Vector2(3, 3)));
    }

    [Test]
    public void AdditiveSamplingStartsFromIdentity()
    {
        RigPackage package = Package();
        RigPackage additive = package;
        RigPose pose = RigSampler.Sample(additive, additive.Clips[0], 0f, true);

        Assert.That(pose.Bones[0].Translation, Is.EqualTo(Vector2.Zero));
        Assert.That(pose.Bones[0].Scale, Is.EqualTo(Vector2.One));
    }

    [Test]
    public void LoopingClipWrapsWithinItsLoopRange()
    {
        RigPackage package = Package();
        package.Clips[0].Loop = true;
        package.Clips[0].LoopStart = 0f;
        package.Clips[0].LoopEnd = 2f;

        RigPose pose = RigSampler.Sample(package, package.Clips[0], 2.5f);

        Assert.That(pose.Bones[0].Translation.X, Is.EqualTo(5f).Within(0.001f));
    }

    private static RigChannel Channel(
        RigInterpolation first,
        params (float Time, float Value, RigInterpolation Interpolation)[] keys)
    {
        var channel = new RigChannel();
        foreach ((float time, float value, RigInterpolation interpolation) in keys)
        {
            channel.Keyframes.Add(
                new RigKeyframe
                {
                    Time = time,
                    Value = value,
                    Interpolation = interpolation
                });
        }

        return channel;
    }

    private static RigPackage Package()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "root",
                Parent = -1,
                Rest = Transform2D.Identity
            });
        var clip = new RigClip
        {
            Id = "test",
            Duration = 2f,
            Loop = false
        };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "root",
            Property = RigChannelProperty.TranslationX
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 0f, Value = 0f });
        channel.Keyframes.Add(new RigKeyframe { Time = 1f, Value = 10f });
        clip.Channels.Add(channel);
        package.Clips.Add(clip);
        return package;
    }
}
