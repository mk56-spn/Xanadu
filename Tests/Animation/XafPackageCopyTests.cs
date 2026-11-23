using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigPackageCopyTests
{
    private static RigPackage Sample()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "child", new Vector2(0, -40));
        package.Attachments.Add(
            new RigAttachment
            {
                SvgPath = "a.svg",
                Group = "a",
                BoneId = "child"
            });
        RigAuthoring.SetBoneProperty(package, package.Clips[0], "child", RigChannelProperty.Rotation, 0.5f, 1f);
        return package;
    }

    [Test]
    public void ACloneIsEqualButIndependent()
    {
        RigPackage original = Sample();

        RigPackage copy = RigPackageCopy.Clone(original);

        Assert.That(RigPackageCopy.AreEqual(original, copy), Is.True);
        copy.Clips[0].Channels[0].Keyframes[0].Value = 9f;
        copy.Skeleton.Bones[1].AllowTranslation = true;
        Assert.That(original.Clips[0].Channels[0].Keyframes[0].Value, Is.EqualTo(1f));
        Assert.That(original.Skeleton.Bones[1].AllowTranslation, Is.False);
    }

    [Test]
    public void AnyDifferenceMakesPackagesUnequal()
    {
        RigPackage original = Sample();

        foreach (System.Action<RigPackage> change in new System.Action<RigPackage>[]
                 {
                     p => p.Skeleton.Bones[1].Rest = new Transform2D(0f, new Vector2(1, 1)),
                     p => p.Skeleton.Bones[1].AllowTranslation = true,
                     p => p.Attachments[0].Group = "b",
                     p => p.Clips[0].Duration = 3f,
                     p => p.Clips[0].Channels[0].Keyframes[0].Time = 0.6f,
                     p => p.Clips[0].Channels[0].Keyframes.Add(new RigKeyframe { Time = 1.5f }),
                     p => p.Clips.RemoveAt(1)
                 })
        {
            RigPackage copy = RigPackageCopy.Clone(original);
            change(copy);
            Assert.That(RigPackageCopy.AreEqual(original, copy), Is.False);
        }
    }
}
