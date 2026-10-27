using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafBinaryTests
{
    [Test]
    public void RoundTripsACharacterPackage()
    {
        XafPackage package = Sample();

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(loaded.Major, Is.EqualTo(XafPackage.MajorVersion));
        Assert.That(loaded.Skeleton.Bones, Has.Count.EqualTo(2));
        Assert.That(loaded.Skeleton.Bones[0].Id, Is.EqualTo("root"));
        Assert.That(loaded.Skeleton.Bones[1].Parent, Is.EqualTo(0));
        Assert.That(loaded.Skeleton.Bones[1].Rest.Origin,
            Is.EqualTo(new Vector2(10, 20)));
        Assert.That(loaded.Skeleton.Bones[1].ZOrder, Is.EqualTo(3));

        Assert.That(loaded.Attachments, Has.Count.EqualTo(2));
        Assert.That(loaded.Attachments[1].Group, Is.EqualTo("head"));
        Assert.That(loaded.Attachments[1].BoneId, Is.EqualTo("head-bone"));
        Assert.That(loaded.Attachments[1].ZOrder, Is.EqualTo(2));

        Assert.That(loaded.Clips, Has.Count.EqualTo(1));
        XafClip clip = loaded.Clips[0];
        Assert.That(clip.Id, Is.EqualTo("idle"));
        Assert.That(clip.Duration, Is.EqualTo(2f).Within(0.001f));
        Assert.That(clip.Loop, Is.True);
        Assert.That(clip.Channels, Has.Count.EqualTo(2));
        Assert.That(clip.Channels[0].Property,
            Is.EqualTo(XafChannelProperty.TranslationX));
        Assert.That(clip.Channels[0].Keyframes, Has.Count.EqualTo(2));
        Assert.That(clip.Channels[0].Keyframes[1].Interpolation,
            Is.EqualTo(XafInterpolation.CubicBezier));
        Assert.That(clip.Channels[0].Keyframes[1].TangentOut,
            Is.EqualTo(0.5f));
    }

    [Test]
    public void SavesAndLoadsFromDisk()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-" + Guid.NewGuid().ToString("N") + ".xaf");

        try
        {
            XafBinary.Save(path, Sample());
            Assert.That(File.Exists(path), Is.True);
            Assert.That(XafBinary.Load(path).Clips[0].Id, Is.EqualTo("idle"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void RejectsWrongMagic()
    {
        Assert.Throws<XafFormatException>(() =>
            XafBinary.Read("NOPE"u8.ToArray()));
    }

    [Test]
    public void RejectsUnsupportedMajorVersion()
    {
        byte[] data = XafBinary.Write(Sample());
        data[4] = 9;

        Assert.Throws<XafFormatException>(() => XafBinary.Read(data));
    }

    [Test]
    public void RejectsMalformedPackage()
    {
        XafPackage package = Sample();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "detached",
            Parent = -1,
        });

        Assert.That(XafValidation.Validate(package), Is.Not.Empty);
        Assert.Throws<XafFormatException>(() => XafBinary.Write(package));
    }

    [Test]
    public void RejectsMissingBoneTarget()
    {
        XafPackage package = Sample();
        package.Clips[0].Channels[0].TargetId = "missing";

        Assert.That(XafValidation.Validate(package), Is.Not.Empty);
    }

    [Test]
    public void RejectsUnsortedKeyframes()
    {
        XafPackage package = Sample();
        package.Clips[0].Channels[0].Keyframes[0].Time = 1.5f;

        Assert.That(XafValidation.Validate(package), Is.Not.Empty);
    }

    private static XafPackage Sample()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
            ZOrder = 0,
        });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "head-bone",
            Parent = 0,
            Rest = new Transform2D(0.5f, new Vector2(10, 20)),
            ZOrder = 3,
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://Assets/SVG/body.svg",
            Group = "body",
            BoneId = "root",
            Transform = Transform2D.Identity,
            ZOrder = 1,
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://Assets/SVG/head.svg",
            Group = "head",
            BoneId = "head-bone",
            Transform = new Transform2D(0.2f, new Vector2(4, 5)),
            ZOrder = 2,
        });

        var clip = new XafClip
        {
            Id = "idle",
            Duration = 2f,
            Loop = true,
            LoopStart = 0f,
            LoopEnd = 2f,
        };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "head-bone",
            Property = XafChannelProperty.TranslationX,
        };
        channel.Keyframes.Add(new XafKeyframe
        {
            Time = 0f,
            Value = 0f,
            Interpolation = XafInterpolation.Linear,
        });
        channel.Keyframes.Add(new XafKeyframe
        {
            Time = 1f,
            Value = 5f,
            Interpolation = XafInterpolation.CubicBezier,
            TangentOut = 0.5f,
        });
        clip.Channels.Add(channel);

        var attachmentChannel = new XafChannel
        {
            Target = XafChannelTarget.Attachment,
            TargetId = "head",
            Property = XafChannelProperty.ScaleY,
        };
        attachmentChannel.Keyframes.Add(new XafKeyframe
        {
            Time = 0f,
            Value = 1f,
        });
        clip.Channels.Add(attachmentChannel);
        package.Clips.Add(clip);
        return package;
    }
}
