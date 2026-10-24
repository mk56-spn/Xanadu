using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafRigTests
{
    [Test]
    public void AppliesParentTransformsToBones()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
        });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "child",
            Parent = 0,
            Rest = new Transform2D(0f, new Vector2(10, 20)),
        });
        var pose = new XafPose(2, 0);
        pose.Bones[0] = new XafTransform
        {
            Translation = new Vector2(100, 50),
            Scale = Vector2.One,
        };
        pose.Bones[1] = XafTransform.From(
            new Transform2D(0f, new Vector2(10, 20)));

        Transform2D[] world = XafRig.WorldBones(package, pose);

        Assert.That(world[1].Origin, Is.EqualTo(new Vector2(110, 70)));
    }

    [Test]
    public void AttachmentUsesBoneWorldTransform()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://body.svg",
            Group = "body",
            BoneId = "root",
            Transform = new Transform2D(0f, new Vector2(4, 6)),
        });
        var pose = new XafPose(1, 1);
        pose.Bones[0] = XafTransform.Identity;
        pose.Attachments[0] = XafTransform.From(
            new Transform2D(0f, new Vector2(4, 6)));

        Transform2D[] bones = XafRig.WorldBones(package, pose);
        Transform2D[] attachments = XafRig.WorldAttachments(
            package,
            pose,
            bones);

        Assert.That(attachments[0].Origin, Is.EqualTo(new Vector2(4, 6)));
    }
}
