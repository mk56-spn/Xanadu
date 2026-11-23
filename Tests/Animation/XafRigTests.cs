using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigRigTests
{
    [Test]
    public void AppliesParentTransformsToBones()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "root",
                Parent = -1,
                Rest = Transform2D.Identity
            });
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "child",
                Parent = 0,
                Rest = new Transform2D(0f, new Vector2(10, 20))
            });
        var pose = new RigPose(2, 0);
        pose.Bones[0] = new RigTransform
        {
            Translation = new Vector2(100, 50),
            Scale = Vector2.One
        };
        pose.Bones[1] = RigTransform.From(new Transform2D(0f, new Vector2(10, 20)));

        Transform2D[] world = RigRig.WorldBones(package, pose);

        Assert.That(world[1].Origin, Is.EqualTo(new Vector2(110, 70)));
    }

    [Test]
    public void AttachmentUsesBoneWorldTransform()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "root",
                Parent = -1,
                Rest = Transform2D.Identity
            });
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "body.svg",
                Group = "body",
                BoneId = "root",
                Transform = new Transform2D(0f, new Vector2(4, 6))
            });
        var pose = new RigPose(1, 1);
        pose.Bones[0] = RigTransform.Identity;
        pose.Attachments[0] = RigTransform.From(new Transform2D(0f, new Vector2(4, 6)));

        Transform2D[] bones = RigRig.WorldBones(package, pose);
        Transform2D[] attachments = RigRig.WorldAttachments(package, pose, bones);

        Assert.That(attachments[0].Origin, Is.EqualTo(new Vector2(4, 6)));
    }
}
