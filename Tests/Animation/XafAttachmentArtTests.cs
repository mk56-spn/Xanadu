using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Attachment art swaps, layer keys and hiding: sampled as held values, switched mid-fade, saved.</summary>
[TestFixture]
public sealed class XafAttachmentArtTests
{
    private static XafPackage Package(out XafClip clip)
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://hand.svg", Group = "hand", BoneId = "root", ZOrder = 2,
            Alternates = { new XafArt { SvgPath = "res://hand.svg", Group = "fist" } },
        });
        clip = package.Clips.Find(found => found.Id == "idle")!;
        return package;
    }

    private static void Key(XafClip clip, XafChannelProperty property, float time, float value) =>
        XafAuthoring.SetChannel(clip, XafChannelTarget.Attachment, "hand", property, time, value, XafInterpolation.Step);

    [Test]
    public void ArtLayerAndVisibilitySwitchAtTheirKeysAndStartAsAuthored()
    {
        XafPackage package = Package(out XafClip clip);
        Key(clip, XafChannelProperty.Art, 0.5f, 1f);
        Key(clip, XafChannelProperty.Layer, 0.5f, -3f);
        Key(clip, XafChannelProperty.Visible, 1.5f, 0f);

        XafAttachmentState before = XafSampler.Sample(package, clip, 0.25f).AttachmentStates[0];
        Assert.That((before.Art, before.LayerOf(package.Attachments[0]), before.Hidden), Is.EqualTo((0, 2, false)));

        XafAttachmentState after = XafSampler.Sample(package, clip, 0.75f).AttachmentStates[0];
        Assert.That((after.Art, after.LayerOf(package.Attachments[0]), after.Hidden), Is.EqualTo((1, -3, false)));
        Assert.That(XafSampler.Sample(package, clip, 1.6f).AttachmentStates[0].Hidden, Is.True);
        Assert.That(package.Attachments[0].Art(1).Group, Is.EqualTo("fist"));
    }

    [Test]
    public void AFadeSwitchesArtHalfWay()
    {
        XafPackage package = Package(out XafClip clip);
        Key(clip, XafChannelProperty.Art, 0f, 1f);
        XafPose open = XafSampler.Sample(package, package.Clips[1], 0f);
        XafPose fist = XafSampler.Sample(package, clip, 0f);

        Assert.That(XafBlend.Crossfade(open, fist, 0.4f).AttachmentStates[0].Art, Is.EqualTo(0));
        Assert.That(XafBlend.Crossfade(open, fist, 0.6f).AttachmentStates[0].Art, Is.EqualTo(1));
    }

    [Test]
    public void AlternatesAreSavedAndBadKeysAreRefused()
    {
        XafPackage package = Package(out XafClip clip);
        Assert.That(XafPackageCopy.AreEqual(package, XafBinary.Read(XafBinary.Write(package))), Is.True);

        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "root", XafChannelProperty.Art, 0f, 1f);
        Assert.That(string.Join(" ", XafValidation.Validate(package)), Does.Contain("which only attachments have"));
    }
}
