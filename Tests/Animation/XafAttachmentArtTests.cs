using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Attachment art swaps, layer keys and hiding: sampled as held values, switched mid-fade, saved.</summary>
[TestFixture]
public sealed class RigAttachmentArtTests
{
    private static RigPackage Package(out RigClip clip)
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "hand.svg", Group = "hand", BoneId = "root", ZOrder = 2,
                Alternates = { new RigArt { SvgPath = "hand.svg", Group = "fist" } }
            });
        clip = package.Clips.Find(found => found.Id == "idle")!;
        return package;
    }

    private static void Key(RigClip clip, RigChannelProperty property, float time, float value) =>
        RigAuthoring.SetChannel(
            clip,
            RigChannelTarget.Attachment,
            "hand",
            property,
            time,
            value,
            RigInterpolation.Step);

    [Test]
    public void ArtLayerAndVisibilitySwitchAtTheirKeysAndStartAsAuthored()
    {
        RigPackage package = Package(out RigClip clip);
        Key(clip, RigChannelProperty.Art, 0.5f, 1f);
        Key(clip, RigChannelProperty.Layer, 0.5f, -3f);
        Key(clip, RigChannelProperty.Visible, 1.5f, 0f);

        RigAttachmentState before = RigSampler.Sample(package, clip, 0.25f).AttachmentStates[0];
        Assert.That((before.Art, before.LayerOf(package.Attachments[0]), before.Hidden), Is.EqualTo((0, 2, false)));

        RigAttachmentState after = RigSampler.Sample(package, clip, 0.75f).AttachmentStates[0];
        Assert.That((after.Art, after.LayerOf(package.Attachments[0]), after.Hidden), Is.EqualTo((1, -3, false)));
        Assert.That(RigSampler.Sample(package, clip, 1.6f).AttachmentStates[0].Hidden, Is.True);
        Assert.That(package.Attachments[0].Art(1).Group, Is.EqualTo("fist"));
    }

    [Test]
    public void AFadeSwitchesArtHalfWay()
    {
        RigPackage package = Package(out RigClip clip);
        Key(clip, RigChannelProperty.Art, 0f, 1f);
        RigPose open = RigSampler.Sample(package, package.Clips[1], 0f);
        RigPose fist = RigSampler.Sample(package, clip, 0f);

        Assert.That(RigBlend.Crossfade(open, fist, 0.4f).AttachmentStates[0].Art, Is.EqualTo(0));
        Assert.That(RigBlend.Crossfade(open, fist, 0.6f).AttachmentStates[0].Art, Is.EqualTo(1));
    }

    [Test]
    public void AlternatesAreSavedAndBadKeysAreRefused()
    {
        RigPackage package = Package(out RigClip clip);
        Assert.That(RigPackageCopy.AreEqual(package, RigBinary.Read(RigBinary.Write(package))), Is.True);

        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Art, 0f, 1f);
        Assert.That(string.Join(" ", RigValidation.Validate(package)), Does.Contain("which only attachments have"));
    }
}
