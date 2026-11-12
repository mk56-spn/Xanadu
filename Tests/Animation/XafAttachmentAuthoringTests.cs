using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Removing alternate art keeps clips showing the same art, and picking mesh bones never loses the own bone.</summary>
[TestFixture]
public sealed class XafAttachmentAuthoringTests
{
    private static XafPackage Package()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "arm", new Vector2(0, -50));
        XafRigAuthoring.AddBone(package, 1, "forearm", new Vector2(0, 30));
        package.Attachments.Add(new XafAttachment { SvgPath = "a.svg", Group = "arm", BoneId = "arm" });
        return package;
    }

    [Test]
    public void DroppingArtMovesLaterKeysDownAndSendsItsOwnKeysHome()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];
        XafAttachmentAuthoring.AddAlternate(package, 0, "a.svg", "fist");
        XafAttachmentAuthoring.AddAlternate(package, 0, "a.svg", "point");
        XafAttachmentAuthoring.KeyArt(package, clip, 0, 0f, 1);
        XafAttachmentAuthoring.KeyArt(package, clip, 0, 1f, 2);

        XafAttachmentAuthoring.RemoveAlternate(package, 0, 1);

        Assert.That(XafSampler.Sample(package, clip, 0.5f).AttachmentStates[0].Art, Is.EqualTo(0));
        Assert.That(package.Attachments[0].Art(XafSampler.Sample(package, clip, 1.5f).AttachmentStates[0].Art).Group,
            Is.EqualTo("point"));
    }

    [Test]
    public void PickingMeshBonesStartsFromTheAutomaticOnesAndKeepsTheOwnBone()
    {
        XafPackage package = Package();
        XafAttachmentAuthoring.ToggleMesh(package, 0);
        XafAttachment arm = package.Attachments[0];

        Assert.That(XafAttachmentAuthoring.ToggleMeshBone(package, 0, 1), Is.False, "the own bone always bends it");
        XafAttachmentAuthoring.ToggleMeshBone(package, 0, 2);
        Assert.That(arm.Mesh!.Bones, Is.EqualTo(new[] { "arm" }), "the forearm is out; the arm stays");
        Assert.That(XafSkinWeights.Influences(package, arm), Is.EqualTo(new[] { 1 }));

        XafAttachmentAuthoring.ToggleMeshBone(package, 0, 0);
        Assert.That(XafSkinWeights.Influences(package, arm), Is.EqualTo(new[] { 1, 0 }));
    }
}
