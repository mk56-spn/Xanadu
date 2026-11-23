using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigRigAuthoringTests
{
    [Test]
    public void EmptyPackageHasOneRootAndDefaultClips()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();

        Assert.That(package.Skeleton.Bones, Has.Count.EqualTo(1));
        Assert.That(package.Skeleton.Bones[0].Parent, Is.EqualTo(-1));
        Assert.That(package.Clips, Has.Count.EqualTo(4));
    }

    [Test]
    public void AddsAndDeletesAChildBone()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        int child = RigRigAuthoring.AddBone(package, 0, "child", new Vector2(0, -40));

        Assert.That(child, Is.EqualTo(1));
        Assert.That(package.Skeleton.Bones[child].Parent, Is.EqualTo(0));
        Assert.That(RigRigAuthoring.DeleteBone(package, child), Is.True);
        Assert.That(package.Skeleton.Bones, Has.Count.EqualTo(1));
    }

    [Test]
    public void RejectsDeletingRootOrParentsOfBones()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "child", Vector2.Zero);
        RigRigAuthoring.AddBone(package, 1, "grandchild", Vector2.Zero);

        Assert.That(RigRigAuthoring.DeleteBone(package, 0), Is.False);
        Assert.That(RigRigAuthoring.DeleteBone(package, 1), Is.False);
    }

    [Test]
    public void RejectsDuplicateAndMissingParents()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();

        Assert.That(RigRigAuthoring.AddBone(package, 0, "root", Vector2.Zero), Is.EqualTo(-1));
        Assert.That(RigRigAuthoring.AddBone(package, 99, "child", Vector2.Zero), Is.EqualTo(-1));
    }

    [Test]
    public void RenamingABoneUpdatesAttachmentsAndChannels()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);
        package.Attachments.Add(new RigAttachment { Group = "sleeve", BoneId = "arm" });
        package.Clips[0].Channels.Add(
            new RigChannel
            {
                Target = RigChannelTarget.Bone,
                TargetId = "arm"
            });
        package.Clips[0].Channels.Add(
            new RigChannel
            {
                Target = RigChannelTarget.Attachment,
                TargetId = "arm"
            });

        Assert.That(RigRigAuthoring.RenameBone(package, 1, "upper-arm"), Is.True);

        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("upper-arm"));
        Assert.That(package.Attachments[0].BoneId, Is.EqualTo("upper-arm"));
        Assert.That(package.Clips[0].Channels[0].TargetId, Is.EqualTo("upper-arm"));
        Assert.That(package.Clips[0].Channels[1].TargetId, Is.EqualTo("arm"));
    }

    [Test]
    public void RejectsEmptyOrTakenBoneNames()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);

        Assert.That(RigRigAuthoring.RenameBone(package, 1, " "), Is.False);
        Assert.That(RigRigAuthoring.RenameBone(package, 1, "root"), Is.False);
        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("arm"));
    }

    [Test]
    public void ReparentingKeepsTheBoneWhereItIsAndParentsFirst()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "hand", new Vector2(10, 0));
        RigRigAuthoring.AddBone(package, 0, "arm", new Vector2(0, -40));
        Transform2D handBefore = RigRig.WorldBones(package, RestPose(package))[1];

        Assert.That(RigRigAuthoring.ReparentBone(package, 1, 2), Is.True);

        // The arm now comes before its new child.
        Assert.That(package.Skeleton.Bones[1].Id, Is.EqualTo("arm"));
        Assert.That(package.Skeleton.Bones[2].Id, Is.EqualTo("hand"));
        Assert.That(package.Skeleton.Bones[2].Parent, Is.EqualTo(1));
        Assert.That(package.Skeleton.Bones[1].Parent, Is.EqualTo(0));
        Transform2D handAfter = RigRig.WorldBones(package, RestPose(package))[2];
        Assert.That(handAfter.Origin.DistanceTo(handBefore.Origin), Is.LessThan(0.001f));
        Assert.That(package.Skeleton.Bones[2].Rest.Origin.DistanceTo(new Vector2(10, 40)), Is.LessThan(0.001f));
    }

    [Test]
    public void RejectsReparentingTheRootOrIntoItsOwnSubtree()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "arm", Vector2.Zero);
        RigRigAuthoring.AddBone(package, 1, "hand", Vector2.Zero);

        Assert.That(RigRigAuthoring.ReparentBone(package, 0, 1), Is.False);
        Assert.That(RigRigAuthoring.ReparentBone(package, 1, 1), Is.False);
        Assert.That(RigRigAuthoring.ReparentBone(package, 1, 2), Is.False);
        Assert.That(RigRigAuthoring.ReparentBone(package, 2, 1), Is.False);
        Assert.That(RigRigAuthoring.ReparentBone(package, 2, 0), Is.True);
    }

    private static RigPose RestPose(RigPackage package)
    {
        var pose = new RigPose(package.Skeleton.Bones.Count, package.Attachments.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = RigTransform.From(package.Skeleton.Bones[i].Rest);
        return pose;
    }
}
