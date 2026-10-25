using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafPackageCopyTests
{
    private static XafPackage Sample()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "child", new Vector2(0, -40));
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://a.svg",
            Group = "a",
            BoneId = "child",
        });
        XafAuthoring.SetBoneProperty(package, package.Clips[0], "child",
            XafChannelProperty.Rotation, 0.5f, 1f);
        return package;
    }

    [Test]
    public void ACloneIsEqualButIndependent()
    {
        XafPackage original = Sample();

        XafPackage copy = XafPackageCopy.Clone(original);

        Assert.That(XafPackageCopy.AreEqual(original, copy), Is.True);
        copy.Clips[0].Channels[0].Keyframes[0].Value = 9f;
        copy.Skeleton.Bones[1].AllowTranslation = true;
        Assert.That(original.Clips[0].Channels[0].Keyframes[0].Value, Is.EqualTo(1f));
        Assert.That(original.Skeleton.Bones[1].AllowTranslation, Is.False);
    }

    [Test]
    public void AnyDifferenceMakesPackagesUnequal()
    {
        XafPackage original = Sample();

        foreach (System.Action<XafPackage> change in new System.Action<XafPackage>[]
                 {
                     p => p.Skeleton.Bones[1].Rest = new Transform2D(0f, new Vector2(1, 1)),
                     p => p.Skeleton.Bones[1].AllowTranslation = true,
                     p => p.Attachments[0].Group = "b",
                     p => p.Clips[0].Duration = 3f,
                     p => p.Clips[0].Channels[0].Keyframes[0].Time = 0.6f,
                     p => p.Clips[0].Channels[0].Keyframes.Add(new XafKeyframe { Time = 1.5f }),
                     p => p.Clips.RemoveAt(1),
                 })
        {
            XafPackage copy = XafPackageCopy.Clone(original);
            change(copy);
            Assert.That(XafPackageCopy.AreEqual(original, copy), Is.False);
        }
    }
}
