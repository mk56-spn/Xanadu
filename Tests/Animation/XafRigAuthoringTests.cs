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
}
