using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Removing alternate art keeps clips showing the same art, and picking mesh bones never loses the own bone.</summary>
[TestFixture]
public sealed class RigAttachmentAuthoringTests
{
    private static RigPackage Package()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "arm", new Vector2(0, -50));
        RigRigAuthoring.AddBone(package, 1, "forearm", new Vector2(0, 30));
        package.Attachments.Add(new RigAttachment { SvgPath = "a.svg", Group = "arm", BoneId = "arm" });
        return package;
    }

    [Test]
    public void DroppingArtMovesLaterKeysDownAndSendsItsOwnKeysHome()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];
        RigAttachmentAuthoring.AddAlternate(package, 0, "a.svg", "fist");
        RigAttachmentAuthoring.AddAlternate(package, 0, "a.svg", "point");
        RigAttachmentAuthoring.KeyArt(package, clip, 0, 0f, 1);
        RigAttachmentAuthoring.KeyArt(package, clip, 0, 1f, 2);

        RigAttachmentAuthoring.RemoveAlternate(package, 0, 1);

        Assert.That(RigSampler.Sample(package, clip, 0.5f).AttachmentStates[0].Art, Is.EqualTo(0));
        Assert.That(
            package.Attachments[0].Art(RigSampler.Sample(package, clip, 1.5f).AttachmentStates[0].Art).Group,
            Is.EqualTo("point"));
    }

    [Test]
    public void PickingMeshBonesStartsFromTheAutomaticOnesAndKeepsTheOwnBone()
    {
        RigPackage package = Package();
        RigAttachmentAuthoring.ToggleMesh(package, 0);
        RigAttachment arm = package.Attachments[0];

        Assert.That(RigAttachmentAuthoring.ToggleMeshBone(package, 0, 1), Is.False, "the own bone always bends it");
        RigAttachmentAuthoring.ToggleMeshBone(package, 0, 2);
        Assert.That(arm.Mesh!.Bones, Is.EqualTo(new[] { "arm" }), "the forearm is out; the arm stays");
        Assert.That(RigSkinWeights.Influences(package, arm), Is.EqualTo(new[] { 1 }));

        RigAttachmentAuthoring.ToggleMeshBone(package, 0, 0);
        Assert.That(RigSkinWeights.Influences(package, arm), Is.EqualTo(new[] { 1, 0 }));
    }
}
