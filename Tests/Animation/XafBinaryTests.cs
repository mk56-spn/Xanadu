using Xanadu.Serialisation.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigBinaryTests
{
    [Test]
    public void RoundTripsACharacterPackage()
    {
        RigPackage package = Sample();

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));

        Assert.That(loaded.Major, Is.EqualTo(RigPackage.MajorVersion));
        Assert.That(loaded.Skeleton.Bones, Has.Count.EqualTo(2));
        Assert.That(loaded.Skeleton.Bones[0].Id, Is.EqualTo("root"));
        Assert.That(loaded.Skeleton.Bones[1].Parent, Is.EqualTo(0));
        Assert.That(loaded.Skeleton.Bones[1].Rest.Origin, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(loaded.Skeleton.Bones[1].ZOrder, Is.EqualTo(3));

        Assert.That(loaded.Attachments, Has.Count.EqualTo(2));
        Assert.That(loaded.Attachments[1].Group, Is.EqualTo("head"));
        Assert.That(loaded.Attachments[1].BoneId, Is.EqualTo("head-bone"));
        Assert.That(loaded.Attachments[1].ZOrder, Is.EqualTo(2));

        Assert.That(loaded.Clips, Has.Count.EqualTo(1));
        RigClip clip = loaded.Clips[0];
        Assert.That(clip.Id, Is.EqualTo("idle"));
        Assert.That(clip.Duration, Is.EqualTo(2f).Within(0.001f));
        Assert.That(clip.Loop, Is.True);
        Assert.That(clip.Channels, Has.Count.EqualTo(2));
        Assert.That(clip.Channels[0].Property, Is.EqualTo(RigChannelProperty.TranslationX));
        Assert.That(clip.Channels[0].Keyframes, Has.Count.EqualTo(2));
        Assert.That(clip.Channels[0].Keyframes[1].Interpolation, Is.EqualTo(RigInterpolation.CubicBezier));
        Assert.That(clip.Channels[0].Keyframes[1].TangentOut, Is.EqualTo(0.5f));
    }

    [Test]
    public void RejectsWrongMagic()
    {
        Assert.Throws<RigFormatException>(() => RigBinary.Read("NOPE"u8.ToArray()));
    }

    [Test]
    public void RejectsUnsupportedMajorVersion()
    {
        byte[] data = RigBinary.Write(Sample());
        data[4] = 9;

        Assert.Throws<RigFormatException>(() => RigBinary.Read(data));
    }

    [Test]
    public void RejectsMalformedPackage()
    {
        RigPackage package = Sample();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "detached",
                Parent = -1
            });

        Assert.That(RigValidation.Validate(package), Is.Not.Empty);
        Assert.Throws<RigFormatException>(() => RigBinary.Write(package));
    }

    [Test]
    public void RejectsMissingBoneTarget()
    {
        RigPackage package = Sample();
        package.Clips[0].Channels[0].TargetId = "missing";

        Assert.That(RigValidation.Validate(package), Is.Not.Empty);
    }

    [Test]
    public void RejectsUnsortedKeyframes()
    {
        RigPackage package = Sample();
        package.Clips[0].Channels[0].Keyframes[0].Time = 1.5f;

        Assert.That(RigValidation.Validate(package), Is.Not.Empty);
    }

    private static RigPackage Sample()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "root",
                Parent = -1,
                Rest = Transform2D.Identity,
                ZOrder = 0
            });
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "head-bone",
                Parent = 0,
                Rest = new Transform2D(0.5f, new Vector2(10, 20)),
                ZOrder = 3
            });
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "Assets/SVG/body.svg",
                Group = "body",
                BoneId = "root",
                Transform = Transform2D.Identity,
                ZOrder = 1
            });
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "Assets/SVG/head.svg",
                Group = "head",
                BoneId = "head-bone",
                Transform = new Transform2D(0.2f, new Vector2(4, 5)),
                ZOrder = 2
            });

        var clip = new RigClip
        {
            Id = "idle",
            Duration = 2f,
            Loop = true,
            LoopStart = 0f,
            LoopEnd = 2f
        };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "head-bone",
            Property = RigChannelProperty.TranslationX
        };
        channel.Keyframes.Add(
            new RigKeyframe
            {
                Time = 0f,
                Value = 0f,
                Interpolation = RigInterpolation.Linear
            });
        channel.Keyframes.Add(
            new RigKeyframe
            {
                Time = 1f,
                Value = 5f,
                Interpolation = RigInterpolation.CubicBezier,
                TangentOut = 0.5f
            });
        clip.Channels.Add(channel);

        var attachmentChannel = new RigChannel
        {
            Target = RigChannelTarget.Attachment,
            TargetId = "head",
            Property = RigChannelProperty.ScaleY
        };
        attachmentChannel.Keyframes.Add(
            new RigKeyframe
            {
                Time = 0f,
                Value = 1f
            });
        clip.Channels.Add(attachmentChannel);
        package.Clips.Add(clip);
        return package;
    }
}
