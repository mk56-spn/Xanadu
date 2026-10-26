using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafRigAuthoringTests
{
    [Test]
    public void EmptyPackageHasOneRootAndDefaultClips()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();

        Assert.That(package.Skeleton.Bones, Has.Count.EqualTo(1));
        Assert.That(package.Skeleton.Bones[0].Parent, Is.EqualTo(-1));
        Assert.That(package.Clips, Has.Count.EqualTo(4));
    }

    [Test]
    public void AddsAndDeletesAChildBone()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        int child = XafRigAuthoring.AddBone(
            package,
            0,
            "child",
            new Vector2(0, -40));

        Assert.That(child, Is.EqualTo(1));
        Assert.That(package.Skeleton.Bones[child].Parent, Is.EqualTo(0));
        Assert.That(XafRigAuthoring.DeleteBone(package, child), Is.True);
        Assert.That(package.Skeleton.Bones, Has.Count.EqualTo(1));
    }

    [Test]
    public void RejectsDeletingRootOrParentsOfBones()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "child", Vector2.Zero);
        XafRigAuthoring.AddBone(package, 1, "grandchild", Vector2.Zero);

        Assert.That(XafRigAuthoring.DeleteBone(package, 0), Is.False);
        Assert.That(XafRigAuthoring.DeleteBone(package, 1), Is.False);
    }

    [Test]
    public void RejectsDuplicateAndMissingParents()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();

        Assert.That(XafRigAuthoring.AddBone(package, 0, "root", Vector2.Zero),
            Is.EqualTo(-1));
        Assert.That(XafRigAuthoring.AddBone(package, 99, "child", Vector2.Zero),
            Is.EqualTo(-1));
    }

    [Test]
    public void RenamingABoneUpdatesAttachmentsAndChannels()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);
        package.Attachments.Add(new XafAttachment { Group = "sleeve", BoneId = "arm" });
        package.Clips[0].Channels.Add(new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "arm",
        });
        package.Clips[0].Channels.Add(new XafChannel
        {
            Target = XafChannelTarget.Attachment,
            TargetId = "arm",
        });

        Assert.That(XafRigAuthoring.RenameBone(package, 1, "upper-arm"), Is.True);

        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("upper-arm"));
        Assert.That(package.Attachments[0].BoneId, Is.EqualTo("upper-arm"));
        Assert.That(package.Clips[0].Channels[0].TargetId, Is.EqualTo("upper-arm"));
        Assert.That(package.Clips[0].Channels[1].TargetId, Is.EqualTo("arm"));
    }

    [Test]
    public void RejectsEmptyOrTakenBoneNames()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);

        Assert.That(XafRigAuthoring.RenameBone(package, 1, " "), Is.False);
        Assert.That(XafRigAuthoring.RenameBone(package, 1, "root"), Is.False);
        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("arm"));
    }

    [Test]
    public void ReparentingKeepsTheBoneWhereItIsAndParentsFirst()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "hand", new Vector2(10, 0));
        XafRigAuthoring.AddBone(package, 0, "arm", new Vector2(0, -40));
        Transform2D handBefore = XafRig.WorldBones(package, RestPose(package))[1];

        Assert.That(XafRigAuthoring.ReparentBone(package, 1, 2), Is.True);

        // The arm now comes before its new child.
        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("arm"));
        Assert.That(package.Skeleton.Bones[2].Id, Is.EqualTo("hand"));
        Assert.That(package.Skeleton.Bones[2].Parent, Is.EqualTo(1));
        Assert.That(package.Skeleton.Bones[1].Parent, Is.EqualTo(0));
        Transform2D handAfter = XafRig.WorldBones(package, RestPose(package))[2];
        Assert.That(handAfter.Origin.DistanceTo(handBefore.Origin), Is.LessThan(0.001f));
        Assert.That(package.Skeleton.Bones[2].Rest.Origin.DistanceTo(new Vector2(10, 40)),
            Is.LessThan(0.001f));
    }

    [Test]
    public void RejectsReparentingTheRootOrIntoItsOwnSubtree()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);
        XafRigAuthoring.AddBone(package, 1, "hand", Vector2.Zero);

        Assert.That(XafRigAuthoring.ReparentBone(package, 0, 1), Is.False);
        Assert.That(XafRigAuthoring.ReparentBone(package, 1, 1), Is.False);
        Assert.That(XafRigAuthoring.ReparentBone(package, 1, 2), Is.False);
        Assert.That(XafRigAuthoring.ReparentBone(package, 2, 1), Is.False);
        Assert.That(XafRigAuthoring.ReparentBone(package, 2, 0), Is.True);
    }

    private static XafPose RestPose(XafPackage package)
    {
        var pose = new XafPose(package.Skeleton.Bones.Count, package.Attachments.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        return pose;
    }
}
